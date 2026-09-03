using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.Company.Commands;
using Scadex.Model.Dtos.Company.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface ICompanyService
{
    // Get
    Task<Result<Company>> GetAsync(Expression<Func<Company, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<Company>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<CompanyDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<Company>>> GetListAsync(Expression<Func<Company, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<Company>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<CompanyDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    // SelectList
    Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<Company, bool>>? where = default, CancellationToken cancellationToken = default);

    // Create
    Task<Result> CreateAsync(CompanyCreateDto request, CancellationToken cancellationToken = default);

    // Update
    Task<Result<CompanyUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(CompanyUpdateDto request, CancellationToken cancellationToken = default);

    // Pagination / Datatable
    Task<Result<PaginationResponse<CompanyDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseClientSide<CompanyDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseServerSide<CompanyDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
}