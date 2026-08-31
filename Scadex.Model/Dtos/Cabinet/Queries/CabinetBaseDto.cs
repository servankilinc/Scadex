using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Cabinet.Queries;

public class CabinetBaseDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }
}