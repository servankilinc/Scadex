/**
 * Ayna: Scadex.Model/Dtos/Camera/Commands/CameraProbeResultDto.cs
 * Sözleşme: docs/api-contract/11-camera.md
 *
 * Bu gövdeyi gönderen yoklama servisi HENÜZ YAZILMADI (kullanıcı kendisi
 * yazacak). Ayna, o servis geldiğinde arayüz tarafında da tiplenmiş bir
 * karşılık bulunsun diye burada duruyor.
 */
export interface CameraProbeResultRequest {
  reachable: boolean;
  /** Bilgi amaçlı — sunucu SAKLAMAZ (her yoklamada değişen bir sayı yazmak,
   *  "durum değişmediyse yazma yok" kuralını anlamsız kılardı). */
  rttMs?: number | null;
  error?: string | null;
}
