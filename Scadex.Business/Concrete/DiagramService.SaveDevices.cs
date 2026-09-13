using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Dtos.Diagram.Commands.Items;
using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

// DiagramService kaydetme boru hattinin CIHAZ ailesi: yukleme -> dogrulama -> uygulama.
// Genel gerekce, akis haritasi ve SaveContext icin DiagramService.SaveContext.cs'e bakin.
// NOT: cihaz silmesinden dogan KABLOLAR kablo ailesindedir
// (DiagramService.SaveConnections.cs, LoadCascadeConnectionsAsync); burada yalnizca
// cihazin kendi pinlerinin cascade'i vardir.
// Pin ve kanal semasinin uretimi/dogrulamasi ayri dilimdedir:
// DiagramService.SaveDevicePins.cs.
public partial class DiagramService
{
    // ==================== YUKLEME ====================

    /// <summary>Gonderide gecen tum cihaz Id'leri; TAKIPLI, kabin filtresi YOK.</summary>
    private async Task<EntityLookup<Device>> LoadDevicesAsync(Guid cabinetId, DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        var ids = request.Devices.Upserted.Select(d => d.Id).Concat(request.Devices.Deleted).Distinct().ToList();
        if (ids.Count == 0) return new();

        var rows = await _unitOfWork.Devices.GetAllAsync(
            where: d => ids.Contains(d.Id),
            cancellationToken: cancellationToken) ?? [];

        // Device IActivatableEntity: "kaldirilmis" olmak IsActive = false demek.
        return Classify(rows, cabinetId, d => d.Id, d => d.CabinetId, d => !d.IsActive);
    }

    /// <summary>
    /// Yeni cihazlarin sablonlari. Palet gibi burasi da bir SECIM kaynagi:
    /// pasif sablon yeni cihaz uretmemeli.
    /// </summary>
    private async Task<HashSet<Guid>> LoadActiveTemplateIdsAsync(List<Guid> templateIds, CancellationToken cancellationToken)
    {
        if (templateIds.Count == 0) return [];

        var rows = await _unitOfWork.ComponentTemplates.GetAllAsync(
            select: t => t.Id,
            where: t => templateIds.Contains(t.Id) && t.IsActive,
            cancellationToken: cancellationToken) ?? [];

        return rows.ToHashSet();
    }

    /// <summary>
    /// Silinen cihazlarla birlikte kalkacak pinler. Pin yalnizca cihaziyla
    /// birlikte silinir — cihaz uzerinde tekil pin silme yok.
    /// </summary>
    private async Task<List<Pin>> LoadPinsOfDeletedDevicesAsync(HashSet<Guid> deletedDeviceIds, CancellationToken cancellationToken)
    {
        if (deletedDeviceIds.Count == 0) return [];

        var ids = deletedDeviceIds.ToList();
        var rows = await _unitOfWork.Pins.GetAllAsync(
            where: p => ids.Contains(p.DeviceId),
            cancellationToken: cancellationToken) ?? [];

        return rows.ToList();
    }

    /// <summary>
    /// Kabindeki aktif cihazlarin dis kodlari —
    /// <c>IX_Device_CabinetId_ExternalCode</c> (unique, WHERE ExternalCode IS NOT NULL
    /// AND IsActive = 1). Gonderide hic kod yoksa sorgu ATILMAZ.
    /// </summary>
    private async Task<Dictionary<Guid, string>> LoadDeviceExternalCodesAsync(Guid cabinetId, DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        if (!request.Devices.Upserted.Any(d => !string.IsNullOrWhiteSpace(d.ExternalCode))) return [];

        var rows = await _unitOfWork.Devices.GetAllAsync(
            select: d => new DeviceCodeRow(d.Id, d.ExternalCode!),
            where: d => d.CabinetId == cabinetId && d.IsActive && d.ExternalCode != null,
            cancellationToken: cancellationToken) ?? [];

        return rows.ToDictionary(r => r.Id, r => r.ExternalCode);
    }

