using Scadex.Core.Model;
using Scadex.Model.Enums;

namespace Scadex.Model.Realtime.Notifications;

/// <summary>
/// Komutu gonderen zaten HTTP yanitinda sonucu aliyor ancak bu event ayni otomasyonu izleyen DIGER kullanicilar da giden komutun cevabını görebilsindel diye var.
/// Kanal DEGERI bu olayla gelmez: SCADA komutu uyguladiginda degisen deger normal ingest yoluyla <see cref="ChannelValueChangeNotify"/> olarak gelir.
/// </summary>
public class CommandCompletedNotify : IDto
{
    public Guid CommandId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? IoChannelId { get; set; }
    public int? ChannelNumber { get; set; }
    public EntityEnums.CommandStatus Status { get; set; }
    public string? ResultMessage { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? RequestedByName { get; set; }
}
