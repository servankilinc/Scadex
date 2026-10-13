using Scadex.Core.Model;
using Scadex.Model.Enums;

namespace Scadex.Model.Dtos.Realtime.Queries;

public class ChannelValueChange : IDto
{
    public Guid IoChannelId { get; set; }
    public Guid DeviceId { get; set; }
    public int ChannelNumber { get; set; }
    public string? Value { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DeviceStatusChange : IDto
{
    public Guid DeviceId { get; set; }
    public EntityEnums.DeviceStatus? StatusId { get; set; }
    public DateTime? LastSeen { get; set; }
}

public class CabinetStatusChange : IDto
{
    public Guid CabinetId { get; set; }
    public EntityEnums.DeviceStatus? StatusId { get; set; }
    public DateTime? LastSeen { get; set; }
    public DateTime? ScadaLastIngestAt { get; set; }
}

/// <summary>
/// Komutu gonderen zaten HTTP yanitinda sonucu aliyor ancak bu event ayni kabini izleyen DIGER kullanicilar da giden komutun cevabını görebilsindel diye var.
/// Kanal DEGERI bu olayla gelmez: SCADA komutu uyguladiginda degisen deger normal ingest yoluyla <see cref="ChannelValueChange"/> olarak gelir.
/// </summary>
public class CommandCompleted : IDto
{
    public Guid CommandId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? IoChannelId { get; set; }
    public int? ChannelNumber { get; set; }
    public EntityEnums.DeviceCommandType CommandType { get; set; }
    public EntityEnums.CommandStatus Status { get; set; }
    public string? ResultMessage { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? RequestedByName { get; set; }
}

/// <summary> Kontrol modülüne bağlı kart okuyucuda kart okutuldu (<c>POST /api/Scada/card</c>) okunan kartı bu yayından gösterir. Kart tanımsız da olsa yayınlanır </summary>
public class CardRead : IDto
{
    public Guid CabinetId { get; set; }

    /// <summary> SCADA'nın gönderdiği ham kart numarası. </summary>
    public string CardId { get; set; } = null!;

    /// <summary> Kart aktif bir kullanıcıya tanımlıysa sahibi; değilse <c>null</c>. </summary>
    public Guid? UserId { get; set; }
    public string? UserFullName { get; set; }

    /// <summary> SCADA'nın bildirdiği okuma anı (göndermediyse bize ulaştığı an). </summary>
    public DateTime OccurredAtUtc { get; set; }
}
