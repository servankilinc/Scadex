/**
 * Ayna: Scadex.Model/Dtos/Cabinet/Commands/CabinetUpdateDto.cs
 *
 * `GET /api/Cabinet/{id}/update` yanıtı da bu şekildedir — düzenleme formu
 * listeden DEĞİL o uçtan doldurulur, çünkü `CabinetDetailDto` SCADA alanlarını
 * taşımıyor.
 *
 * `companyId` BİLEREK yok: `CabinetUpdateDto` bu alanı taşımıyor, kabin firma
 * değiştiremez.
 */
export interface CabinetUpdateRequest {
  id: string;
  name: string;
  latitude: number | null;
  longitude: number | null;
  locationDescription: string | null;
  gsmIp: string | null;
  networkIp: string | null;
  scadaBaseUrl: string | null;
  scadaCommandTimeoutMs: number;
  scadaIsEnabled: boolean;
  /** Kabin `IActivatableEntity` — silme yok, pasife alma var ve geri alınabilir. */
  isActive: boolean;
}
