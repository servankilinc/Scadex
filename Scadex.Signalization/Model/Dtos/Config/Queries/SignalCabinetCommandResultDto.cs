using Scadex.Core.Model;
using Scadex.Signalization.Enums;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Signalization.Model.Dtos.Config.Queries;

/// <summary> Manuel gonderilen komutun sonucu. Sxadex çekirdeğinin  </summary>
public class SignalCabinetCommandResultDto : IDto
{
    public SignalCabinetOutput Target { get; set; }
    public Guid? TargetId { get; set; }

    /// <summary> Cekirdekteki <c>DeviceCommand.Id</c>; komut kaydi hic olusmadiysa <c>null</c>. </summary>
    public Guid? CommandId { get; set; }

    public CommandStatus Status { get; set; }
    public string? ResultMessage { get; set; }

    /// <summary> Komut sahada basarili oldu mu (kanalin degeri cekirdekte guncellendi). </summary>
    public bool Applied { get; set; }

    /// <summary> Komuttan SONRAKI durum (kanaldan okunur): siren caliyor / LED yaniyor / kilit acik. Basarisizsa degismemis haldir; bilinmiyorsa <c>null</c> </summary>
    public bool? IsOn { get; set; }

    public DateTime? ChangedAtUtc { get; set; }
}
