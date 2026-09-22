using Microsoft.EntityFrameworkCore;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.DeviceCommand.Commands;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Model.Dtos.Config.Commands;
using Scadex.Signalization.Model.Dtos.Config.Queries;
using Scadex.Signalization.Model.Utils;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Signalization.Services.Concrete;

/// <summary>
/// Sinyalizasyon modülünden gelen manuel komut.
/// SignalR yayını için burada bir sey tetiklenmez: başarılı komut scadex çekirdeğinde <c>IScadaEventObserver</c> ile modülü haberdar eder modül değişimi SignalR ile yayınlar
/// </summary>

public partial class SignalCabinetService
{
    /// <inheritdoc />
    public async Task<Result<SignalCabinetCommandResultDto>> SendCommandAsync(Guid cabinetId, SignalCabinetCommandRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<SignalCabinetCommandResultDto>.Validation(validationResult.Failures, description: "Validation failed for SignalCabinetCommandRequest");

        bool cabinetExists = await _unitOfWork.Cabinets.IsExistAsync(where: c => c.Id == cabinetId, cancellationToken: cancellationToken);
        if (!cabinetExists)
            return Result<SignalCabinetCommandResultDto>.NotFound(message: "Kabin bulunamadı.");

        return request.Target switch
        {
            SignalCabinetOutput.Siren => await SendSirenCommandAsync(cabinetId, request.TurnOn, cancellationToken),
            SignalCabinetOutput.OuterDoorLight => await SendLightCommandAsync(cabinetId, request.TargetId!.Value, request.TurnOn, cancellationToken),
            SignalCabinetOutput.InnerDoorLock => await SendLockCommandAsync(cabinetId, request.TargetId!.Value, request.TurnOn, cancellationToken),
            _ => Result<SignalCabinetCommandResultDto>.Failure("Geçersiz komut hedefi.")
        };
    }

    #region Hedef Cihaz Metodu

    private async Task<Result<SignalCabinetCommandResultDto>> SendSirenCommandAsync(Guid cabinetId, bool turnOn, CancellationToken cancellationToken)
    {
        var cabinet = await _db.Cabinets.AsNoTracking().FirstOrDefaultAsync(c => c.CabinetId == cabinetId, cancellationToken);
        if (cabinet?.SirenIoChannelId is not Guid sirenChannelId)
            return Result<SignalCabinetCommandResultDto>.Failure("Bu kabinde siren tanımlı değil.");

        var outcome = await SendOutputAsync(sirenChannelId, turnOn, cancellationToken);
        var siren = await _signalizationChannelState.GetSirenStateAsync(cabinet, cancellationToken);

        return Ok(SignalCabinetOutput.Siren, targetId: null, outcome, siren);
    }

    private async Task<Result<SignalCabinetCommandResultDto>> SendLightCommandAsync(Guid cabinetId, Guid outerDoorId, bool turnOn, CancellationToken cancellationToken)
    {
        var outer = await _db.OuterDoors.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == outerDoorId && d.IsActive && d.CabinetId == cabinetId, cancellationToken);

        if (outer == null)
            return Result<SignalCabinetCommandResultDto>.NotFound(message: "Dış kapı bulunamadı.");

        if (outer.LightIoChannelId is not Guid lightChannelId)
            return Result<SignalCabinetCommandResultDto>.Failure("Bu dış kapıya aydınlatma tanımlı değil.");

        var outcome = await SendOutputAsync(lightChannelId, turnOn, cancellationToken);
        var light = await _signalizationChannelState.GetLightStateAsync(outer, cancellationToken);

        return Ok(SignalCabinetOutput.OuterDoorLight, outerDoorId, outcome, light);
    }

    /// <param name="unlock"> <c>true</c> kilidi ac, <c>false</c> kilitle. </param>
    private async Task<Result<SignalCabinetCommandResultDto>> SendLockCommandAsync(Guid cabinetId, Guid innerDoorId, bool unlock, CancellationToken cancellationToken)
    {
        var inner = await _db.InnerDoors.AsNoTracking().Include(i => i.OuterDoor)
            .FirstOrDefaultAsync(i => i.Id == innerDoorId && i.IsActive && i.OuterDoor!.IsActive && i.OuterDoor.CabinetId == cabinetId, cancellationToken);

        if (inner == null)
            return Result<SignalCabinetCommandResultDto>.NotFound(message: "İç kapı bulunamadı.");

        // Kilidi acmak icin hangi fiziksel degerin gidecegi kilidin kendi mantigidir (fail-secure / fail-safe).
        bool turnOn = unlock ? inner.UnlockTurnsOn : !inner.UnlockTurnsOn;
        var outcome = await SendOutputAsync(inner.LockIoChannelId, turnOn, cancellationToken);
        var lockState = await _signalizationChannelState.GetLockStateAsync(inner, cancellationToken);

        return Ok(SignalCabinetOutput.InnerDoorLock, innerDoorId, outcome, lockState);
    }

    #endregion

    #region Helpers

    private readonly record struct OutputCommandOutcome(bool IsSuccess, Guid? CommandId, CommandStatus Status, string? Message);

    /// <summary>
    /// Bir cikis kanalina komut gonderir; cihaz kanaldan turetilir. Retry YOKTUR — tekrarlanan role darbesi basarisiz
    /// komuttan kotudur (motorla ayni kural).
    /// </summary>
    private async Task<OutputCommandOutcome> SendOutputAsync(Guid ioChannelId, bool turnOn, CancellationToken cancellationToken)
    {
        var channel = await _unitOfWork.IoChannels.GetAsync(
            select: c => new { c.DeviceId },
            where: c => c.Id == ioChannelId,
            cancellationToken: cancellationToken
        );

        if (channel == null)
            return new OutputCommandOutcome(false, null, CommandStatus.Failed, $"Kanal bulunamadı ({ioChannelId})");

        var result = await _deviceCommandService.SendAsync(channel.DeviceId, new DeviceCommandSendRequest
        {
            CommandType = DeviceCommandType.SetOutput,
            IoChannelId = ioChannelId,
            TurnOn = turnOn
        }, cancellationToken);

        if (!result.IsSuccess)
            return new OutputCommandOutcome(false, null, CommandStatus.Failed, result.Error.Description ?? result.Message);

        return result.Data.Status == CommandStatus.Succeeded
            ? new OutputCommandOutcome(true, result.Data.Id, CommandStatus.Succeeded, null)
            : new OutputCommandOutcome(false, result.Data.Id, result.Data.Status, result.Data.ResultMessage);
    }

    /// <summary> Sahanin cevabi HTTP koduyla degil govdedeki <c>Status</c> ile bildirilir (cekirdegin komut ucuyla ayni). </summary>
    private static Result<SignalCabinetCommandResultDto> Ok(SignalCabinetOutput target, Guid? targetId, OutputCommandOutcome outcome, ChannelReading reading)
    {
        var model = new SignalCabinetCommandResultDto
        {
            Target = target,
            TargetId = targetId,
            CommandId = outcome.CommandId,
            Status = outcome.Status,
            ResultMessage = outcome.Message,
            Applied = outcome.IsSuccess,
            IsOn = reading.IsOn,
            ChangedAtUtc = reading.ChangedAtUtc
        };
        return Result<SignalCabinetCommandResultDto>.Success(model);
    }
    #endregion
}
