using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.DeviceType.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IDeviceTypeService
{
    // Get
    Task<Result<DeviceType>> GetAsync(Expression<Func<DeviceType, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<DeviceType>> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<DeviceTypeDto>> GetBaseAsync(int id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<DeviceType>>> GetListAsync(Expression<Func<DeviceType, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<DeviceType>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<DeviceTypeDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    // SelectList
    Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<DeviceType, bool>>? where = default, CancellationToken cancellationToken = default);

    // Pagination / Datatable
    Task<Result<PaginationResponse<DeviceTypeDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseClientSide<DeviceTypeDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseServerSide<DeviceTypeDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
}