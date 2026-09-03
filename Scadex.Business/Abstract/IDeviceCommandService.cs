using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.DeviceCommand.Commands;
using Scadex.Model.Dtos.DeviceCommand.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IDeviceCommandService
{
    Task<Result<DeviceCommandResultDto>> SendAsync(Guid deviceId, DeviceCommandSendRequest request, CancellationToken cancellationToken = default);
    /// <summary>Cihazin son komutları, yeniden eskiye.</summary>
    Task<Result<ICollection<DeviceCommandResultDto>>> GetRecentAsync(Guid deviceId, int take, CancellationToken cancellationToken = default);


    // Get
    Task<Result<DeviceCommand>> GetAsync(Expression<Func<DeviceCommand, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<DeviceCommand>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<DeviceCommandDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<DeviceCommand>>> GetListAsync(Expression<Func<DeviceCommand, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<DeviceCommand>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<DeviceCommandDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    // SelectList
    Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<DeviceCommand, bool>>? where = default, CancellationToken cancellationToken = default);

    // Create
    Task<Result> CreateAsync(DeviceCommandCreateDto request, CancellationToken cancellationToken = default);

    // Update
    Task<Result<DeviceCommandUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(DeviceCommandUpdateDto request, CancellationToken cancellationToken = default);

    // Delete / Restore
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    // Pagination / Datatable
    Task<Result<PaginationResponse<DeviceCommandDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseClientSide<DeviceCommandDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseServerSide<DeviceCommandDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
}