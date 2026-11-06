/**
 * Ayna: Scadex.Model/Dtos/Automation/Commands/AutomationUpdateDto.cs
 *
 * `GET /api/Automation/{id}/update` yanıtı da bu şekildedir — düzenleme formu
 * listeden (`AutomationDetailDto`) DEĞİL o uçtan doldurulur.
 *
 * `companyId` BİLEREK yok: `AutomationUpdateDto` bu alanı taşımıyor, otomasyon firma
 * değiştiremez.
 */
export interface AutomationUpdateRequest {
  id: string;
  name: string;
  latitude: number | null;
  longitude: number | null;
  locationDescription: string | null;
  /** Otomasyon `IActivatableEntity` — silme yok, pasife alma var ve geri alınabilir. */
  isActive: boolean;
  /**
   * Okumada `deviceStatusId === Maintenance`. Açıkken otomasyon durumu yapışkan "Bakımda"dır; cihazlardan hesaplanmaz.
   * Sunucu bunu ayrı bir kolonda değil `Automation.DeviceStatusId`'de tutar (`IAutomationStatusService.SetMaintenanceAsync`).
   */
  isUnderMaintenance: boolean;
}
