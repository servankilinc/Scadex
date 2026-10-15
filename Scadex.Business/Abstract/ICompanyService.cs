using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Company.Commands;
using Scadex.Model.Dtos.Company.Queries;

namespace Scadex.Business.Abstract;

public interface ICompanyService
{
    // List
    Task<Result<ICollection<CompanyDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    // Create
    Task<Result> CreateAsync(CompanyCreateDto request, CancellationToken cancellationToken = default);

    // Update
    Task<Result> UpdateAsync(CompanyUpdateDto request, CancellationToken cancellationToken = default);
}