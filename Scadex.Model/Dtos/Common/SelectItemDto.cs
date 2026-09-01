using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Common;

public class SelectItemDto : IDto
{
    public string Value { get; set; } = null!;
    public string Text { get; set; } = null!;
}
