using Scadex.Core.Utils.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Automation.Commands;
using Scadex.Model.Dtos.Automation.Queries;
using Scadex.Model.Dtos.Common;

namespace Scadex.Business.Abstract;

public interface IAutomationService
{
    // List
    Task<Result<ICollection<AutomationDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    // Create
    Task<Result<CreatedDto>> CreateAsync(AutomationCreateDto request, CancellationToken cancellationToken = default);

    // Update
    Task<Result<AutomationUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(AutomationUpdateDto request, CancellationToken cancellationToken = default);
}