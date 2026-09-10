using Scadex.Business.Utils;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Dtos.Diagram.Commands.Items;
using Scadex.Model.Dtos.Scada.Commands;
using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

/// <summary>
/// Toplu kaydetmenin ic adimlari: DB'den okuma, referans dogrulama ve uygulama.
/// Orkestrasyon <c>DiagramService.Save.cs</c>'teki <c>SaveAsync</c>'te.
///
/// <b>Model: upsert.</b> Guid'i istemci uretir, dolayisiyla "yeni mi mevcut mu"
/// sorusu istemcinin niyetinden degil, TEK bir yerden okunur: Id veritabaninda
/// var mi. Bu dosyadaki her sey o karar noktasinin etrafinda kurulu.
/// </summary>
public partial class DiagramService
{

    // ==================== YUKLEME ====================

    /// <summary>
    /// Kaydetme icin gereken her seyi DB'den TEK SEFERDE okur.
    ///
    /// Taslak basina sorgu atmak, 50 node'luk bir kaydetmede 50 gidis-donus ederdi;
    /// buradaki her sorgu bir AILEYI toplu ceker. Var olan varliklar TAKIPLI
    /// okunur — EF degisikligi kendisi yakalar, ayrica <c>Update()</c> cagirmaya
    /// gerek kalmaz.
    ///
    /// <b>Aile sorgulari kabine gore FILTRELENMEZ.</b> Baska kabine ait bir Id
    /// "bulunamadi" gorunup sessizce INSERT'e dusmemeli; bu birincil anahtar
    /// ihlaliyle 500 uretirdi. Onun yerine satir Id ile bulunur ve kabini
    /// <see cref="Classify"/> icinde karsilastirilir.
    /// </summary>
    private async Task<SaveContext> LoadSaveContextAsync(Guid cabinetId, DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        var devices = await LoadDevicesAsync(cabinetId, request, cancellationToken);
        var connections = await LoadConnectionsAsync(cabinetId, request, cancellationToken);
        var annotations = await LoadAnnotationsAsync(cabinetId, request, cancellationToken);

        // Silme yalnizca BU KABINDEKI CANLI satira uygulanir; gerisi atlanir.
        var deletedDeviceIds = request.Devices.Deleted.Where(devices.Live.ContainsKey).ToHashSet();

        // Sablon pinleri yalnizca YENI cihazlar icin gerekir: mevcut bir cihazin
        // pinleri zaten var ve sablonu degistirilemiyor.
        var newDevices = request.Devices.Upserted.Where(d => !devices.Live.ContainsKey(d.Id)).ToList();
        var newDeviceTemplateIds = newDevices.Select(d => d.ComponentTemplateId).Distinct().ToList();

        var referencedPinIds = request.Connections.Upserted
            .SelectMany(c => new[] { c.SourcePinId, c.TargetPinId })
            .Distinct().ToList();

        var templatePins = await LoadTemplatePinsAsync(newDeviceTemplateIds, cancellationToken);

        return new SaveContext
        {
            Devices = devices,
            Connections = connections,
            Annotations = annotations,
            DeletedDeviceIds = deletedDeviceIds,
            ActiveTemplateIds = await LoadActiveTemplateIdsAsync(newDeviceTemplateIds, cancellationToken),
            TemplatePins = templatePins,
            Pins = await LoadPinsAsync(cabinetId, referencedPinIds, cancellationToken),
            PinsOfDeletedDevices = await LoadPinsOfDeletedDevicesAsync(deletedDeviceIds, cancellationToken),
            CascadeConnections = await LoadCascadeConnectionsAsync(cabinetId, deletedDeviceIds, cancellationToken),
            PinPairCandidates = await LoadPinPairCandidatesAsync(cabinetId, referencedPinIds, cancellationToken),
            DeviceExternalCodes = await LoadDeviceExternalCodesAsync(cabinetId, request, cancellationToken),
            DeviceMacAddresses = await LoadDeviceMacAddressesAsync(request, cancellationToken),
            NewPins = BuildNewPinRefs(newDevices, templatePins),
            ClaimedPinIds = await LoadClaimedPinIdsAsync(newDevices, cancellationToken),
            ClaimedIoChannelIds = await LoadClaimedIoChannelIdsAsync(newDevices, cancellationToken),
            CabinetChannelAddresses = await LoadCabinetChannelAddressesAsync(cabinetId, newDevices, cancellationToken)
        };
    }

