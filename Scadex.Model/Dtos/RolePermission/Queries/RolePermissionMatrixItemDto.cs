using Scadex.Core.Model;

namespace Scadex.Model.Dtos.RolePermission.Queries;

/// <summary> Yetki matrisinin bir satırı: bir rol ve sahip olduğu izinlerin kimlikleri (<c>GET /api/RolePermission/matrix</c>).(pasifler ve izinsizler dahil) tam bir kez yer alır. </summary>
public class RolePermissionMatrixItemDto : IDto
{
    public Guid RoleId { get; set; }
    public ICollection<int> PermissionIds { get; set; } = new List<int>();
}
