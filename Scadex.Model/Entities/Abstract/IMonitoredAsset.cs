using Scadex.Core.Model;

namespace Scadex.Model.Entities.Abstract;

/// <summary> Ağ üzerinden ping / TCP connect ayakta olup olmadığı anlaşılan varlıkların ortak sözleşmesi. Örneğin <see cref="Camera"/>. </summary>
public interface IMonitoredAsset : IEntity
{
    Guid Id { get; }
    Guid CabinetId { get; set; }
    string Name { get; set; }
    /// <summary> Eğer router veya NAT arkasında ise kabinin kendi dış IP bilgisi atanmalı port ile ulaşılır, değilse cihaz IP adresi ile ulaşılır. </summary>
    string IpAddress { get; set; }
    /// <summary> Yoklama sondasının bağlanacağı TCP portu; saf ICMP ping kullanılacaksa <c>null</c>. bırakılır. </summary>
    int? MonitoringPort { get; set; }
    int? DeviceStatusId { get; set; }
    DateTime? LastSeen { get; set; }
    int PingIntervalSec { get; set; }
    bool IsMonitoringEnabled { get; set; }
    string? LastConnectionError { get; set; }
}
