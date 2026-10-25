using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Dtos.Diagram.Commands.Items;
using Scadex.Model.Entities;
using DeviceType = Scadex.Model.Enums.EntityEnums.DeviceType;

namespace Scadex.Business.Concrete;

// DiagramService kaydetme boru hattinin CIHAZ EK TABLOLARI dilimi (2026-10-22): yukleme -> dogrulama -> uygulama.
// Genel gerekce, akis haritasi ve SaveContext icin DiagramService.SaveContext.cs'e bakin.
//
// Tipe ozgu alanlar Device'ta degil 1-1 ek tablolardadir: Camera, ControlCard, PcDevice (ortak birincil anahtar,
// DeviceId). Hangi ek tablonun dolacagini SABLONUN DeviceType'i belirler; taslak ile tip arasindaki tutarliligi DB
// degil bu dosya garanti eder. Ek satirlar cihaz govdesiyle ayni gonderide, ayni iki SaveChanges'te yazilir.
public partial class DiagramService
{
    // ==================== YUKLEME ====================

    /// <summary>
    /// Gonderilen taslaklarin sablon -> <c>DeviceTypeId</c> haritasi. Pasif sablonlar DAHIL: mevcut bir cihazin
    /// sablonu sonradan emekliye ayrilmis olabilir, cihaz yine de guncellenebilmeli. Taslaktaki
    /// <c>ComponentTemplateId</c> yeterlidir: mevcut cihazin sablonu degistirilemez (<see cref="ValidateDevices"/>).
    /// </summary>
    private async Task<Dictionary<Guid, int>> LoadTemplateDeviceTypesAsync(DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        var templateIds = request.Devices.Upserted.Select(d => d.ComponentTemplateId).Distinct().ToList();
        if (templateIds.Count == 0) return [];

        var rows = await _unitOfWork.ComponentTemplates.GetAllAsync(
            select: t => new { t.Id, t.DeviceTypeId },
            where: t => templateIds.Contains(t.Id),
            cancellationToken: cancellationToken) ?? [];

        return rows.ToDictionary(r => r.Id, r => r.DeviceTypeId);
    }

    /// <summary>
    /// Gonderilen kontrol karti MAC adreslerinin SAHIPLERI — <c>IX_ControlCard_MacAddress</c>.
    /// <b>Kabinle daraltilmaz:</b> index SISTEM GENELIDIR (ingest kabini bu adresten cozer), carpisma baska kabindeki
    /// kartla da olabilir; sorgu GONDERILEN ADRESLERLE daraltilir. Gonderide adres yoksa sorgu atilmaz.
    /// Pasif cihaz filtresi gerekmez: cihaz silinirken MAC'i null'a cekilir (<see cref="ApplyDeviceExtensionDeletions"/>).
    /// </summary>
    private async Task<Dictionary<Guid, string>> LoadControlCardMacAddressesAsync(DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        var submitted = SubmittedMacAddresses(request.Devices.Upserted.Select(d => d.ControlCard?.MacAddress));
        if (submitted.Count == 0) return [];

        var rows = await _unitOfWork.ControlCards.GetAllAsync(
            select: c => new ExtensionValueRow(c.DeviceId, c.MacAddress!),
            where: c => c.MacAddress != null && submitted.Contains(c.MacAddress),
            cancellationToken: cancellationToken) ?? [];

        return rows.ToDictionary(r => r.DeviceId, r => r.Value);
    }

    /// <summary> <see cref="LoadControlCardMacAddressesAsync"/>'in PC karsiligi — <c>IX_PcDevice_MacAddress</c>, sistem geneli. </summary>
    private async Task<Dictionary<Guid, string>> LoadPcMacAddressesAsync(DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        var submitted = SubmittedMacAddresses(request.Devices.Upserted.Select(d => d.Pc?.MacAddress));
        if (submitted.Count == 0) return [];

        var rows = await _unitOfWork.PcDevices.GetAllAsync(
            select: p => new ExtensionValueRow(p.DeviceId, p.MacAddress!),
            where: p => p.MacAddress != null && submitted.Contains(p.MacAddress),
            cancellationToken: cancellationToken) ?? [];

        return rows.ToDictionary(r => r.DeviceId, r => r.Value);
    }

    /// <summary>
    /// Kabindeki aktif kameralarin IP adresleri — kamera IP'si KABIN ICINDE tekildir (eski <c>Camera</c> tablosundaki
    /// unique index'in yerine; CabinetId ve IsActive artik Device'ta oldugu icin DB'de ifade edilemiyor).
    /// Gonderide kamera taslagi yoksa sorgu atilmaz.
    /// </summary>
    private async Task<Dictionary<Guid, string>> LoadCameraIpAddressesAsync(Guid cabinetId, DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        if (!request.Devices.Upserted.Any(d => d.Camera != null)) return [];

        var rows = await _unitOfWork.Devices.GetAllAsync(
            select: d => new ExtensionValueRow(d.Id, d.IpAddress!),
            where: d => d.CabinetId == cabinetId && d.IsActive && d.Camera != null && d.IpAddress != null,
            cancellationToken: cancellationToken) ?? [];

        return rows.ToDictionary(r => r.DeviceId, r => r.Value);
    }

