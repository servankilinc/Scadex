using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Cabinet.Commands;
using Scadex.Model.Dtos.Cabinet.Queries;
using Scadex.Model.Dtos.Common;

namespace Scadex.Business.Abstract;

public interface ICabinetService
{
    // List
    Task<Result<ICollection<CabinetDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    // Create
    Task<Result<CreatedDto>> CreateAsync(CabinetCreateDto request, CancellationToken cancellationToken = default);

    // Update
    Task<Result<CabinetUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(CabinetUpdateDto request, CancellationToken cancellationToken = default);
}