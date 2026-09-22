using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Hubs;

namespace Scadex.Signalization.Realtime;

public class SignalizationNotifier : ISignalizationNotifier
{
    private readonly IHubContext<SignalizationHub, ISignalizationHubClientContract> _hub;
    private readonly ILogger<SignalizationNotifier> _logger;

    public SignalizationNotifier(IHubContext<SignalizationHub, ISignalizationHubClientContract> hub, ILogger<SignalizationNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task OperatorSessionChangedAsync(OperatorSessionChangedMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients.Group(SignalizationHub.SessionsGroupName).OperatorSessionChanged(message);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Canli yayin basarisiz: OperatorSessionChangedAsync, islem {SessionId}, kabin {CabinetId}", message.SessionId, message.CabinetId);
        }
    }

    public async Task SignalCabinetStateChangedAsync(SignalCabinetStateChangedMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients.Group(SignalizationHub.CabinetGroupName(message.CabinetId)).SignalCabinetStateChanged(message);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Canli yayin basarisiz: SignalCabinetStateChangedAsync, kabin {CabinetId}, hedef {Target}", message.CabinetId, message.Target);
        }
    }

    public async Task SignalDoorSwitchChangedAsync(SignalDoorSwitchChangedMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients.Group(SignalizationHub.CabinetGroupName(message.CabinetId)).SignalDoorSwitchChanged(message);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Canli yayin basarisiz: SignalDoorSwitchChangedAsync, kabin {CabinetId}, kapi {DoorId}", message.CabinetId, message.DoorId);
        }
    }
}
