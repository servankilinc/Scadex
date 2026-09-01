using Scadex.Core.Model;

namespace Scadex.Model.Dtos.ComponentTemplate.Queries;

/// <summary>Yuklenen ComponentTemplate gorselinin sonucu. </summary>
public class TemplateImageDto : IDto
{
    public string Url { get; set; } = null!;
}
