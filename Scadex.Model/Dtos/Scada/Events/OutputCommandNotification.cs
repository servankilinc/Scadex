using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Scada.Events;

/// <summary>
/// Bir çıkış komutu sonuçlandı (<c>IDeviceCommandService.SendAsync</c>) — başarılı ya da başarısız, kanal değeri değişsin değişmesin.
/// Gözlemcilere (<c>IScadaEventObserver.OnOutputCommandCompletedAsync</c>) yayınlanır.
/// </summary>
/// <remarks>
/// <see cref="ChannelChangedNotification"/>'dan farkı: o yalnızca BAŞARILI ve değeri DEĞİŞTİREN komutta gelir, kimin gönderdiğini
/// taşımaz. Bu ise komutun kendisidir; <see cref="RequestedByUserId"/> komut bir HTTP isteğinden (sanal kabin, diyagram) geldiyse doludur,
/// arka plan işlerinin (ör. sinyalizasyon motoru) gönderdiği komutta <c>null</c>'dır.
/// </remarks>
public class OutputCommandNotification : IDto
{
    public Guid CabinetId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid IoChannelId { get; set; }
    public Guid CommandId { get; set; }

    /// <summary> İstenen MANTIKSAL değer (NO/NC çevrilmeden önce). </summary>
    public bool TurnOn { get; set; }

    public CommandStatus Status { get; set; }
    public string? ResultMessage { get; set; }

    /// <summary> Komutu gönderen kullanıcı; arka plan işinin komutunda <c>null</c>. </summary>
    public Guid? RequestedByUserId { get; set; }

    /// <summary> Komutun sonuçlandığı an (<c>DeviceCommand.RespondedAt</c>). </summary>
    public DateTime OccurredAtUtc { get; set; }
}
