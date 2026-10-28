using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Dtos.Diagram.Queries.Items;
using Scadex.Model.Dtos.Scada.Commands;
using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;
using DeviceType = Scadex.Model.Enums.EntityEnums.DeviceType;

namespace Scadex.Business.Concrete;

// Diyagramın CİHAZ uçları: oluşturma (paletten bırakma / kopyalama) ve özellik formu.
// Konum / boyut / kilit / görünürlük DiagramService.Layout.cs'te, silme DiagramService.Delete.cs'te.
//
// Tipe özgü alanlar Device'ta değil 1-1 ek tablolardadır: Camera, ControlCard, PcDevice (ortak birincil anahtar, DeviceId).
// Hangi ek nesnenin gelmesi gerektiğini ŞABLONUN DeviceType'ı belirler; taslak ile tip arasındaki tutarlılığı DB değil
// bu dosya garanti eder.
public partial class DiagramService
{
    public async Task<Result<DiagramDeviceDto>> CreateDeviceAsync(Guid cabinetId, DeviceCreateRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<DiagramDeviceDto>.Validation(validationResult.Failures, description: "Validation failed for DeviceCreateRequest");

        if (!await IsCabinetActiveAsync(cabinetId, cancellationToken))
            return Result<DiagramDeviceDto>.NotFound(description: "Kabin bulunamadi veya pasif durumda");

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        // Device hicbir zaman fiziksel silinmez (IActivatableEntity): pasif satir da kimligi isgal etmeye devam eder.
        if (await _unitOfWork.Devices.IsExistAsync(where: d => d.Id == request.Id, cancellationToken: cancellationToken))
            AddError(errors, nameof(request.Id), "Bu cihaz kimligi zaten kullanimda");

        // Palet gibi burasi da bir SECIM kaynagi: pasif sablon yeni cihaz uretmemeli.
        var template = await _unitOfWork.ComponentTemplates.GetAsync(
            select: t => new TemplateRow(t.DeviceTypeId, t.IsMonitorable),
            where: t => t.Id == request.ComponentTemplateId && t.IsActive,
            cancellationToken: cancellationToken);
        if (template == null)
        {
            // Sablon cozulemediyse pin semasi da tip de bilinmiyor: asagidaki kontroller yalnizca kafa karistirirdi.
            AddError(errors, nameof(request.ComponentTemplateId), "Sablon bulunamadi veya pasif durumda");
            return Result<DiagramDeviceDto>.Validation(ToFailures(errors), description: "Cihaz olusturulamadi");
        }

        // tracking: false — bunlar yalnizca KOPYALAMA kaynagi; takip edilirlerse change tracker'i sisirirler.
        var templatePins = await _unitOfWork.ComponentTemplatePins.GetAllAsync(
            where: p => p.ComponentTemplateId == request.ComponentTemplateId,
            orderBy: q => q.OrderBy(p => p.Name),
            tracking: false,
            cancellationToken: cancellationToken) ?? [];

        await ValidateDeviceAsync(cabinetId, request.Id, request, template, errors, cancellationToken);
        await ValidateChannelAddressesAsync(cabinetId, templatePins, errors, cancellationToken);
        if (errors.Count > 0)
            return Result<DiagramDeviceDto>.Validation(ToFailures(errors), description: "Cihaz olusturulamadi");

        var device = new Device
        {
            Id = request.Id,
            CabinetId = cabinetId,
            ComponentTemplateId = request.ComponentTemplateId,
            CoordinateX = request.CoordinateX,
            CoordinateY = request.CoordinateY,
            Width = request.Width,
            Height = request.Height,
            Rotation = request.Rotation,
            ZIndex = request.ZIndex,
            IsLocked = request.IsLocked,
            IsVisible = request.IsVisible,
            // Taslakta IsActive yok, servis ACIKCA true yazar. Yazilmazsa kayit pasif dogar ve diyagram okumasindan duser.
            IsActive = true
        };
        WriteDevice(device, request);
        _unitOfWork.Devices.Add(device);
        WriteDeviceExtensions(device, request);
        InstantiateTemplatePins(device, templatePins);

        // Cihaz + pinler + kanallar + ek satir TEK SaveChanges: EF hepsini tek transaction'da ve dogru sirayla yazar.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Pin ve kanal Id'lerini istemci bilmiyor (EF uretti): tuval cihazi bu yanittan kurar.
        var created = await _unitOfWork.Devices.GetAsync<DiagramDeviceDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: d => d.Id == device.Id,
            cancellationToken: cancellationToken);
        return Result<DiagramDeviceDto>.Success(created!);
    }

    public async Task<Result> UpdateDeviceAsync(Guid cabinetId, Guid deviceId, DeviceUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for DeviceUpdateRequest");

        if (!await IsCabinetActiveAsync(cabinetId, cancellationToken))
            return Result.NotFound(description: "Kabin bulunamadi veya pasif durumda");

        // TAKIPLI okunur: EF degisen kolonu kendisi bulur. Update() cagirmak butun kolonlari degismis isaretler ve
        // ICabinetStatusService'in yazdigi telemetri alanlarini (DeviceStatusId, LastSeen...) eski degerleriyle geri yazardi.
        var device = await _unitOfWork.Devices.GetAsync(
            where: d => d.Id == deviceId && d.CabinetId == cabinetId && d.IsActive,
            include: q => q.Include(d => d.Camera).Include(d => d.ControlCard).Include(d => d.PcDevice).Include(d => d.ComponentTemplate),
            cancellationToken: cancellationToken);
        if (device == null)
            return Result.NotFound(description: "Cihaz bu kabinde bulunamadi");

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        // Pasif sablon da kabul: mevcut cihazin sablonu sonradan emekliye ayrilmis olabilir, cihaz yine de duzenlenebilmeli.
        var template = new TemplateRow(device.ComponentTemplate!.DeviceTypeId, device.ComponentTemplate.IsMonitorable);
        await ValidateDeviceAsync(cabinetId, device.Id, request, template, errors, cancellationToken);
        if (errors.Count > 0)
            return Result.Validation(ToFailures(errors), description: "Cihaz guncellenemedi");

        WriteDevice(device, request);
        WriteDeviceExtensions(device, request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // ==================== DOGRULAMA ====================

    /// <summary>
    /// FluentValidation'in goremedigi, olusturma ve guncellemenin ORTAK kurallari:
    /// <list type="bullet">
    /// <item>Ek nesne sablon tipiyle eslesmeli: kamera sablonu <c>Camera</c>, kontrol modulu <c>ControlCard</c>, PC <c>Pc</c>
    /// ister; diger tipler hicbirini gondermez. Sessizce yok saymak, yanlis panelden yazilan kimlik bilgisini kaybetmek olurdu.</item>
    /// <item>Kamera IP'si zorunlu (RTSP adresi ondan kurulur) ve KABIN ICINDE tekil. CabinetId ve IsActive Device'ta oldugu
    /// icin DB index'iyle ifade edilemiyor; kamera ekrani (<c>CameraService</c>) da ayni kurali uygular.</item>
    /// <item>Izleme yalnizca izlenebilir sablonda (<c>ComponentTemplate.IsMonitorable</c>) acilabilir: klemensin, sigortanin
    /// yoklanacak bir agi yoktur. Kapatmak her zaman serbesttir.</item>
    /// </list>
    /// </summary>
    private async Task ValidateDeviceAsync(
        Guid cabinetId,
        Guid deviceId,
        DeviceUpdateRequest request,
        TemplateRow template,
        Dictionary<string, List<string>> errors,
        CancellationToken cancellationToken)
    {
        var isCamera = template.DeviceTypeId == (int)DeviceType.Camera;

        CheckExtension(request.Camera != null, isCamera, nameof(request.Camera), "Kamera bilgisi", errors);
        CheckExtension(request.ControlCard != null, template.DeviceTypeId == (int)DeviceType.ControlModule, nameof(request.ControlCard), "Kontrol kartı bilgisi", errors);
        CheckExtension(request.Pc != null, template.DeviceTypeId == (int)DeviceType.Pc, nameof(request.Pc), "Bilgisayar bilgisi", errors);
        // Kontrol karti ve PC kimligi tekillik denetimi ISTEMEZ: Basic Auth kullanici adi cihazin Id'sidir.

        if (isCamera)
        {
            if (string.IsNullOrWhiteSpace(request.IpAddress))
                AddError(errors, nameof(request.IpAddress), "Kamera için IP adresi zorunlu");
            else
            {
                // SQL Server'in varsayilan collation'i buyuk/kucuk harf duyarsizdir; esitlik orada karsilastirilir.
                var ipAddress = request.IpAddress;
                var taken = await _unitOfWork.Devices.IsExistAsync(
                    where: d => d.CabinetId == cabinetId && d.IsActive && d.Id != deviceId && d.Camera != null && d.IpAddress == ipAddress,
                    cancellationToken: cancellationToken);
                if (taken)
                    AddError(errors, nameof(request.IpAddress), "Bu kabinde aynı IP adresine sahip bir kamera zaten var");
            }
        }

        if (request.IsMonitoringEnabled && !template.IsMonitorable)
            AddError(errors, nameof(request.IsMonitoringEnabled), "Bu cihazin sablonu izlenebilir degil; izleme acilamaz");
    }

    private static void CheckExtension(bool sent, bool expected, string key, string label, Dictionary<string, List<string>> errors)
    {
        if (sent && !expected) AddError(errors, key, $"{label} bu cihazın şablonuna ait değil; gönderilemez");
        else if (!sent && expected) AddError(errors, key, $"{label} zorunlu");
    }

    /// <summary>
    /// Yeni cihazin isteyecegi kanal adresleri kabinde bos mu.
    ///
    /// <b>Neden kabin geneli.</b> Kabin BIR kontrol kartidir ve kartin adres uzayi duzdur: <c>IN1</c> kabinde tektir,
    /// cihazda degil (<c>IX_IoChannel_CabinetId_Direction_ChannelNumber</c>). Bu kontrol olmasaydi ayni sablonu ikinci kez
    /// birakmak SaveChanges'te benzersiz indeks ihlaliyle 500 verirdi. Cihaz ADI da okunur: "IN1 zaten kullaniliyor" tek
    /// basina operatore hangi kutuya bakacagini soylemez.
    ///
    /// Silinen cihazlarin kanallari HARIC TUTULMAZ: kanallar cihazla birlikte kalkmiyor (silme onlara dokunmuyor),
    /// dolayisiyla adres isgal edilmeye devam ediyor.
    /// </summary>
    private async Task ValidateChannelAddressesAsync(
        Guid cabinetId,
        ICollection<ComponentTemplatePin> templatePins,
        Dictionary<string, List<string>> errors,
        CancellationToken cancellationToken)
    {
        var addresses = templatePins
            .Where(p => p.ChannelNumber.HasValue)
            .Select(p => (p.Direction, ChannelNumber: p.ChannelNumber!.Value))
            .ToHashSet();
        if (addresses.Count == 0) return;

        var channelNumbers = addresses.Select(a => a.ChannelNumber).Distinct().ToList();
        var rows = await _unitOfWork.IoChannels.GetAllAsync(
            select: c => new ChannelAddressRow(c.Direction, c.ChannelNumber, c.Device!.Name),
            where: c => c.CabinetId == cabinetId && channelNumbers.Contains(c.ChannelNumber),
            cancellationToken: cancellationToken) ?? [];

        foreach (var row in rows.Where(r => addresses.Contains((r.Direction, r.ChannelNumber))))
        {
            var label = ScadaPinAddress.Format(row.Direction, row.ChannelNumber);
            AddError(errors, "IoChannels",
                $"{label} bu kabinde zaten kullaniliyor ({row.DeviceName}). Kabin tek bir kontrol kartidir; kanal adresleri kabin genelinde benzersizdir.");
        }
    }

    // ==================== UYGULAMA ====================

    /// <summary>
    /// <c>DeviceStatusId</c> / <c>LastSeen</c> / <c>LastConnectionError</c> burada DOKUNULMAZ — istekte zaten yoklar, yalnizca
    /// <c>ICabinetStatusService</c> yazar. Izlemesi kapatilan cihazin durumu son degerinde kalir; son kaniti 23 saatten
    /// eskiyse periyodik tarama onu <c>null</c>'a (bilinmiyor) ceker — Offline'a degil.
    /// </summary>
    private static void WriteDevice(Device device, DeviceUpdateRequest request)
    {
        device.Name = request.Name;
        device.IpAddress = request.IpAddress;
        device.MonitoringPort = request.MonitoringPort;
        device.PingIntervalSec = request.PingIntervalSec;
        device.IsMonitoringEnabled = request.IsMonitoringEnabled;
    }

    /// <summary>
    /// Cihazin ek satirini yazar: yoksa olusturur, varsa takipli satiri gunceller. Hangi nesnenin gelecegi
    /// <see cref="ValidateDeviceAsync"/>'ta tiple eslestirildi; burada istek izlenir. Kamerada bos <c>MonitoringPort</c>
    /// markanin RTSP portuyla doldurulur: kamerada yoklanacak hizmet RTSP'dir, ICMP ping'e dusmesin.
    /// </summary>
    private void WriteDeviceExtensions(Device device, DeviceUpdateRequest request)
    {
        if (request.Camera is { } cameraDraft)
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

        if (request.ControlCard is { } cardDraft)
        {
            var card = device.ControlCard;
            if (card == null)
            {
                card = new ControlCard { DeviceId = device.Id };
                device.ControlCard = card;
                _unitOfWork.ControlCards.Add(card);
            }
            card.MacAddress = cardDraft.MacAddress;
        }

        if (request.Pc is { } pcDraft)
        {
            var pc = device.PcDevice;
            if (pc == null)
            {
                pc = new PcDevice { DeviceId = device.Id };
                device.PcDevice = pc;
                _unitOfWork.PcDevices.Add(pc);
            }
            pc.CredentialHash = ApplySecret(pc.CredentialHash, pcDraft.Secret);
        }
    }

    /// <summary> Okuma DTO'su parolayi dondurmez: <c>null</c> "dokunma", <c>""</c> "temizle", deger "yaz" demektir. </summary>
    private static string? ApplyPassword(string? current, string? submitted) => submitted switch
    {
        null => current,
        "" => null,
        _ => submitted
    };

    /// <summary>
    /// PC'nin Basic Auth sirri: <c>null</c> "dokunma", <c>""</c> "iptal", deger yeni sirdir ve yalnizca HASH'i
    /// saklanir — sir geri okunamaz, eskisi aninda gecersiz olur. Bicim istek validator'unda zorlandi.
    /// </summary>
    private static string? ApplySecret(string? currentHash, string? submitted) => submitted switch
    {
        null => currentHash,
        "" => null,
        _ => DeviceCredential.Hash(submitted)
    };

    /// <summary>
    /// Sablonun pin semasini cihaza kopyalar ve kanal numarasi tasiyan her pin icin bir <c>IoChannel</c> uretir.
    ///
    /// <b>Kanallar neden burada dogar.</b> Bu olmadan SCADA ingest'inin yazacagi HICBIR SATIR olmazdi: ingest kanali
    /// <c>(CabinetId, Direction, ChannelNumber)</c> ile cozuyor ve tanimadigi kanali sessizce atliyor. Kanallari ureten
    /// baska bir yol da yok — cihaz yaratmanin tek yolu paletten birakmak.
    ///
    /// Pin ve kanalin ICERIGINI sablon, KIMLIGINI EF yazar (Guid anahtarlarda <c>SequentialGuidValueGenerator</c>; Add aninda
    /// atanir). Navigasyon uzerinden baglanir ki EF ekleme sirasini kendisi cozsun ve ayni bilgi iki yerde tutulmasin.
    /// </summary>
    private void InstantiateTemplatePins(Device device, ICollection<ComponentTemplatePin> templatePins)
    {
        // Ayni (yon, kanal numarasi) cifti TEK bir IoChannel'dir. Sablonda iki pin ayni kanali gosteriyorsa (or. bir
        // rolenin COM ve NO uclari) ikisi de ayni kanala baglanir; ayri ayri uretmek
        // IX_IoChannel_CabinetId_Direction_ChannelNumber'i ihlal ederdi.
        var channelsByAddress = new Dictionary<(PinDirection, int), IoChannel>();

        foreach (var templatePin in templatePins)
        {
            var pin = new Pin
            {
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
                        Device = device,
                        // CabinetId denormalize: benzersizlik kabin kapsamli ve bir kolon olmadan indekse dokulemezdi.
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

    private sealed record TemplateRow(int DeviceTypeId, bool IsMonitorable);

    private sealed record ChannelAddressRow(PinDirection Direction, int ChannelNumber, string DeviceName);
}
