using Scadex.Core.Model;

namespace Scadex.Model.Dtos.RolePermission.Queries;

public class RolePermissionDto : IDto
{
    public Guid RoleId { get; set; }
    public int PermissionId { get; set; }
    public string? PermissionCode { get; set; }
    public string? PermissionDisplayName { get; set; }
    public string? PermissionCategory { get; set; }
}