    /// <summary>
    /// Gonderilen MAC adreslerinin SAHIPLERI — <c>IX_Device_MacAddress</c>
    /// (unique, WHERE MacAddress IS NOT NULL AND IsActive = 1).
    ///
    /// <b>Neden kabinle daraltilmiyor.</b> Dis kod index'i kabin bazlidir, MAC index'i
    /// GLOBALDIR: bir fiziksel kartin tek MAC'i vardir ve ingest kabini bu adresten cozer.
    /// Dolayisiyla carpisma baska bir kabindeki cihazla da olabilir; sorgu kabinle degil,
    /// GONDERILEN ADRESLERLE daraltilir. Gonderide hic adres yoksa sorgu ATILMAZ.
    /// </summary>
    private async Task<Dictionary<Guid, string>> LoadDeviceMacAddressesAsync(DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        var submitted = request.Devices.Upserted
            .Select(d => d.MacAddress)
            .Where(mac => !string.IsNullOrWhiteSpace(mac))
            .Select(mac => mac!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (submitted.Count == 0) return [];

        var rows = await _unitOfWork.Devices.GetAllAsync(
            select: d => new DeviceMacRow(d.Id, d.MacAddress!),
            where: d => d.IsActive && d.MacAddress != null && submitted.Contains(d.MacAddress),
            cancellationToken: cancellationToken) ?? [];

        return rows.ToDictionary(r => r.Id, r => r.MacAddress);
    }

    // ==================== REFERANS DOGRULAMA ====================

    /// <summary>
    /// Cihaz taslaklari: hedef satir erisilebilir mi, yeniyse sablonu gecerli mi,
    /// mevcutsa sablonu ayni mi, ve gonderilen pin/kanal kimlikleri tutarli mi.
    /// </summary>
    private static void ValidateDevices(DiagramSaveRequest request, SaveContext context, Dictionary<string, List<string>> errors)
    {
        // Kimlik tekilligi GONDERININ TAMAMINDA aranir: iki farkli cihazin ayni pin
        // Id'sini paylasmasi da bir carpismadir.
        var seenPinIds = new HashSet<Guid>();
        var seenChannelIds = new HashSet<Guid>();
        // Ayni gonderide iki YENI cihaz da ayni adresi isteyebilir; DB'de henuz
        // ikisi de yok oldugu icin bunu CabinetChannelAddresses yakalayamaz.
        var seenChannelAddresses = new HashSet<(PinDirection, int)>();

        for (int i = 0; i < request.Devices.Upserted.Count; i++)
        {
            var draft = request.Devices.Upserted[i];
            var key = $"Devices.Upserted[{i}]";

            if (ReportUnreachable(context.Devices, draft.Id, errors, $"{key}.Id", "Cihaz")) continue;

            if (context.Devices.Live.TryGetValue(draft.Id, out var device))
            {
                // Sablon degistirmek pin semasini degistirmek demek; bu bir
                // guncelleme degil, cihazi bastan yaratmaktir.
                if (device.ComponentTemplateId != draft.ComponentTemplateId)
                    AddError(errors, $"{key}.ComponentTemplateId", "Mevcut bir cihazin sablonu degistirilemez; silip yeniden ekleyin");

                // Pin ve kanal SALT-OLUSTURMA. Mevcut cihazin pinleri zaten var;
                // ikinci kez gonderilen bir kimlik kumesi ya cop ya da bir
                // istemci hatasidir — sessizce yok saymak ikincisini gizlerdi.
                if (draft.Pins.Count > 0)
                    AddError(errors, $"{key}.Pins", "Mevcut bir cihaza pin gonderilemez; pinleri olusturulurken uretilir");
                if (draft.IoChannels.Count > 0)
                    AddError(errors, $"{key}.IoChannels", "Mevcut bir cihaza kanal gonderilemez; kanallari olusturulurken uretilir");

                continue;
            }

            if (!context.ActiveTemplateIds.Contains(draft.ComponentTemplateId))
            {
                // Sablon cozulemediyse pin semasi da bilinmiyor: asagidaki kume
                // karsilastirmasi yalnizca kafa karistirici ikinci bir hata uretirdi.
                AddError(errors, $"{key}.ComponentTemplateId", "Sablon bulunamadi veya pasif durumda");
                continue;
            }

            ValidateDevicePinIdentities(draft, key, context, seenPinIds, seenChannelIds, seenChannelAddresses, errors);
        }
    }

    /// <summary>
    /// Dis kodlarin kabin icinde benzersizligi — <c>IX_Device_CabinetId_ExternalCode</c>.
    /// Kod SCADA tarafindaki kimliktir; ayni kodun iki cihaza dusmesi telemetriyi
    /// yanlis cihaza yazardi, dolayisiyla index yalnizca bir performans detayi degil.
    /// </summary>
    private static void ValidateDeviceExternalCodes(DiagramSaveRequest request, SaveContext context, Dictionary<string, List<string>> errors)
    {
        var upsertedIds = request.Devices.Upserted.Select(d => d.Id).ToHashSet();
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (deviceId, code) in context.DeviceExternalCodes)
        {
            // Silinen cihaz index filtresinden duser, yazilan cihazin YENI degeri
            // asagida eklenecek — ikisi de mevcut kod sayilmaz.
            if (context.DeletedDeviceIds.Contains(deviceId) || upsertedIds.Contains(deviceId)) continue;
            codes.Add(code);
        }

        for (int i = 0; i < request.Devices.Upserted.Count; i++)
        {
            var code = request.Devices.Upserted[i].ExternalCode;
            if (string.IsNullOrWhiteSpace(code)) continue;
            if (!codes.Add(code))
                AddError(errors, $"Devices.Upserted[{i}].ExternalCode", "Bu kabinde ayni dis koda sahip baska bir cihaz var");
        }
    }

