using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.DeviceCommand.Commands;
using Scadex.Model.Dtos.DeviceCommand.Queries;
using Scadex.Model.Dtos.Realtime.Queries;
using Scadex.Model.Dtos.Scada.Commands;
using Scadex.Model.Entities;
using System.Text.Json;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

/// <summary> Komutların (Scadex -> SCADA) is akışı </summary>
public partial class DeviceCommandService
{
    // kullanıcı tarafından girilen parametrelere karşı ek güvenlik amacıyla eklendi, 0, veya 900K gibi değerler girememesi adına 
    private const int MinTimeoutMs = 5000; // 5sn
    private const int MaxTimeoutMs = 180000; // 3dk

    /// <inheritdoc />
    public async Task<Result<DeviceCommandResultDto>> SendAsync(Guid deviceId, DeviceCommandSendRequest request, CancellationToken cancellationToken = default)
    {
        // 1) Validation
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<DeviceCommandResultDto>.Validation(validationResult.Failures, description: "Validation failed for DeviceCommandSendRequest");


        // 2) Komutun gönderileceği komponent bulunur
        var device = await _unitOfWork.Devices.GetAsync(
            where: d => d.Id == deviceId && d.IsActive,
            include: i => i.Include(d => d.Cabinet),
            tracking: false,
            cancellationToken: cancellationToken
        );
        if (device == null)
            return Result<DeviceCommandResultDto>.NotFound(description: "Cihaz bulunamadi veya pasif durumda");


        // 3) Komutun gönderileceği kabin bulunur
        var cabinet = device.Cabinet;
        if (cabinet == null || !cabinet.IsActive)
            return Result<DeviceCommandResultDto>.NotFound(description: "Cihazin kabini bulunamadi veya pasif durumda");


        // 4) SCADA ve kanal ile iletişim için gerekli kontroller
        if (!cabinet.ScadaIsEnabled)
            return Result<DeviceCommandResultDto>.Failure($"Bu kabinde({cabinet.Id}) SCADA kapalı; komut gönderilemez");
        if (string.IsNullOrWhiteSpace(cabinet.ScadaBaseUrl))
            return Result<DeviceCommandResultDto>.Failure($"Kabinin({cabinet.Id}) SCADA adresi tanımlı değil");
        if (request.IoChannelId is not Guid channelId)
            return Result<DeviceCommandResultDto>.Failure("Komut için hedef kanal zorunlu");

        var channel = await _unitOfWork.IoChannels.GetAsync(
            where: c => c.Id == channelId && c.DeviceId == deviceId,
            tracking: false,
            cancellationToken: cancellationToken
        );
        if (channel == null)
            return Result<DeviceCommandResultDto>.Failure($"Kanal bu cihaza ait değil", $"channel:{channelId}, device:{deviceId}");
        if (!channel.IsEnabled)
            return Result<DeviceCommandResultDto>.Failure($"Kanal devre dışı, channel:{channelId}");
        if (channel.Direction != PinDirection.Output)
            return Result<DeviceCommandResultDto>.Failure($"Komut sadece Output kanallarına gönderilebilir", $"channel:{channelId}");


        // 5) Kanalın pini çözülür (NO/NC) ve SCADA'ya gönderilecek değer belirlenir
        var polarity = await ResolvePolarityAsync(channel.Id, cancellationToken);
        if (!polarity.IsSuccess)
        {
            _logger.LogWarning(polarity.Description);
            return Result<DeviceCommandResultDto>.Failure(polarity.Description);
        }


        // 6) SCADA'ya gönderilecek değer belirlenir
        bool turnOn = polarity.Contact == PinFunction.NC ? !request.TurnOn!.Value : request.TurnOn!.Value;
        string sentValue = turnOn ? "1" : "0";


        // 7) Komut kaydı oluşturulur ve veritabanına yazdır
        var identifier = _httpContextManager.GetNameIdentifier();
        Guid? requestedByUserId = !identifier.IsSuccess ? null : (Guid.TryParse(identifier.Data, out var userId) ? userId : null);

        var command = new DeviceCommand
        {
            DeviceId = device.Id,
            IoChannelId = channel.Id,
            CommandType = request.CommandType,
            PayloadJson = JsonSerializer.Serialize(new ScadaCommandPayload(turnOn, sentValue, polarity.Contact), ProjectJsonOptions.SerializerOptions),
            Status = CommandStatus.Sent,
            RequestedByUserId = requestedByUserId,
            SentAt = DateTime.UtcNow
        };
        await _unitOfWork.DeviceCommands.AddAndSaveAsync(command, cancellationToken);


        // 8) SCADA'ya gönderilir ve sonucu beklenir
        var outcome = await _scadaCommandGateway.SendAsync(
            cabinet.ScadaBaseUrl!,
            new ScadaCommandEnvelope
            {
                CommandId = command.Id,
                CabinetId = cabinet.Id,
                ChannelNumber = channel.ChannelNumber, // karta "output=1", "output=2" olarak iletilir
                CommandType = request.CommandType,
                Value = sentValue,
                IssuedAtUtc = DateTime.UtcNow
            },
            TimeSpan.FromMilliseconds(Math.Clamp(cabinet.ScadaCommandTimeoutMs, MinTimeoutMs, MaxTimeoutMs))
        );

        // 9) Komut bilgileri SCADA'dan gelen yanıta göre güncellenir ve veritabanına yazdırılır
        command.Status = outcome.Status;
        command.ResultMessage = outcome.Message;
        command.RespondedAt = DateTime.UtcNow;
        await _unitOfWork.DeviceCommands.UpdateAndSaveAsync(command, CancellationToken.None);

        // 10) Komutun tamamlandığı, kabindeki diğer izleyici client'lara yayınlanır
        var userNameResult = _httpContextManager.GetName();
        string? requestedByName = userNameResult.IsSuccess ? userNameResult.Data : null;

        // NOT: Yayın sadece komutu gönderene degil ayni kabini izleyen herkese gider.
        await _notifier.CommandCompletedAsync(cabinet.Id, new CommandCompleted
        {
            CommandId = command.Id,
            DeviceId = device.Id,
            IoChannelId = channel.Id,
            ChannelNumber = channel.ChannelNumber,
            CommandType = command.CommandType,
            Status = command.Status,
            ResultMessage = command.ResultMessage,
            RespondedAt = command.RespondedAt,
            RequestedByName = requestedByName
        }, CancellationToken.None);

        return Result<DeviceCommandResultDto>.Success(command.ToResultDto(channel.ChannelNumber, requestedByName));
    }