    /// <summary>
    /// Kabinde HALIHAZIRDA kullanilan kanal adresleri -> sahibi cihazin adi.
    ///
    /// <b>Neden kabin geneli.</b> Kabin BIR kontrol kartidir ve kartin adres
    /// uzayi duzdur: <c>IN1</c> kabinde tektir, cihazda degil
    /// (<c>IX_IoChannel_CabinetId_Direction_ChannelNumber</c>). Bu kontrol
    /// olmasaydi ayni sablonu ikinci kez birakmak, dogrulamadan gecip
    /// SaveChanges'te benzersiz indeks ihlaliyle 500 verirdi — kullaniciya
    /// hicbir sey anlatmayan bir hata.
    ///
    /// Cihaz ADI da okunuyor: "IN1 zaten kullaniliyor" tek basina operatore
    /// hangi kutuya bakacagini soylemez.
    ///
    /// Yalnizca YENI cihaz varken sorgulanir; mevcut cihazlarin kanallari zaten
    /// yerinde ve bu gonderide degismiyor. Silinen cihazlarin kanallari HARIC
    /// TUTULMAZ: kanallar cihazla birlikte kalkmiyor (IoChannel soft-delete ve
    /// diyagram kaydetme onlara dokunmuyor), dolayisiyla adres isgal edilmeye
    /// devam ediyor.
    /// </summary>
    private async Task<Dictionary<(PinDirection Direction, int ChannelNumber), string>> LoadCabinetChannelAddressesAsync(
        Guid cabinetId,
        List<DeviceDraft> newDevices,
        CancellationToken cancellationToken)
    {
        if (newDevices.Count == 0) return [];

        var rows = await _unitOfWork.IoChannels.GetAllAsync(
            select: c => new ChannelAddressRow(c.Direction, c.ChannelNumber, c.Device!.Name),
            where: c => c.CabinetId == cabinetId,
            cancellationToken: cancellationToken) ?? [];

        var map = new Dictionary<(PinDirection, int), string>();
        foreach (var row in rows)
            map[(row.Direction, row.ChannelNumber)] = row.DeviceName;

        return map;
    }

    /// <summary>
    /// Bu gonderide DOGACAK pinlerin kimlik dizini.
    ///
    /// Ayni gonderide hem cihaz birakilip hem ona kablo cizilebiliyor (pin Id'lerini
    /// artik istemci uretiyor), dolayisiyla bir kablo ucu DB'de olmayan bir pini
    /// gosterebilir. <see cref="ResolveEndpoint"/> once <c>Pins</c>'e, sonra buraya
    /// bakar.
    ///
    /// Gerilim taslaktan degil SABLON pininden okunur: taslak zaten veri tasimiyor.
    /// Cozulemeyen bir <c>ComponentTemplatePinId</c> burada sessizce atlanir —
    /// sema uyumsuzlugunu <see cref="ValidateDevicePinIdentities"/> zaten raporluyor.
    /// </summary>
    private static Dictionary<Guid, PinRef> BuildNewPinRefs(
        List<DeviceDraft> newDevices,
        Dictionary<Guid, List<ComponentTemplatePin>> templatePins)
    {
        var refs = new Dictionary<Guid, PinRef>();

        foreach (var draft in newDevices)
        {
            if (!templatePins.TryGetValue(draft.ComponentTemplateId, out var pins)) continue;
            var byId = pins.ToDictionary(p => p.Id);

            foreach (var pin in draft.Pins)
            {
                if (!byId.TryGetValue(pin.ComponentTemplatePinId, out var templatePin)) continue;
                // Indeksleyici: ayni Id iki kez gelirse sozluk PATLAMAMALI, hata
                // olarak raporlanmali (bkz. ValidateDevicePinIdentities).
                refs[pin.Id] = new PinRef(draft.Id, templatePin.VoltageLevel);
            }
        }

        return refs;
    }

    /// <summary>
    /// Gonderilen pin Id'lerinden DB'de ZATEN var olanlar.
    ///
    /// <c>ignoreFilters: true</c> zorunlu: <c>Pin</c> soft-delete edilebilir ve
    /// silinmis bir satir birincil anahtar uzayini isgal etmeye devam eder. Bu
    /// kontrol olmasaydi carpisan bir Id INSERT'e duser ve PK ihlaliyle 500 verirdi
    /// — kablolarda ayni tuzak <see cref="LoadConnectionsAsync"/>'de anlatiliyor.
    /// </summary>
    private async Task<HashSet<Guid>> LoadClaimedPinIdsAsync(List<DeviceDraft> newDevices, CancellationToken cancellationToken)
    {
        var ids = newDevices.SelectMany(d => d.Pins).Select(p => p.Id).Distinct().ToList();
        if (ids.Count == 0) return [];

        var rows = await _unitOfWork.Pins.GetAllAsync(
            select: p => p.Id,
            where: p => ids.Contains(p.Id),
            ignoreFilters: true,
            cancellationToken: cancellationToken) ?? [];

        return rows.ToHashSet();
    }

    /// <summary>Gonderilen kanal Id'lerinden DB'de ZATEN var olanlar — pinlerle ayni gerekce.</summary>
    private async Task<HashSet<Guid>> LoadClaimedIoChannelIdsAsync(List<DeviceDraft> newDevices, CancellationToken cancellationToken)
    {
        var ids = newDevices.SelectMany(d => d.IoChannels).Select(c => c.Id).Distinct().ToList();
        if (ids.Count == 0) return [];

        var rows = await _unitOfWork.IoChannels.GetAllAsync(
            select: c => c.Id,
            where: c => ids.Contains(c.Id),
            ignoreFilters: true,
            cancellationToken: cancellationToken) ?? [];

        return rows.ToHashSet();
    }

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
    /// Gonderide gecen tum kablo Id'leri; TAKIPLI, kabin filtresi YOK.
    ///
    /// <c>ignoreFilters: true</c> ZORUNLU: soft-delete edilmis satir birincil
    /// anahtar uzayini paylasmaya devam eder. Filtreli sorgu onu "yok" gosterir,
    /// upsert INSERT'e duser ve PK ihlaliyle 500 doner.
    /// </summary>
    private async Task<EntityLookup<Connection>> LoadConnectionsAsync(Guid cabinetId, DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        var ids = request.Connections.Upserted.Select(c => c.Id).Concat(request.Connections.Deleted).Distinct().ToList();
        if (ids.Count == 0) return new();

        var rows = await _unitOfWork.Connections.GetAllAsync(
            where: c => ids.Contains(c.Id),
            ignoreFilters: true,
            cancellationToken: cancellationToken) ?? [];

        return Classify(rows, cabinetId, c => c.Id, c => c.CabinetId, c => c.IsDeleted);
    }

