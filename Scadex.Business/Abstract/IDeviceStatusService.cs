using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.DeviceStatus.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IDeviceStatusService
{
    // Get
    Task<Result<DeviceStatus>> GetAsync(Expression<Func<DeviceStatus, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<DeviceStatus>> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<DeviceStatusDto>> GetBaseAsync(int id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<DeviceStatus>>> GetListAsync(Expression<Func<DeviceStatus, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<DeviceStatus>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<DeviceStatusDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    // SelectList
    Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<DeviceStatus, bool>>? where = default, CancellationToken cancellationToken = default);

    // Pagination / Datatable
    Task<Result<PaginationResponse<DeviceStatusDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseClientSide<DeviceStatusDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseServerSide<DeviceStatusDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
}