    private static List<string> SubmittedMacAddresses(IEnumerable<string?> macAddresses) =>
        macAddresses
            .Where(mac => !string.IsNullOrWhiteSpace(mac))
            .Select(mac => mac!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    // ==================== REFERANS DOGRULAMA ====================

    /// <summary>
    /// Ek nesne sablon tipiyle eslesmeli: kamera sablonu <c>Camera</c>, kontrol modulu <c>ControlCard</c>, PC <c>Pc</c>
    /// ister; diger tipler hicbirini gondermez. Eksik ya da yanlis tipe giden nesne 400'dur — sessizce yok saymak,
    /// istemcinin yanlis panelden yazdigi kimlik bilgisini kaybetmek olurdu.
    ///
    /// Hedefi erisilemeyen (baska kabin / silinmis) ya da sablonu cozulemeyen taslak burada ATLANIR: o hatalari
    /// <see cref="ValidateDevices"/> zaten raporlar, ikinci bir hata yalnizca kafa karistirir.
    /// </summary>
    private static void ValidateDeviceExtensions(DiagramSaveRequest request, SaveContext context, Dictionary<string, List<string>> errors)
    {
        for (int i = 0; i < request.Devices.Upserted.Count; i++)
        {
            var draft = request.Devices.Upserted[i];
            var key = $"Devices.Upserted[{i}]";

            if (context.Devices.Foreign.Contains(draft.Id) || context.Devices.Removed.Contains(draft.Id)) continue;
            if (!context.TemplateDeviceTypes.TryGetValue(draft.ComponentTemplateId, out var deviceTypeId)) continue;

            CheckExtension(draft.Camera != null, deviceTypeId == (int)DeviceType.Camera, $"{key}.Camera", "Kamera bilgisi", errors);
            CheckExtension(draft.ControlCard != null, deviceTypeId == (int)DeviceType.ControlModule, $"{key}.ControlCard", "Kontrol kartı bilgisi", errors);
            CheckExtension(draft.Pc != null, deviceTypeId == (int)DeviceType.Pc, $"{key}.Pc", "Bilgisayar bilgisi", errors);

            // Kamera RTSP adresini IP'den kurar; IP'siz kamera izlenemez ve MediaMTX yolu uretilemez.
            if (deviceTypeId == (int)DeviceType.Camera && string.IsNullOrWhiteSpace(draft.IpAddress))
                AddError(errors, $"{key}.IpAddress", "Kamera için IP adresi zorunlu");
        }

        ValidateUniqueValues(request, context, d => d.Camera != null ? d.IpAddress : null, context.CameraIpAddresses,
            i => $"Devices.Upserted[{i}].IpAddress", "Bu kabinde aynı IP adresine sahip bir kamera zaten var", errors);

        // MAC tekilligi TIP BASINADIR ve SISTEM GENELIDIR: ingest yalnizca ControlCard'a, PcHub yalnizca PcDevice'a bakar.
        // Mesaj "bu kabinde" demez — carpisan cihaz baska bir kabinde olabilir.
        ValidateUniqueValues(request, context, d => d.ControlCard?.MacAddress, context.ControlCardMacAddresses,
            i => $"Devices.Upserted[{i}].ControlCard.MacAddress", "Bu MAC adresi başka bir kontrol kartına kayıtlı", errors);
        ValidateUniqueValues(request, context, d => d.Pc?.MacAddress, context.PcMacAddresses,
            i => $"Devices.Upserted[{i}].Pc.MacAddress", "Bu MAC adresi başka bir bilgisayara kayıtlı", errors);
    }

    private static void CheckExtension(bool sent, bool expected, string key, string label, Dictionary<string, List<string>> errors)
    {
        if (sent && !expected) AddError(errors, key, $"{label} bu cihazın şablonuna ait değil; gönderilemez");
        else if (!sent && expected) AddError(errors, key, $"{label} zorunlu");
    }

    /// <summary>
    /// Bir degerin (MAC / kamera IP'si) mevcut sahipler ve ayni gonderideki diger taslaklar arasinda tekil olmasi.
    /// Silinen cihaz ve yazilan cihazin ESKI degeri mevcut sayilmaz (silinenin MAC'i null'a cekilir, yazilanin yeni
    /// degeri asagida eklenir). <c>OrdinalIgnoreCase</c>: SQL Server'in varsayilan collation'i buyuk/kucuk harf duyarsiz,
    /// index'te "aa:bb.." ile "AA:BB.." ayni satirdir.
    /// </summary>
    private static void ValidateUniqueValues(
        DiagramSaveRequest request,
        SaveContext context,
        Func<DeviceDraft, string?> valueOf,
        Dictionary<Guid, string> existingOwners,
        Func<int, string> errorKeyOf,
        string message,
        Dictionary<string, List<string>> errors)
    {
        var upsertedIds = request.Devices.Upserted.Select(d => d.Id).ToHashSet();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (deviceId, value) in existingOwners)
        {
            if (context.DeletedDeviceIds.Contains(deviceId) || upsertedIds.Contains(deviceId)) continue;
            taken.Add(value);
        }

        for (int i = 0; i < request.Devices.Upserted.Count; i++)
        {
            var value = valueOf(request.Devices.Upserted[i]);
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (!taken.Add(value))
                AddError(errors, errorKeyOf(i), message);
        }
    }

