using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

/// <summary>
/// Toplu kaydetmenin ic adimlari: DB'den okuma, referans dogrulama ve uygulama.
/// Orkestrasyon <c>DiagramService.Save.cs</c>'teki <c>SaveAsync</c>'te; bu dosya o
/// orkestrasyonun dagiticilarini ve uc ailenin de okudugu <c>SaveContext</c>'i tasir.
///
/// <b>Model: upsert.</b> Guid'i istemci uretir, dolayisiyla "yeni mi mevcut mu"
/// sorusu istemcinin niyetinden degil, TEK bir yerden okunur: Id veritabaninda
/// var mi. Bu boru hattindaki her sey o karar noktasinin etrafinda kurulu.
///
/// <b>Akis:</b>
/// <code>
/// Istek -> 1. FluentValidation (DiagramSaveRequest.cs)  — sekil / uzunluk / enum
///          2. Kabin var mi + IsEmpty kisa devresi       — Save.cs
///          3. LoadSaveContextAsync                      — TEK okuma turu (en fazla 15 sorgu)
///          4. ValidateReferences                        — DB kisitlarinin on kontrolu -> 400
///          -- transaction --
///          5. ApplyDeletions  -> SaveChangesAsync       — kablo -> pin -> cihaz -> not
///          6. ApplyUpserts    -> SaveChangesAsync       — uc aile bagimsiz
///          -- commit --
/// </code>
///
/// <b>Adimlarin govdeleri aile bazli uc dosyada</b>, her biri kendi
/// yukleme -> dogrulama -> uygulama dikey dilimini tasir:
/// <c>DiagramService.SaveDevices.cs</c> (cihaz govdesi),
/// <c>DiagramService.SaveDevicePins.cs</c> (pin / kanal alt ailesi),
/// <c>DiagramService.SaveConnections.cs</c> (kablo + uc cozumleme),
/// <c>DiagramService.SaveAnnotations.cs</c> (not).
///
/// <b>Nereye ne eklenir:</b>
/// <list type="bullet">
/// <item>Cihaza yeni alan: <c>DeviceDraft</c> + <c>DeviceDraftValidator</c> ->
/// <c>WriteDevice</c> (SaveDevices.cs) -> TS aynasi (Scadex.WebUI/src/models/diagram).</item>
/// <item>Yeni benzersizlik kurali: bir <c>LoadXAsync</c> + <c>SaveContext</c> alani +
/// bir <c>ValidateX</c> (alan bazli anahtarla <see cref="AddError"/>).</item>
/// <item>Yeni aile: yeni <c>DiagramService.SaveXxx.cs</c> + <c>EntityDelta&lt;T&gt;</c>
/// alani + asagidaki dort dagiticida birer satir.</item>
/// <item>Silme sirasi: yalnizca <see cref="ApplyDeletions"/>; gerekcesi orada.</item>
/// </list>
/// </summary>
public partial class DiagramService
{
    // ==================== ORKESTRASYON ====================

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
    ///
    /// Yukleyicilerin kendileri aile dosyalarindadir (SaveDevices, SaveDevicePins,
    /// SaveConnections, SaveAnnotations); burada yalnizca cagri sirasi durur ve o
    /// sira bagimlidir: <c>deletedDeviceIds</c> cihaz yuklemesinden dogar, hem pin
    /// hem kablo cascade'i onu kullanir.
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
            MonitorableTemplateIds = await LoadMonitorableTemplateIdsAsync(request, cancellationToken),
            NewPins = BuildNewPinRefs(newDevices, templatePins),
            ClaimedPinIds = await LoadClaimedPinIdsAsync(newDevices, cancellationToken),
            ClaimedIoChannelIds = await LoadClaimedIoChannelIdsAsync(newDevices, cancellationToken),
            CabinetChannelAddresses = await LoadCabinetChannelAddressesAsync(cabinetId, newDevices, cancellationToken)
        };
    }

    /// <summary>
    /// FluentValidation'in goremedigi her sey: taslaklarin isaret ettigi satirlarin
    /// BU KABINE ait oldugu, degismez alanlarin degistirilmedigi ve taslaklarin
    /// birbirleriyle celismedigi.
    ///
    /// Hepsi 400 doner. DB kisitina carpip 500 uretmek yerine burada yakalamak,
    /// kullaniciya hangi taslagin hatali oldugunu indeksiyle soyleyebilmek demek.
    ///
    /// SILMELER burada dogrulanmaz: karsiligi bulunamayan silme bir hata degil,
    /// atlanacak bir istektir (bkz. <see cref="ApplyDeletions"/>).
    /// </summary>
    private static Dictionary<string, string[]> ValidateReferences(DiagramSaveRequest request, SaveContext context)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        ValidateDevices(request, context, errors);
        ValidateDeviceExternalCodes(request, context, errors);
        ValidateDeviceMacAddresses(request, context, errors);
        ValidateDeviceMonitoring(request, context, errors);
        ValidateConnections(request, context, errors);
        ValidateAnnotations(request, context, errors);

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Silmeler. Sira TERS BAGIMLILIK yonunde: once kablolar, sonra pinler, en son
    /// cihazlar — aksi halde silinen bir pine bagli kablo bir an icin oksuz kalirdi.
    /// Notlar bagimsizdir, en sona alinmistir.
    ///
    /// Karsiligi bulunamayan Id'ler SESSIZCE ATLANIR. Bu, istemcinin "bu kayit
    /// sunucuya gitti mi" bilgisini tasima zorunlulugunu kaldiran karardir (K7): iki
    /// kez gonderilen ya da hic olusmamis bir silme, kullanicinin o ana kadarki tum
    /// duzenlemesini 400 ile cope atmaz. Atlananlar SAYILMAZ da: sayiyi okuyan bir
    /// istemci hicbir zaman olmadi ve kaydetme artik bos 200 donuyor.
    ///
    /// Adimlarin kendi mekanizma gerekceleri (interceptor davranisi, neden
    /// <c>Remove()</c> cagrilmadigi) yaprak metotlarin doc'larindadir.
    /// </summary>
    private void ApplyDeletions(DiagramSaveRequest request, SaveContext context)
    {
        ApplyConnectionDeletions(request, context);   // 1) kablolar (+ cihazi kalkanlar)
        ApplyPinDeletions(context);                   // 2) pinler
        ApplyDeviceDeletions(context);                // 3) cihazlar (IsActive = false)
        ApplyAnnotationDeletions(request, context);   // 4) notlar (sistemdeki TEK hard delete)
    }

    /// <summary>
    /// Yazmalar. Uc aile birbirinden bagimsiz; sira onemli degil.
    /// Her taslak icin tek soru: satir <c>Live</c> mi (guncelle) yoksa yok mu (olustur).
    /// </summary>
    private void ApplyUpserts(Guid cabinetId, DiagramSaveRequest request, SaveContext context)
    {
        ApplyDeviceUpserts(cabinetId, request, context);
        ApplyConnectionUpserts(cabinetId, request, context);
        ApplyAnnotationUpserts(cabinetId, request, context);
    }

    // ==================== PAYLASILAN YARDIMCILAR ====================

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
    /// Ayni alanda birden fazla hata birikebilsin diye.
    /// <c>ProblemDetails.errors</c> sozlugunun sekli: alan -> mesaj dizisi.
    /// </summary>
    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages)) errors[key] = messages = [];
        messages.Add(message);
    }

    // ==================== PAYLASILAN TIPLER ====================

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
        /// <summary>Izlemesi acik gonderilen taslaklarin sablonlarindan izlenebilir olanlar (yalnizca gerektiginde okunur).</summary>
        public required HashSet<Guid> MonitorableTemplateIds { get; init; }
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
}
