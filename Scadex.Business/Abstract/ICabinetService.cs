using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Cabinet.Commands;
using Scadex.Model.Dtos.Cabinet.Queries;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface ICabinetService
{
    // Get
    Task<Result<Cabinet>> GetAsync(Expression<Func<Cabinet, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<Cabinet>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<CabinetBaseDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<CabinetDetailDto>> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<Cabinet>>> GetListAsync(Expression<Func<Cabinet, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<Cabinet>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<CabinetBaseDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<CabinetDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    // SelectList
    Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<Cabinet, bool>>? where = default, CancellationToken cancellationToken = default);

    // Create
    Task<Result<CreatedDto>> CreateAsync(CabinetCreateDto request, CancellationToken cancellationToken = default);

    // Update
    Task<Result<CabinetUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(CabinetUpdateDto request, CancellationToken cancellationToken = default);

    // Pagination / Datatable
    Task<Result<PaginationResponse<CabinetDetailDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseClientSide<CabinetDetailDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseServerSide<CabinetDetailDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
}