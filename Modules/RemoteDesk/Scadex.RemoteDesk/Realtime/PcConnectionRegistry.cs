using System.Collections.Concurrent;
using Scadex.RemoteDesk.Contracts.Media;

namespace Scadex.RemoteDesk.Realtime;

/// <summary> Kabul edilmiş bir Windows istemcisi bağlantısı. Tabloya YAZILMAZ — bağlantı durumu, sürüm ve monitörler bellektedir). </summary>
public sealed record PcConnection(
    Guid DeviceId,
    string ConnectionId,
    string ClientVersion,
    string OsVersion,
    string MachineName,
    string UserName,
    string? RemoteIp,
    IReadOnlyList<string> MacAddresses,
    IReadOnlyList<MonitorInfo> Monitors,
    DateTime ConnectedUtc);

/// <summary>
/// <c>DeviceId → bağlantı</c>, SINGLETON. Tek sunucu varsayımı.
/// Burada tutulan, PC'deki <b>Windows Pc'nin</b> bağlantısıdır — izleyiciler DEĞİL: aynı PC'yi istediği kadar operatör aynı anda izleyebilir.
/// PC başına tek istemci bağlantısı: <b>ilk bağlanan kazanır</b>; kopan bağlantı SignalR zaman aşımıyla (≈30 sn) düşünce yer açılır.
/// </summary>
public sealed class PcConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, PcConnection> _byDevice = new();

    /// <summary> Cihazda başka bir bağlantı varsa <c>false</c> (aynı bağlantının tekrar kaydı başarılıdır). </summary>
    public bool TryRegister(PcConnection connection)
    {
        var stored = _byDevice.GetOrAdd(connection.DeviceId, connection);
        return stored.ConnectionId == connection.ConnectionId;
    }

    public bool TryGet(Guid deviceId, out PcConnection connection) => _byDevice.TryGetValue(deviceId, out connection!);

    public void UpdateMonitors(Guid deviceId, string connectionId, IReadOnlyList<MonitorInfo> monitors)
    {
        if (_byDevice.TryGetValue(deviceId, out var current) && current.ConnectionId == connectionId)
            _byDevice.TryUpdate(deviceId, current with { Monitors = monitors }, current);
    }

    /// <summary> Yalnızca kayıt HÂLÂ bu bağlantıya aitse siler (eski bağlantının geç gelen kopuşu yenisini silmesin). </summary>
    public bool Remove(Guid deviceId, string connectionId)
    {
        return _byDevice.TryGetValue(deviceId, out var current)
            && current.ConnectionId == connectionId
            && _byDevice.TryRemove(new KeyValuePair<Guid, PcConnection>(deviceId, current));
    }
}
