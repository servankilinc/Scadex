using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Role.Queries;

public class RoleDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    public bool IsActive { get; set; }
}