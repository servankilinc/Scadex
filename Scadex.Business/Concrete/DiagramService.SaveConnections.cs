using Scadex.Business.Utils;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Dtos.Diagram.Commands.Items;
using Scadex.Model.Entities;

namespace Scadex.Business.Concrete;

// DiagramService kaydetme boru hattinin KABLO ailesi: yukleme -> dogrulama -> uygulama.
// Kablo uclarinin cozumlenmesi (kalici pinler + ayni gonderide dogacak pinler) de burada.
// Genel gerekce, akis haritasi ve SaveContext icin DiagramService.SaveContext.cs'e bakin.
// NOT: cihaz silmesinden dogan PINLER cihaz ailesindedir (DiagramService.SaveDevices.cs,
// LoadPinsOfDeletedDevicesAsync); burada yalnizca kablolarin cascade'i vardir.
public partial class DiagramService
{
    // ==================== YUKLEME ====================

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

    // ==================== REFERANS DOGRULAMA ====================

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
    /// Dogrudan silinen kablolar + cihazi kalktigi icin birlikte kalkanlar.
    ///
    /// Silme sirasindaki yeri ve karsiligi bulunamayan Id'lerin neden sessizce
    /// atlandigi icin <c>ApplyDeletions</c>'a bakin (DiagramService.SaveContext.cs).
    /// </summary>
    private void ApplyConnectionDeletions(DiagramSaveRequest request, SaveContext context)
    {
        var connectionsToRemove = request.Connections.Deleted
            .Where(context.Connections.Live.ContainsKey)
            .Select(id => context.Connections.Live[id])
            .Concat(context.CascadeConnections)
            .DistinctBy(c => c.Id)
            .ToList();
        if (connectionsToRemove.Count > 0)
            _unitOfWork.Connections.Delete(connectionsToRemove);
    }

    /// <summary>
    /// Kablo yazmalari. Her taslak icin tek soru: satir <c>Live</c> mi (guncelle)
    /// yoksa yok mu (olustur).
    /// </summary>
    private void ApplyConnectionUpserts(Guid cabinetId, DiagramSaveRequest request, SaveContext context)
    {
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
}