    /// <summary>
    /// MAC adreslerinin benzersizligi — <c>IX_Device_MacAddress</c>. Index KABIN BAZLI
    /// DEGIL GLOBALDIR: bir fiziksel kartin tek MAC'i vardir ve ingest kabini bu adresten
    /// cozer; ayni adres iki cihaza dusseydi telemetri yanlis kabine yazilirdi. Bu yuzden
    /// hata mesaji "bu kabinde" demez — carpisan cihaz baska bir kabinde olabilir.
    /// </summary>
    private static void ValidateDeviceMacAddresses(DiagramSaveRequest request, SaveContext context, Dictionary<string, List<string>> errors)
    {
        var upsertedIds = request.Devices.Upserted.Select(d => d.Id).ToHashSet();
        // OrdinalIgnoreCase: SQL Server'in varsayilan collation'i buyuk/kucuk harf duyarsiz,
        // yani "aa:bb.." ile "AA:BB.." index'te AYNI satirdir. On kontrol de oyle saymali.
        var macAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (deviceId, macAddress) in context.DeviceMacAddresses)
        {
            // Silinen cihaz index filtresinden duser, yazilan cihazin YENI degeri
            // asagida eklenecek — ikisi de mevcut adres sayilmaz.
            if (context.DeletedDeviceIds.Contains(deviceId) || upsertedIds.Contains(deviceId)) continue;
            macAddresses.Add(macAddress);
        }

