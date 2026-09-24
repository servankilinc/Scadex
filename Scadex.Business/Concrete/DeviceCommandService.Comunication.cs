using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Business.Utils.ScadaObserver;
using Scadex.Core.Utils;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.DeviceCommand.Commands;
using Scadex.Model.Dtos.DeviceCommand.Queries;
using Scadex.Model.Dtos.Realtime.Queries;
using Scadex.Model.Dtos.Scada.Commands;
using Scadex.Model.Dtos.Scada.Events;
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
        var polarity = await _polarityResolver.ResolveAsync(channel.Id, cancellationToken);
        if (!polarity.IsSuccess)
        {
            _logger.LogWarning(polarity.Description);
            return Result<DeviceCommandResultDto>.Failure(polarity.Description);
        }


        // 6) SCADA'ya gönderilecek değer belirlenir
        bool turnOn = polarity.ToPhysical(request.TurnOn!.Value);
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
                // Pin = ScadaPinAddress.Format(channel.Direction, channel.ChannelNumber), // "OUT1", "OUT2" gibi SCADA'ya iletilir
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

        // 9.0) Komutun sonucu SCADA kartının omline olup olmadığı çıkar: yanıt verdiyse temas, zaman aşımı / bağlantı hatası ise kayıp.
        if (outcome.Status == CommandStatus.Succeeded)
            await _cabinetStatusService.RecordScadaContactAsync(cabinet.Id, CancellationToken.None);
        else if (outcome.Status == CommandStatus.NoResponse)
            await _cabinetStatusService.RecordScadaUnreachableAsync(cabinet.Id, outcome.Message, CancellationToken.None);

        // 9.1) Başarılı komut, kanalın mevcut değerini yazar; başarısız/zaman aşımı değeri değiştirmez.
        ChannelValueChange? channelChange = null;
        ChannelChangedNotification? observerNotification = null;

        if (outcome.Status == CommandStatus.Succeeded)
        {
            var now = command.RespondedAt.Value;

            var previousValue = await _unitOfWork.IoChannels.GetAsync(select: c => c.CurrentValue, where: c => c.Id == channel.Id, cancellationToken: CancellationToken.None);
            bool changed = await _unitOfWork.IoChannels.SetCurrentValueIfChangedAsync(channel.Id, sentValue, now, CancellationToken.None);

            if (changed)
            {
                observerNotification = new ChannelChangedNotification
                {
                    CabinetId = cabinet.Id,
                    IoChannelId = channel.Id,
                    DeviceId = channel.DeviceId,
                    Direction = channel.Direction,
                    ChannelNumber = channel.ChannelNumber,
                    Value = sentValue,
                    PreviousValue = previousValue,
                    TurnOn = request.TurnOn!.Value,
                    OccurredAtUtc = now,
                    ReceivedAtUtc = now
                };

                channelChange = new ChannelValueChange
                {
                    IoChannelId = channel.Id,
                    DeviceId = channel.DeviceId,
                    ChannelNumber = channel.ChannelNumber,
                    Value = sentValue,
                    UpdatedAt = now
                };
            }
        }

        // 9.2) Kanal değeri değiştiyse izleyen client'lara bildirilir
        if (channelChange != null)
            await _notifier.ChannelValuesChangedAsync(cabinet.Id, [channelChange], CancellationToken.None);

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

        // 11) Değişen çıkış kanalı, Scadex'in dış modüllerine (dinleyicilere) yayınlanır
        if (observerNotification != null)
            await _observers.PublishAsync(o => o.OnChannelChangedAsync(observerNotification, CancellationToken.None), _logger, nameof(IScadaEventObserver.OnChannelChangedAsync));

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

}