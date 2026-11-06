/**
 * Oynayan `<video>`'dan o anki kareyi yakalar.
 *
 * **Neden sunucuya gitmiyoruz.** Operatör görüntüyü zaten izliyor; aynı kareyi
 * kameradan bir kez daha istemek, hem kameraya hem GSM hattına gereksiz yük
 * bindirir ve saniyeler sürer. Canvas'tan alınan kare anında hazırdır ve hiçbir
 * istek üretmez.
 *
 * **Bunun DELİL olmadığına dikkat.** Kare, o an izlenen akımın çözünürlüğünde
 * (grid'de tali akım) ve istemcinin ürettiği baytlardır. Delil çekimi
 * `POST /api/Camera/{id}/capture` ile sunucu tarafında, ana akımdan yapılır ve
 * `CameraCapture` satırı bırakır — ikisi ayrı amaçlara hizmet eder.
 */

/** Tarayıcının henüz kare üretmediği hâli (`HAVE_NOTHING` / `HAVE_METADATA`). */
const HAVE_CURRENT_DATA = 2;

export interface GrabbedFrame {
  blob: Blob;
  /** `URL.createObjectURL` sonucu. Çağıran **revoke etmekle yükümlü**. */
  objectUrl: string;
  width: number;
  height: number;
}

export async function grabVideoFrame(video: HTMLVideoElement): Promise<GrabbedFrame> {
  // `videoWidth` yayın gelene kadar 0'dır; kontrol edilmezse 0x0 bir canvas'a
  // çizip boş bir görüntü üretirdik.
  if (video.readyState < HAVE_CURRENT_DATA || video.videoWidth === 0 || video.videoHeight === 0) {
    throw new Error('Görüntü henüz hazır değil.');
  }

  const canvas = document.createElement('canvas');
  canvas.width = video.videoWidth;
  canvas.height = video.videoHeight;

  const context = canvas.getContext('2d');
  if (!context) throw new Error('Tarayıcı canvas desteklemiyor.');

  context.drawImage(video, 0, 0, canvas.width, canvas.height);

  const blob = await new Promise<Blob | null>(resolve => {
    // 0.92 tarayıcının JPEG varsayılanı; bir izleme karesi için fazlasıyla yeterli.
    canvas.toBlob(resolve, 'image/jpeg', 0.92);
  });

  if (!blob) throw new Error('Kare oluşturulamadı.');

  return {
    blob,
    objectUrl: URL.createObjectURL(blob),
    width: canvas.width,
    height: canvas.height
  };
}
