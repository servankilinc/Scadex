using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Diagram.Queries.Items;

/// <summary> SCADA'nın <c>DeviceId</c> header'ında gönderdiği MAC adresi (<c>null</c> = kart bağlanamaz) ve kartın SCADA ayarları. </summary>
public class DiagramControlCardDto : IDto
{
    public string? MacAddress { get; set; }
    public string? ScadaBaseUrl { get; set; }
    public bool ScadaIsEnabled { get; set; }
    public int ScadaCommandTimeoutMs { get; set; }
    /// <summary> Salt okunur telemetri; istekte yoktur. </summary>
    public DateTime? ScadaLastIngestAt { get; set; }
}