    // ==================== UYGULAMA ====================

    /// <summary>
    /// Silinen cihazlarin ek tablolarindaki MAC'ler <c>null</c>'a cekilir. Filtreli MAC index'i <c>Device.IsActive</c>'e
    /// bakamaz; cekilmezse pasif cihaz adresi sonsuza dek isgal ederdi. Ilk SaveChanges'te (silmeler) akar, bu yuzden ayni
    /// gonderide eklenen yeni cihaz ayni adresi alabilir. Eski deger <c>Archive</c>'da kalir.
    /// Ek satir <see cref="LoadDevicesAsync"/>'te cihazla birlikte takipli yuklenir.
    /// </summary>
    private static void ApplyDeviceExtensionDeletions(SaveContext context)
    {
        foreach (var id in context.DeletedDeviceIds)
        {
            var device = context.Devices.Live[id];
            if (device.ControlCard != null) device.ControlCard.MacAddress = null;
            if (device.PcDevice != null) device.PcDevice.MacAddress = null;
        }
    }

    /// <summary>
    /// Cihazin ek satirini yazar: yoksa olusturur, varsa takipli satiri gunceller (EF degisen kolonu kendisi bulur).
    /// Hangi nesnenin gelecegi <see cref="ValidateDeviceExtensions"/>'ta tiple eslestirildi; burada taslak izlenir.
    /// Kamerada bos <c>MonitoringPort</c> markanin RTSP portuyla doldurulur: kamerada yoklanacak hizmet RTSP'dir,
    /// ICMP ping'e dusmesin (eski <c>CameraService</c> davranisi).
    /// </summary>
    private void WriteDeviceExtensions(Device device, DeviceDraft draft)
    {
        if (draft.Camera is { } cameraDraft)
        {
            var camera = device.Camera;
            if (camera == null)
            {
                camera = new Camera { DeviceId = device.Id };
                device.Camera = camera;
                _unitOfWork.Cameras.Add(camera);
            }
            camera.Brand = cameraDraft.Brand;
            camera.Model = cameraDraft.Model;
            camera.Description = cameraDraft.Description;
            camera.Username = cameraDraft.Username;
            camera.Password = ApplyPassword(camera.Password, cameraDraft.Password);

            device.MonitoringPort ??= _cameraProtocolProfileResolver.Resolve(cameraDraft.Brand).RtspPort;
        }

        if (draft.ControlCard is { } cardDraft)
        {
            var card = device.ControlCard;
            if (card == null)
            {
                card = new ControlCard { DeviceId = device.Id };
                device.ControlCard = card;
                _unitOfWork.ControlCards.Add(card);
            }
            card.MacAddress = cardDraft.MacAddress;
            card.Username = cardDraft.Username;
            card.Password = ApplyPassword(card.Password, cardDraft.Password);
        }

        if (draft.Pc is { } pcDraft)
        {
            var pc = device.PcDevice;
            if (pc == null)
            {
                pc = new PcDevice { DeviceId = device.Id };
                device.PcDevice = pc;
                _unitOfWork.PcDevices.Add(pc);
            }
            pc.MacAddress = pcDraft.MacAddress;
        }
    }

    /// <summary> Okuma DTO'su parolayi dondurmez: <c>null</c> "dokunma", <c>""</c> "temizle", deger "yaz" demektir. </summary>
    private static string? ApplyPassword(string? current, string? submitted) => submitted switch
    {
        null => current,
        "" => null,
        _ => submitted
    };

    // ==================== YARDIMCI TIPLER ====================

    /// <summary> Ek tablodaki tekil bir degerin (MAC / kamera IP'si) sahibi. </summary>
    private sealed record ExtensionValueRow(Guid DeviceId, string Value);
}
