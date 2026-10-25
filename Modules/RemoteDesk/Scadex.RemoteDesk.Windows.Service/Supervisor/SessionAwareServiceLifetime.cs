using System.ServiceProcess;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;

namespace Scadex.RemoteDesk.Windows.Service.Supervisor;

/// <summary> SCM'nin bildirdiği oturum olayı (oturum aç/kapat, kilit, RDP bağlan/kes, konsol bağlan/kes). </summary>
public readonly record struct SessionChange(SessionChangeReason Reason, int SessionId);

/// <summary> Servis yaşam döngüsünden gözetmene oturum olaylarını taşıyan kuyruk (tek okuyucu). </summary>
public sealed class SessionChangeQueue
{
    private readonly Channel<SessionChange> _channel = Channel.CreateUnbounded<SessionChange>(new UnboundedChannelOptions { SingleReader = true });

    public void Post(SessionChange change) => _channel.Writer.TryWrite(change);

    public ChannelReader<SessionChange> Reader => _channel.Reader;
}

/// <summary>
/// <c>WindowsServiceLifetime</c> + oturum olayları: SCM bunları ancak servis <c>CanHandleSessionChangeEvent</c> derse gönderir ve standart
/// yaşam döngüsü bunu açmaz. Olaylar yalnızca kuyruğa bırakılır; kararı <see cref="SessionSupervisorService"/> verir.
/// </summary>
public sealed class SessionAwareServiceLifetime : WindowsServiceLifetime
{
    private readonly SessionChangeQueue _changes;

    public SessionAwareServiceLifetime(IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory,
        IOptions<HostOptions> optionsAccessor, IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor, SessionChangeQueue changes)
        : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptionsAccessor)
    {
        _changes = changes;
        CanHandleSessionChangeEvent = true;
    }

    protected override void OnSessionChange(SessionChangeDescription changeDescription)
    {
        _changes.Post(new SessionChange(changeDescription.Reason, changeDescription.SessionId));
        base.OnSessionChange(changeDescription);
    }
}
