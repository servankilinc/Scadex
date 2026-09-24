using Scadex.Core.Model;

namespace Scadex.Model.Entities.Abstract;

/// <summary> Ağ üzerinden TCP connect ile ayakta olup olmadığı anlaşılan varlıkların ortak sözleşmesi. Örneğin <see cref="Camera"/>, <see cref="Device"/>. </summary>
public interface IMonitoredAsset : IEntity
{
    Guid Id { get; }
    
    Guid CabinetId { get; set; }
    
    string Name { get; set; }
    
    /// <summary> Eğer router veya NAT arkasında ise kabinin kendi dış IP bilgisi atanmalı port ile ulaşılır, değilse cihaz IP adresi ile ulaşılır. </summary>
    string? IpAddress { get; set; }
    
    /// <summary> Yoklama sondasının bağlanacağı TCP portu. </summary>
    int? MonitoringPort { get; set; }
    
    int? DeviceStatusId { get; set; }
    
    DateTime? LastSeen { get; set; }
    
    /// <summary> Cihaz başına yoklama periyodu (sn). Fiilî periyot worker'ın tur aralığının katına yukarı yuvarlanır. </summary>
    int PingIntervalSec { get; set; }

    bool IsMonitoringEnabled { get; set; }
    
    /// <summary> Son başarısız yoklamanın hatası; başarılı yoklamada <c>null</c>'a çekilir. </summary>
    string? LastConnectionError { get; set; }
}
