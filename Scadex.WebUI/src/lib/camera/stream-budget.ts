/**
 * Eş zamanlı canlı akım bütçesi.
 *
 * **Neden var.** Her kutucuk bir `RTCPeerConnection` ve — geçidin arkasında —
 * kameraya açılmış bir RTSP oturumu demek. 32 kameralı bir kabinde sayfa
 * açılışında hepsini birden başlatmak, tarayıcıyı da MediaMTX'i de aynı anda
 * dize getirir; kabinlerin ağırlıklı olarak GSM ile bağlandığı bir kurulumda
 * hattı da doldurur.
 *
 * Bütçe modül düzeyinde tutuluyor (React state DEĞİL): sayı, render'dan
 * bağımsız bir kaynak sınırıdır ve state'e konsaydı her alım/bırakım tüm
 * kutucukları yeniden render ederdi.
 */

/**
 * Aynı anda açık tutulabilecek akım sayısı.
 *
 * Sunucu ayarı DEĞİL, bilinçli olarak: sınırı belirleyen şey tarayıcının ve
 * kullanıcının ağının kapasitesi, sunucununki değil. Ayrıca sunucuda dursaydı
 * onu servis eden bir uç yazmak gerekirdi ve o uç yalnızca bu sabiti taşırdı.
 */
const MAX_CONCURRENT_STREAMS = 12;

let activeCount = 0;
const waiting: Array<() => void> = [];

export type ReleaseSlot = () => void;

/**
 * Bir akım yeri ayırır. Bütçe doluysa sıradaki yer boşalana kadar bekler.
 *
 * @returns Yeri geri veren fonksiyon. **Her `acquire` için tam olarak bir kez**
 *   çağrılmalı; çağrılmazsa bütçe sızar ve grid bir daha hiç bağlanmaz.
 */
export function acquireStreamSlot(signal?: AbortSignal): Promise<ReleaseSlot> {
  if (signal?.aborted) return Promise.reject(new DOMException('Aborted', 'AbortError'));

  if (activeCount < MAX_CONCURRENT_STREAMS) {
    activeCount += 1;
    return Promise.resolve(createRelease());
  }

  return new Promise<ReleaseSlot>((resolve, reject) => {
    const grant = () => {
      signal?.removeEventListener('abort', onAbort);
      activeCount += 1;
      resolve(createRelease());
    };

    const onAbort = () => {
      // Sıradan çıkar: kutucuk artık görünmüyor ya da bileşen söküldü.
      const index = waiting.indexOf(grant);
      if (index >= 0) waiting.splice(index, 1);
      reject(new DOMException('Aborted', 'AbortError'));
    };

    signal?.addEventListener('abort', onAbort, { once: true });
    waiting.push(grant);
  });
}

/** Yer sayısı ve bekleyenler — kutucuğun "sırada" mesajını göstermesi için. */
export function streamBudgetSnapshot() {
  return { active: activeCount, max: MAX_CONCURRENT_STREAMS, waiting: waiting.length };
}

function createRelease(): ReleaseSlot {
  // Tek atışlık: aynı release iki kez çağrılırsa sayaç eksiye düşer ve
  // bütçe fiilen ortadan kalkardı.
  let released = false;

  return () => {
    if (released) return;
    released = true;

    activeCount -= 1;
    waiting.shift()?.();
  };
}
