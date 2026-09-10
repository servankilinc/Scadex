/**
 * WHEP (WebRTC-HTTP Egress Protocol) istemcisi — **kütüphanesiz**.
 *
 * Bir WHEP oturumu tek bir HTTP POST'tan ibarettir: SDP teklifi gönderilir,
 * SDP cevabı alınır. Bunun için bir kütüphane eklemek, tarayıcının zaten
 * sağladığı `RTCPeerConnection`'ın üzerine yüz kilobaytlık bir katman
 * koymaktan başka işe yaramazdı.
 *
 * **Bu çağrı `axios`'tan GEÇMEZ.** İstek kendi API'mize değil doğrudan medya
 * geçidine (farklı origin) gider, gövdesi JSON değil `application/sdp`'dir ve
 * cevabı da düz metindir; `axios-helper`'ın her hatayı `ApiError`'a çeviren
 * yorumlayıcısı burada yanlış olurdu. Bilet isteği ise normal bir API
 * çağrısıdır ve `axios` ile gider (`src/api/camera-stream.ts`).
 */

/** ICE toplama tavanı. Aşılırsa eldeki adaylarla devam edilir. */
const ICE_GATHERING_TIMEOUT_MS = 3000;

export interface WhepSession {
  /** Bağlantıyı kapatır ve geçitteki oturumu serbest bırakır. */
  close(): void;
}

export async function whepConnect(
  whepUrl: string,
  ticket: string,
  videoEl: HTMLVideoElement,
  onStateChange?: (state: RTCPeerConnectionState) => void
): Promise<WhepSession> {
  // `iceServers` BOŞ: kamera da medya geçidi de aynı yerel ağda. STUN/TURN
  // eklemek, hiçbir zaman kullanılmayacak adaylar için el sıkışmayı geciktirirdi.
  const pc = new RTCPeerConnection({ iceServers: [] });

  // Transceiver'lar teklif ÜRETİLMEDEN ÖNCE eklenmeli. Sonra eklenirse SDP
  // teklifinde hiçbir medya hattı olmaz, geçit de "istenen bir şey yok" diye
  // sessizce boş bir cevap döner — bağlantı kurulur ama görüntü hiç gelmez.
  pc.addTransceiver('video', { direction: 'recvonly' });
  pc.addTransceiver('audio', { direction: 'recvonly' });

  pc.ontrack = event => {
    // `streams` boş gelebilir (SDP'de a=msid yoksa). Kontrolsüz atamak
    // `srcObject`'e undefined yazmak olurdu.
    videoEl.srcObject = event.streams[0] ?? null;
  };

  if (onStateChange) {
    pc.onconnectionstatechange = () => onStateChange(pc.connectionState);
  }

  let sessionUrl: string | null = null;
  let closed = false;

  const close = () => {
    if (closed) return;
    closed = true;

    pc.onconnectionstatechange = null;
    pc.ontrack = null;
    pc.close();
    videoEl.srcObject = null;

    // Geçitteki oturumu HEMEN bırak. Bu olmadan MediaMTX kameradan çekmeye
    // `sourceOnDemandCloseAfter` dolana kadar devam eder; 12 kutucuklu bir
    // sayfadan çıkıldığında bu, on saniye boyunca boşa akan 12 RTSP oturumu
    // demek. En iyi çaba: başarısızlığı umursamıyoruz, zaman aşımı yine kapatır.
    if (sessionUrl) {
      void fetch(sessionUrl, {
        method: 'DELETE',
        headers: { Authorization: basicTicket(ticket) }
      }).catch(() => undefined);
    }
  };

  try {
    const offer = await pc.createOffer();
    await pc.setLocalDescription(offer);

    // Trickle ICE UYGULANMIYOR: teklif tek parça gönderiliyor. Trickle, aday
    // başına ayrı bir HTTP isteği (ve sunucuda oturum durumu) gerektirir;
    // yerel ağda toplama zaten milisaniyeler sürdüğü için kazancı yok.
    await waitForIceGathering(pc, ICE_GATHERING_TIMEOUT_MS);

    const response = await fetch(whepUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/sdp',
        // Geçit Basic başlığını ikiye ayırıp parola kısmını bize sorar.
        // Kullanıcı adı sabit "ticket"; anlamlı olan bilettir.
        Authorization: basicTicket(ticket)
      },
      body: pc.localDescription!.sdp
    });

    if (!response.ok) {
      const detail = await response.text().catch(() => '');
      throw new Error(
        response.status === 401
          ? // 401'in İKİ sebebi var ve istemciden ayırt EDİLEMEZLER: bilet
            // gerçekten geçersiz olabilir, ya da geçit bileti doğrulatacağı
            // adrese (`authHTTPAddress`) ulaşamıyor olabilir — ikisinde de
            // geçit aynı 401'i döndürür.
            //
            // Mesaj yalnızca birincisini söylerse yanlış yere baktırır: bu
            // tam olarak yaşandı, `authHTTPAddress` eski test projesini
            // gösterirken saatler "bilet neden geçersiz" diye arandı.
            'Yayın açılamadı: medya geçidi bileti doğrulayamadı. Bilet süresi dolmuş olabilir ya da geçidin authHTTPAddress ayarı bu sunucuyu göstermiyor olabilir.'
          : `Medya geçidi bağlantıyı reddetti (${response.status}). ${detail}`.trim()
      );
    }

    // Oturumun kendi adresi. Kapanışta bunu DELETE ediyoruz.
    const location = response.headers.get('location');
    if (location) sessionUrl = new URL(location, whepUrl).toString();

    const answerSdp = await response.text();
    await pc.setRemoteDescription({ type: 'answer', sdp: answerSdp });

    // El sıkışma sırasında dışarıdan kapatılmış olabilir.
    if (closed) throw new Error('Bağlantı kapatıldı.');

    return { close };
  } catch (error) {
    close();
    throw error;
  }
}

const basicTicket = (ticket: string) => `Basic ${btoa(`ticket:${ticket}`)}`;

/**
 * ICE toplaması bitene kadar bekler; süre dolarsa **yine de devam eder**.
 *
 * Zaman aşımında hata fırlatmak yanlış olurdu: o ana kadar toplanmış adaylar
 * yerel ağda bağlantı kurmaya çoğu zaman yeter, ve tek bir yavaş arayüz
 * yüzünden yayını hiç açmamak, biraz geç açmaktan kötüdür.
 */
function waitForIceGathering(pc: RTCPeerConnection, timeoutMs: number): Promise<void> {
  if (pc.iceGatheringState === 'complete') return Promise.resolve();

  return new Promise<void>(resolve => {
    const finish = () => {
      clearTimeout(timer);
      pc.removeEventListener('icegatheringstatechange', onChange);
      resolve();
    };

    const onChange = () => {
      if (pc.iceGatheringState === 'complete') finish();
    };

    const timer = setTimeout(finish, timeoutMs);
    pc.addEventListener('icegatheringstatechange', onChange);
  });
}