    /// <summary>
    /// Gonderide gecen tum not Id'leri; TAKIPLI, kabin filtresi YOK.
    /// <c>DiagramAnnotation</c> ne aktiflestirilebilir ne soft-delete edilebilir,
    /// dolayisiyla "kaldirilmis" hali yoktur.
    /// </summary>
    private async Task<EntityLookup<DiagramAnnotation>> LoadAnnotationsAsync(Guid cabinetId, DiagramSaveRequest request, CancellationToken cancellationToken)
    {
        var ids = request.DiagramAnnotations.Upserted.Select(a => a.Id).Concat(request.DiagramAnnotations.Deleted).Distinct().ToList();
        if (ids.Count == 0) return new();

        var rows = await _unitOfWork.DiagramAnnotations.GetAllAsync(
            where: a => ids.Contains(a.Id),
            cancellationToken: cancellationToken) ?? [];

        return Classify(rows, cabinetId, a => a.Id, a => a.CabinetId, _ => false);
    }

    /// <summary>
    /// Bulunan satirlari uc kovaya ayirir. Upsert kararinin TEK kaynagi burasi:
    /// <c>Live</c> -> guncelle, hicbirinde yok -> olustur, digerleri -> 400.
    /// </summary>
    private static EntityLookup<TEntity> Classify<TEntity>(
        IEnumerable<TEntity> rows,
        Guid cabinetId,
        Func<TEntity, Guid> idOf,
        Func<TEntity, Guid> cabinetOf,
        Func<TEntity, bool> isRemoved)
    {
        var lookup = new EntityLookup<TEntity>();
        foreach (var row in rows)
        {
            var id = idOf(row);
            if (cabinetOf(row) != cabinetId) lookup.Foreign.Add(id);
            else if (isRemoved(row)) lookup.Removed.Add(id);
            else lookup.Live[id] = row;
        }
        return lookup;
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
    /// Yeni cihazlarin pinleri her zaman bunlardan uretilir. Pini olmayan sablon
    /// burada hic anahtar acmaz ve cihaz pinsiz dogar.
    ///
    /// <c>tracking: false</c> — bunlar yalnizca KOPYALAMA kaynagi; takip
    /// edilirlerse degistirilmedikleri halde change tracker'i sisirirler.
    /// </summary>
    private async Task<Dictionary<Guid, List<ComponentTemplatePin>>> LoadTemplatePinsAsync(List<Guid> templateIds, CancellationToken cancellationToken)
    {
        if (templateIds.Count == 0) return [];

        var rows = await _unitOfWork.ComponentTemplatePins.GetAllAsync(
            where: p => templateIds.Contains(p.ComponentTemplateId),
            orderBy: q => q.OrderBy(p => p.Name),
            tracking: false,
            cancellationToken: cancellationToken) ?? [];

        return rows.GroupBy(p => p.ComponentTemplateId).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>Kablo uclarinin gosterdigi pinler — kabine bagli, canli.</summary>
    private async Task<Dictionary<Guid, Pin>> LoadPinsAsync(Guid cabinetId, List<Guid> referencedPinIds, CancellationToken cancellationToken)
    {
        if (referencedPinIds.Count == 0) return [];

        var rows = await _unitOfWork.Pins.GetAllAsync(
            where: p => p.Device!.CabinetId == cabinetId && referencedPinIds.Contains(p.Id),
            cancellationToken: cancellationToken) ?? [];

        return rows.ToDictionary(p => p.Id);
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
    /// Cihazi silindigi icin birlikte kalkacak kablolar.
    ///
    /// Pin listesi uzerinden degil, dogrudan cihaz uzerinden sorulur: aradaki
    /// "kalkacak pin kumesi" adimi bir kavram fazlasiydi.
    /// </summary>
    private async Task<List<Connection>> LoadCascadeConnectionsAsync(Guid cabinetId, HashSet<Guid> deletedDeviceIds, CancellationToken cancellationToken)
    {
        if (deletedDeviceIds.Count == 0) return [];

        var ids = deletedDeviceIds.ToList();
        var rows = await _unitOfWork.Connections.GetAllAsync(
            where: c => c.CabinetId == cabinetId
                     && (ids.Contains(c.SourcePin!.DeviceId) || ids.Contains(c.TargetPin!.DeviceId)),
            cancellationToken: cancellationToken) ?? [];

        return rows.ToList();
    }

    /// <summary>
    /// Taslaklarin dokundugu pinlere bagli MEVCUT kablolar — cift cakismasi
    /// yalnizca bunlara bakilarak anlasilir. Projeksiyon yeter; bu satirlar
    /// degistirilmeyecek.
    /// </summary>
    private async Task<List<PinPairRow>> LoadPinPairCandidatesAsync(Guid cabinetId, List<Guid> referencedPinIds, CancellationToken cancellationToken)
    {
        if (referencedPinIds.Count == 0) return [];

        var rows = await _unitOfWork.Connections.GetAllAsync(
            select: c => new PinPairRow(c.Id, c.SourcePinId, c.TargetPinId),
            where: c => c.CabinetId == cabinetId
                     && (referencedPinIds.Contains(c.SourcePinId) || referencedPinIds.Contains(c.TargetPinId)),
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
    /// FluentValidation'in goremedigi her sey: taslaklarin isaret ettigi satirlarin
    /// BU KABINE ait oldugu, degismez alanlarin degistirilmedigi ve taslaklarin
    /// birbirleriyle celismedigi.
    ///
    /// Hepsi 400 doner. DB kisitina carpip 500 uretmek yerine burada yakalamak,
    /// kullaniciya hangi taslagin hatali oldugunu indeksiyle soyleyebilmek demek.
    ///
    /// SILMELER burada dogrulanmaz: karsiligi bulunamayan silme bir hata degil,
    /// atlanacak bir istektir (bkz. <c>ApplyDeletions</c>).
    /// </summary>
    private static Dictionary<string, string[]> ValidateReferences(DiagramSaveRequest request, SaveContext context)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        ValidateDevices(request, context, errors);
        ValidateDeviceExternalCodes(request, context, errors);
        ValidateDeviceMacAddresses(request, context, errors);
        ValidateConnections(request, context, errors);
        ValidateAnnotations(request, context, errors);

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal);
    }

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
    /// Yeni bir cihazla birlikte gonderilen pin ve kanal KIMLIKLERI.
    ///
    /// Iki soru sorulur: (1) kume sablonun semasiyla birebir ortusuyor mu, (2)
    /// Id'ler bos degil ve hicbir yerde carpismiyor mu. Pin VERISI dogrulanmaz
    /// cunku istemci veri gondermiyor — her alan sablondan kopyalaniyor.
    ///
    /// Sema kontrolu sekli bir katilik degil: eksik pin gonderen bir istemci
    /// cihazi kopuk pinlerle yaratir, fazla gonderen ise sablonun disinda bir pin
    /// uydurmus olurdu ki bu tam da "pin semasinin tek yazari sablondur" kuralini
    /// delmek demektir.
    /// </summary>
    private static void ValidateDevicePinIdentities(
        DeviceDraft draft,
        string key,
        SaveContext context,
        HashSet<Guid> seenPinIds,
        HashSet<Guid> seenChannelIds,
        HashSet<(PinDirection, int)> seenChannelAddresses,
        Dictionary<string, List<string>> errors)
    {
        var templatePins = context.TemplatePins.GetValueOrDefault(draft.ComponentTemplateId) ?? [];

        // ---- pinler ----
        var expectedTemplatePinIds = templatePins.Select(p => p.Id).ToHashSet();
        var sentTemplatePinIds = new HashSet<Guid>();

        foreach (var pin in draft.Pins)
        {
            if (!sentTemplatePinIds.Add(pin.ComponentTemplatePinId))
                AddError(errors, $"{key}.Pins", "Ayni sablon pini icin birden fazla pin gonderildi");

            if (!seenPinIds.Add(pin.Id))
                AddError(errors, $"{key}.Pins", "Ayni pin kimligi gonderide birden fazla kez var");
            else if (context.ClaimedPinIds.Contains(pin.Id))
                AddError(errors, $"{key}.Pins", "Bu pin kimligi zaten kullanimda");
        }

        if (!sentTemplatePinIds.SetEquals(expectedTemplatePinIds))
            AddError(errors, $"{key}.Pins", "Gonderilen pinler sablonun pin semasiyla ortusmuyor");

        // ---- kanallar ----
        // Adres artik (yon, numara) CIFTI: kartta IN1 ile OUT1 ayri noktalar.
        var expectedAddresses = templatePins
            .Where(p => p.ChannelNumber.HasValue)
            .Select(p => (p.Direction, p.ChannelNumber!.Value))
            .ToHashSet();
        var sentAddresses = new HashSet<(PinDirection, int)>();

        foreach (var channel in draft.IoChannels)
        {
            var address = (channel.Direction, channel.ChannelNumber);

            if (!sentAddresses.Add(address))
                AddError(errors, $"{key}.IoChannels", "Ayni kanal adresi icin birden fazla kanal gonderildi");

            if (!seenChannelIds.Add(channel.Id))
                AddError(errors, $"{key}.IoChannels", "Ayni kanal kimligi gonderide birden fazla kez var");
            else if (context.ClaimedIoChannelIds.Contains(channel.Id))
                AddError(errors, $"{key}.IoChannels", "Bu kanal kimligi zaten kullanimda");

            // KABIN GENELI CAKISMA. Kabin bir kontrol kartidir; kartin adres
            // uzayi duz oldugu icin ayni sablonu ikinci kez birakmak burada
            // durur. Kontrol olmasaydi benzersiz indeks SaveChanges'te patlar ve
            // operatore hicbir sey anlatmayan bir 500 donerdi.
            var label = FormatChannelAddress(channel.Direction, channel.ChannelNumber);

            if (context.CabinetChannelAddresses.TryGetValue(address, out var ownerName))
                AddError(errors, $"{key}.IoChannels",
                    $"{label} bu kabinde zaten kullaniliyor ({ownerName}). Kabin tek bir kontrol kartidir; kanal adresleri kabin genelinde benzersizdir.");
            else if (!seenChannelAddresses.Add(address))
                AddError(errors, $"{key}.IoChannels",
                    $"{label} ayni gonderide birden fazla cihaz tarafindan isteniyor. Kabin tek bir kontrol kartidir; kanal adresleri kabin genelinde benzersizdir.");
        }

        if (!sentAddresses.SetEquals(expectedAddresses))
            AddError(errors, $"{key}.IoChannels", "Gonderilen kanallar sablonun kanal adresleriyle ortusmuyor");
    }

    /// <summary>Hata mesajlarinda kartin kendi dili kullanilir: "IN1", "OUT17".</summary>
    private static string FormatChannelAddress(PinDirection direction, int channelNumber) =>
        ScadaPinAddress.Format(direction, channelNumber);

    /// <summary>
    /// Kablo taslaklari: uclar cozulebiliyor mu, degismez mi kalmis, cift zaten
    /// var mi, gerilimler uyuyor mu.
    /// </summary>
    private static void ValidateConnections(DiagramSaveRequest request, SaveContext context, Dictionary<string, List<string>> errors)
    {
        var survivingPairs = SurvivingPinPairs(request, context);
        var pairsInRequest = new HashSet<(Guid, Guid)>();

        for (int i = 0; i < request.Connections.Upserted.Count; i++)
        {
            var draft = request.Connections.Upserted[i];
            var key = $"Connections.Upserted[{i}]";

            if (ReportUnreachable(context.Connections, draft.Id, errors, $"{key}.Id", "Kablo")) continue;

            // Cihazi silinen bir kablo ayni gonderide yazilamaz: iki niyet celisiyor.
            if (context.CascadeConnections.Any(c => c.Id == draft.Id))
            {
                AddError(errors, $"{key}.Id", "Bu kablo, cihazi silindigi icin kaldiriliyor; ayni gonderide kaydedilemez");
                continue;
            }

            if (context.Connections.Live.TryGetValue(draft.Id, out var existing)
                && (existing.SourcePinId != draft.SourcePinId || existing.TargetPinId != draft.TargetPinId))
            {
                AddError(errors, key, "Kablo uclari degistirilemez; silip yeniden cizin");
                continue;
            }

            var source = ResolveEndpoint(draft.SourcePinId, context, errors, $"{key}.SourcePinId");
            var target = ResolveEndpoint(draft.TargetPinId, context, errors, $"{key}.TargetPinId");
            if (source == null || target == null) continue;

            var pair = PairKey(draft.SourcePinId, draft.TargetPinId);
            // Cift, YONSUZ karsilastirilir. DB'deki unique index (SourcePinId, TargetPinId)
            // sirali oldugu icin ters cizilmis ayni kabloyu YAKALAMAZ; ConnectionMode.Loose
            // ile "kaynak"/"hedef" zaten keyfi oldugundan burada daha katiyiz.
            if (!pairsInRequest.Add(pair) || survivingPairs.Contains(pair))
            {
                AddError(errors, key, "Bu iki pin arasinda zaten bir kablo var");
                continue;
            }

            // Gerilim uyusmazligi: iki taraf da BELIRTILMISSE ve farkliysa reddedilir.
            // Biri null ise ("belirtilmemis") susulur — bilinmeyeni hata saymak,
            // gerilimi henuz girilmemis sablonlarla calismayi imkansiz kilardi.
            if (source.Value.VoltageLevel.HasValue && target.Value.VoltageLevel.HasValue
                && source.Value.VoltageLevel.Value != target.Value.VoltageLevel.Value)
            {
                AddError(errors, key, "Farkli gerilim seviyesindeki pinler baglanamaz");
            }
        }
    }

    /// <summary>Not taslaklari: hedef satir erisilebilir mi.</summary>
    private static void ValidateAnnotations(DiagramSaveRequest request, SaveContext context, Dictionary<string, List<string>> errors)
    {
        for (int i = 0; i < request.DiagramAnnotations.Upserted.Count; i++)
        {
            ReportUnreachable(context.Annotations, request.DiagramAnnotations.Upserted[i].Id, errors, $"Annotations.Upserted[{i}].Id", "Not");
        }
    }

    /// <summary>
    /// Upsert'in iki RET halini raporlar. Satirin hic bulunmamasi hata DEGILDIR —
    /// o, olusturma niyetidir.
    /// </summary>
    private static bool ReportUnreachable<TEntity>(
        EntityLookup<TEntity> lookup,
        Guid id,
        Dictionary<string, List<string>> errors,
        string errorKey,
        string label)
    {
        if (lookup.Foreign.Contains(id))
        {
            AddError(errors, errorKey, $"{label} baska bir kabine ait");
            return true;
        }
        if (lookup.Removed.Contains(id))
        {
            AddError(errors, errorKey, $"{label} silinmis; ayni kimlikle yeniden olusturulamaz");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Kaydetmeden SONRA da ayakta kalacak pin ciftleri — yeni bir kablonun
    /// cakisip cakismadigi buna bakilarak anlasilir.
    ///
    /// Bu gonderide kalkacak ve bu gonderide yeniden yazilacak kablolar cakisma
    /// SAYILMAZ: kullanicinin bir kabloyu silip ayni iki pin arasina yenisini
    /// cizmesi mesru bir islemdir, ve bir kablonun kendisiyle cakismasi anlamsizdir.
    /// </summary>
    private static HashSet<(Guid, Guid)> SurvivingPinPairs(DiagramSaveRequest request, SaveContext context)
    {
        var replaced = new HashSet<Guid>(request.Connections.Deleted);
        foreach (var draft in request.Connections.Upserted) replaced.Add(draft.Id);
        foreach (var connection in context.CascadeConnections) replaced.Add(connection.Id);

        var pairs = new HashSet<(Guid, Guid)>();
        foreach (var candidate in context.PinPairCandidates)
        {
            if (replaced.Contains(candidate.Id)) continue;
            pairs.Add(PairKey(candidate.SourcePinId, candidate.TargetPinId));
        }
        return pairs;
    }

    /// <summary>
    /// Ayni alanda birden fazla hata birikebilsin diye.
    /// <c>ProblemDetails.errors</c> sozlugunun sekli: alan -> mesaj dizisi.
    /// </summary>
    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages)) errors[key] = messages = [];
        messages.Add(message);
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

    /// <summary>
    /// Bir kablo ucunu cozer. Iki kaynak vardir: DB'deki KALICI pinler ve AYNI
    /// GONDERIDE dogacak pinler — Id'leri istemci urettigi icin bir cihaz
    /// birakilip ona ayni kaydetmede kablo cizilebiliyor.
    ///
    /// Cozulemezse hatayi yazar ve null doner. Doner deger gerilim karsilastirmasi
    /// icin kullanilir; iki kaynak da ayni <see cref="PinRef"/> sorusuna cevap
    /// verdigi icin cagiran taraf hangisinden geldigini bilmek zorunda degil.
    /// </summary>
    private static PinRef? ResolveEndpoint(Guid pinId, SaveContext context, Dictionary<string, List<string>> errors, string errorKey)
    {
        PinRef reference;

        if (context.Pins.TryGetValue(pinId, out var pin))
            reference = new PinRef(pin.DeviceId, pin.VoltageLevel);
        else if (!context.NewPins.TryGetValue(pinId, out reference))
        {
            AddError(errors, errorKey, "Pin bu kabinde bulunamadi");
            return null;
        }

        if (context.DeletedDeviceIds.Contains(reference.DeviceId))
        {
            AddError(errors, errorKey, "Cihazi ayni gonderide silinen bir pine kablo cizilemez");
            return null;
        }
        return reference;
    }

    /// <summary>Ciftin YONSUZ anahtari: (a,b) ile (b,a) ayni kabloyu gosterir.</summary>
    private static (Guid, Guid) PairKey(Guid first, Guid second)
        => first.CompareTo(second) <= 0 ? (first, second) : (second, first);

    // ==================== UYGULAMA ====================

    /// <summary>
    /// Silmeler. Sira TERS BAGIMLILIK yonunde: once kablolar, sonra pinler, en son
    /// cihazlar — aksi halde silinen bir pine bagli kablo bir an icin oksuz kalirdi.
    ///
    /// Karsiligi bulunamayan Id'ler SESSIZCE ATLANIR. Bu, istemcinin "bu kayit
    /// sunucuya gitti mi" bilgisini tasima zorunlulugunu kaldiran karardir (K7): iki
    /// kez gonderilen ya da hic olusmamis bir silme, kullanicinin o ana kadarki tum
    /// duzenlemesini 400 ile cope atmaz. Atlananlar SAYILMAZ da: sayiyi okuyan bir
    /// istemci hicbir zaman olmadi ve kaydetme artik bos 200 donuyor.
    /// </summary>
    private void ApplyDeletions(DiagramSaveRequest request, SaveContext context)
    {
        // 1) Kablolar: dogrudan silinenler + cihazi kalkanlar
        var connectionsToRemove = request.Connections.Deleted
            .Where(context.Connections.Live.ContainsKey)
            .Select(id => context.Connections.Live[id])
            .Concat(context.CascadeConnections)
            .DistinctBy(c => c.Id)
            .ToList();
        if (connectionsToRemove.Count > 0)
            _unitOfWork.Connections.Delete(connectionsToRemove);

        // 2) Pinler (ISoftDeletableEntity: interceptor Remove'u IsDeleted=true'ya cevirir)
        if (context.PinsOfDeletedDevices.Count > 0)
            _unitOfWork.Pins.Delete(context.PinsOfDeletedDevices);

        // 3) Cihazlar: Remove() CAGRILMAZ. Device IActivatableEntity oldugu icin
        //    EntityLifecycleInterceptor hard delete'te exception atar ve 500 doner.
        foreach (var id in context.DeletedDeviceIds)
            context.Devices.Live[id].IsActive = false;

        // 4) Notlar: sistemdeki TEK hard delete. DiagramAnnotation ne
        //    IActivatableEntity ne ISoftDeletableEntity oldugundan interceptor araya
        //    girmez ve satir gercekten silinir; kimse ona FK ile bagli olmadigi icin
        //    oksuz satir birakmaz.
        var annotationsToRemove = request.DiagramAnnotations.Deleted
            .Where(context.Annotations.Live.ContainsKey)
            .Select(id => context.Annotations.Live[id])
            .ToList();
        if (annotationsToRemove.Count > 0)
            _unitOfWork.DiagramAnnotations.Delete(annotationsToRemove);
    }

    /// <summary>
    /// Yazmalar. Uc aile birbirinden bagimsiz; sira onemli degil.
    /// Her taslak icin tek soru: satir <c>Live</c> mi (guncelle) yoksa yok mu (olustur).
    /// </summary>
    private void ApplyUpserts(Guid cabinetId, DiagramSaveRequest request, SaveContext context)
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

        foreach (var draft in request.Connections.Upserted)
        {
            if (context.Connections.Live.TryGetValue(draft.Id, out var connection))
            {
                WriteConnection(connection, draft);
                continue;
            }

            connection = new Connection
            {
                Id = draft.Id,
                CabinetId = cabinetId,
                // Uclar YALNIZCA olusturmada yazilir; guncellemede degismezligi
                // ValidateConnections dogruladi.
                SourcePinId = draft.SourcePinId,
                TargetPinId = draft.TargetPinId
            };
            WriteConnection(connection, draft);
            _unitOfWork.Connections.Add(connection);
        }

        foreach (var draft in request.DiagramAnnotations.Upserted)
        {
            if (context.Annotations.Live.TryGetValue(draft.Id, out var annotation))
            {
                _mapper.Map(draft, annotation);
                continue;
            }

            annotation = new DiagramAnnotation { Id = draft.Id, CabinetId = cabinetId };
            _mapper.Map(draft, annotation);  // Id ve CabinetId disinda tum alanlar modelden kopyalanir
            _unitOfWork.DiagramAnnotations.Add(annotation);
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

    private static void WriteConnection(Connection connection, ConnectionDraft draft)
    {
        connection.Label = draft.Label;
        connection.WireType = draft.WireType;
        connection.Color = draft.Color;
        connection.LineStyle = draft.LineStyle;
        connection.StrokeWidth = draft.StrokeWidth;
        connection.Routing = draft.Routing;
        connection.WaypointsJson = DiagramWaypoints.Serialize(draft.Waypoints);
        connection.ZIndex = draft.ZIndex;
    }

    /// <summary>
    /// Sablonun pin semasini cihaza kopyalar ve kanal numarasi tasiyan her pin icin
    /// bir <c>IoChannel</c> uretir.
    ///
    /// <b>Kanallar neden burada dogar.</b> Bu olmadan SCADA ingest'inin yazacagi
    /// HICBIR SATIR olmazdi: ingest kanali <c>(CabinetId, Direction, ChannelNumber)</c>
    /// ile cozuyor ve tanimadigi kanali sessizce atliyor (K7). Kanallari ureten baska
    /// bir yol da yok — urunde cihaz yaratmanin tek yolu paletten birakmak.
    ///
    /// <b>Id'leri ISTEMCI uretir, icerigi SUNUCU.</b> Taslak yalnizca
    /// "su sablon pini icin su Guid'i kullan" der; ad, konum, fonksiyon, yon ve
    /// gerilim buradaki kopyalamayla gelmeye devam eder. Taslakta karsiligi
    /// bulunmayan bir sablon pini olamaz — <see cref="ValidateDevicePinIdentities"/>
    /// kumelerin birebir ortustugunu yazmadan once dogruladi.
    ///
    /// <b>Neden hala navigasyon, neden skaler FK degil.</b> <c>pin.Device = device</c>
    /// ve <c>pin.IoChannel = channel</c> yazmaya devam ediyoruz: Id'lerin biliniyor
    /// olmasi EF'in ekleme SIRASINI cozmesini gereksiz kilmaz, elle FK atamak ise
    /// ayni bilgiyi iki yerde tutmak olurdu.
    /// </summary>
    private void InstantiateTemplatePins(Device device, List<ComponentTemplatePin> templatePins, DeviceDraft draft)
    {
        var pinIdByTemplatePinId = draft.Pins.ToDictionary(p => p.ComponentTemplatePinId, p => p.Id);
        var channelIdByAddress = draft.IoChannels.ToDictionary(c => (c.Direction, c.ChannelNumber), c => c.Id);

        // Ayni (yon, kanal numarasi) cifti TEK bir IoChannel'dir. Sablonda iki pin
        // ayni kanali gosteriyorsa (or. bir rolenin COM ve NO uclari) ikisi de ayni
        // kanala baglanir; ayri ayri uretmek
        // IX_IoChannel_CabinetId_Direction_ChannelNumber'i ihlal ederdi.
        var channelsByAddress = new Dictionary<(PinDirection, int), IoChannel>();

        foreach (var templatePin in templatePins)
        {
            var pin = new Pin
            {
                Id = pinIdByTemplatePinId[templatePin.Id],
                Device = device,
                ComponentTemplatePinId = templatePin.Id,
                Name = templatePin.Name,
                RelativeX = templatePin.RelativeX,
                RelativeY = templatePin.RelativeY,
                Side = templatePin.Side,
                Function = templatePin.Function,
                Direction = templatePin.Direction,
                VoltageLevel = templatePin.VoltageLevel,
                ChannelNumber = templatePin.ChannelNumber
            };

            if (templatePin.ChannelNumber is int channelNumber)
            {
                var address = (templatePin.Direction, channelNumber);

                if (!channelsByAddress.TryGetValue(address, out var channel))
                {
                    channel = new IoChannel
                    {
                        Id = channelIdByAddress[address],
                        Device = device,
                        // CabinetId denormalize: benzersizlik kabin kapsamli ve
                        // bir kolon olmadan indekse dokulemezdi. Navigasyon
                        // uzerinden atanir ki EF ekleme sirasini kendisi cozsun
                        // ve ayni bilgi iki yerde tutulmasin.
                        Cabinet = device.Cabinet,
                        CabinetId = device.CabinetId,
                        ChannelNumber = channelNumber,
                        Direction = templatePin.Direction,
                        IsEnabled = true,
                        Name = templatePin.Name
                    };
                    _unitOfWork.IoChannels.Add(channel);
                    channelsByAddress[address] = channel;
                }

                pin.IoChannel = channel;
            }

            _unitOfWork.Pins.Add(pin);
        }
    }

    // ==================== YARDIMCI TIPLER ====================

    /// <summary>
    /// Bir ailenin Id'lerine verilen cevap. Uc kova, upsert'in uc sonucuna birebir
    /// karsilik gelir; <c>Live</c>'da olmayan ve hicbir kovada bulunmayan Id yenidir.
    /// </summary>
    private sealed class EntityLookup<TEntity>
    {
        public Dictionary<Guid, TEntity> Live { get; } = [];
        /// <summary>Var ama BASKA kabine ait — sessiz capraz-kabin duzenlemesi olmasin diye.</summary>
        public HashSet<Guid> Foreign { get; } = [];
        /// <summary>Var ama silinmis/pasif — birincil anahtar uzayini isgal etmeye devam ediyor.</summary>
        public HashSet<Guid> Removed { get; } = [];
    }

    /// <summary>Kaydetme icin DB'den bir kez okunan her sey.</summary>
    private sealed class SaveContext
    {
        public required EntityLookup<Device> Devices { get; init; }
        public required EntityLookup<Connection> Connections { get; init; }
        public required EntityLookup<DiagramAnnotation> Annotations { get; init; }
        /// <summary>Bu gonderide GERCEKTEN silinecek cihazlar (karsiligi bulunanlar).</summary>
        public required HashSet<Guid> DeletedDeviceIds { get; init; }
        public required HashSet<Guid> ActiveTemplateIds { get; init; }
        public required Dictionary<Guid, List<ComponentTemplatePin>> TemplatePins { get; init; }
        /// <summary>Kablo uclarinin gosterdigi pinler.</summary>
        public required Dictionary<Guid, Pin> Pins { get; init; }
        /// <summary>Cihazi silindigi icin birlikte kalkacak pinler.</summary>
        public required List<Pin> PinsOfDeletedDevices { get; init; }
        /// <summary>Cihazi silindigi icin birlikte kalkacak kablolar.</summary>
        public required List<Connection> CascadeConnections { get; init; }
        /// <summary>Kabindeki aktif cihazlarin dis kodlari (yalnizca gerektiginde okunur).</summary>
        public required Dictionary<Guid, string> DeviceExternalCodes { get; init; }
        /// <summary>Gonderilen MAC adreslerinin sahipleri — KABIN GENELI DEGIL, SISTEM GENELI.</summary>
        public required Dictionary<Guid, string> DeviceMacAddresses { get; init; }
        /// <summary>Cift cakismasi icin bakilacak mevcut kablolar.</summary>
        public required List<PinPairRow> PinPairCandidates { get; init; }
        /// <summary>Bu gonderide DOGACAK pinler — kablo uclari bunlari da gosterebilir.</summary>
        public required Dictionary<Guid, PinRef> NewPins { get; init; }
        /// <summary>Gonderilen pin Id'lerinden DB'de zaten var olanlar (carpisma).</summary>
        public required HashSet<Guid> ClaimedPinIds { get; init; }
        /// <summary>Gonderilen kanal Id'lerinden DB'de zaten var olanlar (carpisma).</summary>
        public required HashSet<Guid> ClaimedIoChannelIds { get; init; }
        /// <summary>Kabinde kullanimda olan kanal adresleri -> sahibi cihazin adi.</summary>
        public required Dictionary<(PinDirection Direction, int ChannelNumber), string> CabinetChannelAddresses { get; init; }
    }

    /// <summary>
    /// Bir kablo ucunun cozumu. Kalici bir <c>Pin</c> satiri da, ayni gonderide
    /// dogacak bir pin de bu iki soruya cevap verir; dogrulama baska bir sey
    /// sormadigi icin ortak tip bu kadar dar.
    /// </summary>
    private readonly record struct PinRef(Guid DeviceId, VoltageLevel? VoltageLevel);

    private sealed record PinPairRow(Guid Id, Guid SourcePinId, Guid TargetPinId);

    private sealed record DeviceCodeRow(Guid Id, string ExternalCode);

    private sealed record DeviceMacRow(Guid Id, string MacAddress);

    private sealed record ChannelAddressRow(PinDirection Direction, int ChannelNumber, string DeviceName);
}