        for (int i = 0; i < request.Devices.Upserted.Count; i++)
        {
            var macAddress = request.Devices.Upserted[i].MacAddress;
            if (string.IsNullOrWhiteSpace(macAddress)) continue;
            if (!macAddresses.Add(macAddress))
                AddError(errors, $"Devices.Upserted[{i}].MacAddress", "Bu MAC adresi baska bir cihaza kayitli");
        }
    }

    // ==================== UYGULAMA ====================

    /// <summary>
    /// Cihazi silindigi icin birlikte kalkan pinler.
    /// <c>Pin</c> <c>ISoftDeletableEntity</c>'dir: interceptor <c>Remove</c>'u
    /// <c>IsDeleted = true</c>'ya cevirir, satir fiziksel olarak silinmez.
    ///
    /// Silme sirasindaki yeri icin <c>ApplyDeletions</c>'a bakin
    /// (DiagramService.SaveContext.cs).
    /// </summary>
    private void ApplyPinDeletions(SaveContext context)
    {
        if (context.PinsOfDeletedDevices.Count > 0)
            _unitOfWork.Pins.Delete(context.PinsOfDeletedDevices);
    }

    /// <summary>
    /// Cihaz kaldirma. <c>Remove()</c> CAGRILMAZ: <c>Device</c> <c>IActivatableEntity</c>
    /// oldugu icin <c>EntityLifecycleInterceptor</c> hard delete'te exception atar ve
    /// 500 doner — kaldirmak <c>IsActive = false</c> demektir.
    ///
    /// Indeksleyicinin guvenli oldugu garanti: <c>DeletedDeviceIds</c>,
    /// <c>LoadSaveContextAsync</c>'te <c>devices.Live.ContainsKey</c> ile suzuluyor
    /// (DiagramService.SaveContext.cs), dolayisiyla her Id <c>Live</c>'da var.
    /// </summary>
    private void ApplyDeviceDeletions(SaveContext context)
    {
        foreach (var id in context.DeletedDeviceIds)
            context.Devices.Live[id].IsActive = false;
    }

    /// <summary>
    /// Cihaz yazmalari. Her taslak icin tek soru: satir <c>Live</c> mi (guncelle)
    /// yoksa yok mu (olustur). Yeni cihazin pinleri ve kanallari
    /// <see cref="InstantiateTemplatePins"/> ile sablondan uretilir
    /// (DiagramService.SaveDevicePins.cs).
    /// </summary>
    private void ApplyDeviceUpserts(Guid cabinetId, DiagramSaveRequest request, SaveContext context)
    {
        foreach (var draft in request.Devices.Upserted)
        {
            if (context.Devices.Live.TryGetValue(draft.Id, out var device))
            {
                WriteDevice(device, draft);
                continue;
            }

            device = new Device
            {
                Id = draft.Id,
                CabinetId = cabinetId,
                ComponentTemplateId = draft.ComponentTemplateId,
                // B5: taslakta IsActive yok, servis ACIKCA true yazar. Yazilmazsa
                // kayit pasif dogar ve diyagram okumasindan dusmus olur.
                IsActive = true
            };
            WriteDevice(device, draft);
            _unitOfWork.Devices.Add(device);

            // Pini olmayan sablon: cihaz pinsiz dogar. Bu bir SECIM degil, sablonun
            // sonucu — pin yazarligi sablon ekranina aittir.
            if (context.TemplatePins.TryGetValue(draft.ComponentTemplateId, out var templatePins))
                InstantiateTemplatePins(device, templatePins, draft);
        }
    }

    /// <summary>
    /// Mevcut satirlarda <c>Update()</c> CAGRILMAZ: varliklar takipli okundugu icin
    /// EF degisen alanlari kendisi tespit eder ve yalnizca onlari UPDATE'e koyar.
    /// <c>Update()</c> cagirmak butun kolonlari degismis isaretler ve telemetri
    /// alanlarini eski degerleriyle geri yazma riski dogurur.
    ///
    /// <c>DeviceStatusId</c> / <c>LastSeen</c> burada DOKUNULMAZ — taslakta zaten yoklar,
    /// telemetriyle yazilirlar. <c>MacAddress</c> / <c>IpAddress</c> ise taslakta VARDIR:
    /// MAC, SCADA ingest'inin kabini cozdugu adrestir ve operatorun girebilmesi gerekir.
    /// </summary>
    private static void WriteDevice(Device device, DeviceDraft draft)
    {
        device.Name = draft.Name;
        device.CoordinateX = draft.CoordinateX;
        device.CoordinateY = draft.CoordinateY;
        device.Width = draft.Width;
        device.Height = draft.Height;
        device.Rotation = draft.Rotation;
        device.ZIndex = draft.ZIndex;
        device.IsLocked = draft.IsLocked;
        device.IsVisible = draft.IsVisible;
        device.ExternalCode = draft.ExternalCode;
        device.MacAddress = draft.MacAddress;
        device.IpAddress = draft.IpAddress;
    }

    // ==================== YARDIMCI TIPLER ====================

    private sealed record DeviceCodeRow(Guid Id, string ExternalCode);

    private sealed record DeviceMacRow(Guid Id, string MacAddress);
}
