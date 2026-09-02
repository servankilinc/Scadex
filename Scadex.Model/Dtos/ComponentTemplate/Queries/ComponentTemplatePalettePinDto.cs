using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.ComponentTemplate.Queries;

/// <summary>
/// Palet sablonunun pin semasindaki tek bir pin.
/// Buradaki alanlar tam olarak istemcinin cihazi canvas'ta cizmek ve pinlerini urettikten sonra kablo dogrulamasi yapmak icin ihtiyac duyduklaridir bilgilerdir.
/// Kaydedildikten sonra <see cref="Diagram.Queries.DiagramPinDto"/> olarak diyagramda kullanilir.
/// </summary>
public class ComponentTemplatePalettePinDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public double RelativeX { get; set; }
    public double RelativeY { get; set; }
    public HandleSide Side { get; set; }
    public PinFunction Function { get; set; }
    public PinDirection Direction { get; set; }
    public VoltageLevel? VoltageLevel { get; set; }
    public int? ChannelNumber { get; set; }
}