    /// <inheritdoc />
    public async Task<Result<ICollection<DeviceCommandResultDto>>> GetRecentAsync(Guid deviceId, int take, CancellationToken cancellationToken = default)
    {
        int MaxHistoryTake = 100;

        var rows = await _unitOfWork.DeviceCommands.GetRecentForDeviceAsync(deviceId, Math.Clamp(take, 1, MaxHistoryTake), cancellationToken);

        ICollection<DeviceCommandResultDto> list = rows
            .Select(devCom => devCom.ToResultDto(devCom.IoChannel?.ChannelNumber, devCom.RequesterUser?.FullName ?? devCom.RequesterUser?.UserName))
            .ToList();

        return Result<ICollection<DeviceCommandResultDto>>.Success(list);
    }

    #region Command Pin Resolution
    /// <summary> Kanalin NO/NC kutbu — hangi pini bağlı? </summary>
    private async Task<PolarityResolution> ResolvePolarityAsync(Guid ioChannelId, CancellationToken cancellationToken)
    {
        var nc_no_pins = await _unitOfWork.Pins.GetAllAsync(
            select: p => new { p.Id, p.Function },
            where: p => p.IoChannelId == ioChannelId && (p.Function == PinFunction.NO || p.Function == PinFunction.NC),
            cancellationToken: cancellationToken
        ) ?? [];

        // NC/NO barındırmayan kanal: LED, duz dijital cikis vs. olabilir
        if (nc_no_pins.Count == 0)
            return PolarityResolution.Resolved(null);

        // Kanalın NC/NO pinlerinden sadece biri var
        var distinct = nc_no_pins.Select(c => c.Function).Distinct().ToList();
        if (distinct.Count == 1)
            return PolarityResolution.Resolved(distinct[0]);

        // Hem NO hem NC pini var -> bu pinlerin bağlantılarına bak.
        var nc_no_pin_ids = nc_no_pins.Select(c => c.Id).ToList();
        var wiredPinIds = await _unitOfWork.Connections.GetAllAsync(
            select: c => new { c.SourcePinId, c.TargetPinId },
            where: c => nc_no_pin_ids.Contains(c.SourcePinId) || nc_no_pin_ids.Contains(c.TargetPinId),
            cancellationToken: cancellationToken
        ) ?? [];

        // Kanaldaki NC/NO pinlerinden hangileri kablolu?
        var wired_nc_no_pin_ids = new HashSet<Guid>();
        foreach (var connection in wiredPinIds)
        {
            wired_nc_no_pin_ids.Add(connection.SourcePinId);
            wired_nc_no_pin_ids.Add(connection.TargetPinId);
        }

        // Kablolu pinlerin fonksiyonları bulunur. Eğer sadece NO veya sadece NC kabloluysa, o pin ile devam edilir.
        var wiredFunctions = nc_no_pins.Where(c => wired_nc_no_pin_ids.Contains(c.Id)).Select(c => c.Function).Distinct().ToList();
        if (wiredFunctions.Count == 1)
            return PolarityResolution.Resolved(wiredFunctions[0]);

        if (wiredFunctions.Count > 1)
            return PolarityResolution.Reject($"Hem NO hem NC pini kablolu; hangisinin yükü taşıdığı belirsiz. Kullanılmayan kabloyu kaldırın. Kanal {ioChannelId}");

        return PolarityResolution.Reject($"Output pini çözülemedi. Kanal {ioChannelId}");
    }

    /// <summary> NC/NO çözümlemesinin sonucu. </summary>
    private readonly record struct PolarityResolution(bool IsSuccess, PinFunction? Contact, string? Description)
    {
        public static PolarityResolution Resolved(PinFunction? contact) => new(true, contact, null);
        public static PolarityResolution Reject(string description) => new(false, null, description);
    }
    #endregion 
}