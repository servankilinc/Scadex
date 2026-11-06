using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Dtos.Authority.Queries;

public class SignalAuthorityDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid RoleId { get; set; }
    public string? RoleName { get; set; }

    /// <summary> Pasif rol yetki turetmez: kurum aktif olsa da rolu pasifse kimse bu kurumla kapi acamaz. </summary>
    public bool RoleIsActive { get; set; }
    public bool IsActive { get; set; }
}
