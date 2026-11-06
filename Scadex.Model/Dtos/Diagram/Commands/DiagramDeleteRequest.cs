using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Diagram.Commands;

/// <summary>
/// Seçimi silme: <c>POST /api/Diagram/automation/{automationId}/delete</c>. Cihazın pinleri ve kabloları sunucuda birlikte kalkar;
/// istemcinin cihaza bağlı kabloları ayrıca göndermesi gerekmez. Bu otomasyonda karşılığı bulunmayan Id sessizce atlanır —
/// iki kez gönderilen bir silme hata değildir.
/// </summary>
public class DiagramDeleteRequest : IDto
{
    public List<Guid> DeviceIds { get; set; } = [];
    public List<Guid> ConnectionIds { get; set; } = [];
    public List<Guid> AnnotationIds { get; set; } = [];
}
