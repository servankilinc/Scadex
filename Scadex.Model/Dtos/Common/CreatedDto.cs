using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Common;

public class CreatedDto : IDto
{
    public Guid Id { get; set; }

    public CreatedDto()
    {
    }

    public CreatedDto(Guid id) => Id = id;
}
