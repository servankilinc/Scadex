using Scadex.Model.Dtos.Diagram.Commands.Items;
using Scadex.Model.Dtos.Scada.Commands;
using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

// DiagramService kaydetme boru hattinin cihaz ailesine ait PIN / KANAL alt dilimi:
// yukleme -> dogrulama -> uygulama. Cihazin kendi govdesi (ad, konum, dis kod, MAC)
// DiagramService.SaveDevices.cs'te; genel gerekce ve akis haritasi
// DiagramService.SaveContext.cs'te.
//
// Ortak kural: pin ve kanalin ICERIGINI her zaman sablon yazar, KIMLIGINI istemci.
public partial class DiagramService
{
    // ==================== YUKLEME ====================

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

    /// <summary>
    /// Bu gonderide DOGACAK pinlerin kimlik dizini.
    ///
    /// Ayni gonderide hem cihaz birakilip hem ona kablo cizilebiliyor (pin Id'lerini
    /// artik istemci uretiyor), dolayisiyla bir kablo ucu DB'de olmayan bir pini
    /// gosterebilir. <see cref="ResolveEndpoint"/> once <c>Pins</c>'e, sonra buraya
    /// bakar (DiagramService.SaveConnections.cs).
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
    /// — kablolarda ayni tuzak <see cref="LoadConnectionsAsync"/>'de anlatiliyor
    /// (DiagramService.SaveConnections.cs).
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

    // ==================== REFERANS DOGRULAMA ====================

    /// <summary>
    /// Yeni bir cihazla birlikte gonderilen pin ve kanal KIMLIKLERI.
    /// <c>ValidateDevices</c> (DiagramService.SaveDevices.cs) yalnizca sablonu
    /// cozulebilen YENI cihazlar icin cagirir.
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

    // ==================== UYGULAMA ====================

    /// <summary>
    /// Sablonun pin semasini cihaza kopyalar ve kanal numarasi tasiyan her pin icin
    /// bir <c>IoChannel</c> uretir. <c>ApplyDeviceUpserts</c> yalnizca YENI cihazlar
    /// icin cagirir (DiagramService.SaveDevices.cs).
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
    /// kumelerin birebir ortustugunu yazmadan once dogruladi; asagidaki iki
    /// indeksleyicinin guvenli olmasi tam da buna dayanir.
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

    private sealed record ChannelAddressRow(PinDirection Direction, int ChannelNumber, string DeviceName);
}
