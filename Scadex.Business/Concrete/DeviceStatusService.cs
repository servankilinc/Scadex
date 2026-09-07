using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.DeviceStatus.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public class DeviceStatusService : IDeviceStatusService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMapper _mapper;
    public DeviceStatusService(IUnitOfWork unitOfWork, IValidationService validationService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mapper = mapper;
    }

    #region Get
    public async Task<Result<DeviceStatus>> GetAsync(Expression<Func<DeviceStatus, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceStatuses.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DeviceStatus>.NotFound();
        return Result<DeviceStatus>.Success(result);
    }

    public async Task<Result<DeviceStatus>> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceStatuses.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DeviceStatus>.NotFound();
        return Result<DeviceStatus>.Success(result);
    }

    public async Task<Result<DeviceStatusDto>> GetBaseAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceStatuses.GetAsync<DeviceStatusDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DeviceStatusDto>.NotFound();
        return Result<DeviceStatusDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<DeviceStatus>>> GetListAsync(Expression<Func<DeviceStatus, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceStatuses.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DeviceStatus>>.NotFound();
        return Result<ICollection<DeviceStatus>>.Success(result);
    }

    public async Task<Result<ICollection<DeviceStatus>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceStatuses.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DeviceStatus>>.NotFound();
        return Result<ICollection<DeviceStatus>>.Success(result);
    }

    public async Task<Result<ICollection<DeviceStatusDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceStatuses.GetAllAsync<DeviceStatusDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DeviceStatusDto>>.NotFound();
        return Result<ICollection<DeviceStatusDto>>.Success(result);
    }
    #endregion

    #region SelectList
    public async Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<DeviceStatus, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var list = await _unitOfWork.DeviceStatuses.GetAllAsync<SelectItemDto>(select: s => new SelectItemDto { Value = s.Id.ToString(), Text = s.Name }, where: where, cancellationToken: cancellationToken);
        var selectList = list ?? new List<SelectItemDto>();
        return Result<ICollection<SelectItemDto>>.Success(selectList);
    }
    #endregion

    #region Pagination / Datatable
    public async Task<Result<PaginationResponse<DeviceStatusDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceStatuses.PaginationAsync<DeviceStatusDto>(configurationProvider: _mapper.ConfigurationProvider, paginationRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<PaginationResponse<DeviceStatusDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseClientSide<DeviceStatusDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceStatuses.DatatableClientSideAsync<DeviceStatusDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<DatatableResponseClientSide<DeviceStatusDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseServerSide<DeviceStatusDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceStatuses.DatatableServerSideAsync<DeviceStatusDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<DatatableResponseServerSide<DeviceStatusDto>>.Success(result);
    }
    #endregion
}