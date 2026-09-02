using Scadex.Core.Model;

namespace Scadex.Model.Dtos.User.Queries;

public class UserDetailDto : IDto
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
    public bool IsActive { get; set; }
}