using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.DeviceType.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public class DeviceTypeService : IDeviceTypeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMapper _mapper;
    public DeviceTypeService(IUnitOfWork unitOfWork, IValidationService validationService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mapper = mapper;
    }

    #region Get
    public async Task<Result<DeviceType>> GetAsync(Expression<Func<DeviceType, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceTypes.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DeviceType>.NotFound();
        return Result<DeviceType>.Success(result);
    }

    public async Task<Result<DeviceType>> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceTypes.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DeviceType>.NotFound();
        return Result<DeviceType>.Success(result);
    }

    public async Task<Result<DeviceTypeDto>> GetBaseAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceTypes.GetAsync<DeviceTypeDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DeviceTypeDto>.NotFound();
        return Result<DeviceTypeDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<DeviceType>>> GetListAsync(Expression<Func<DeviceType, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceTypes.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DeviceType>>.NotFound();
        return Result<ICollection<DeviceType>>.Success(result);
    }

    public async Task<Result<ICollection<DeviceType>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceTypes.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DeviceType>>.NotFound();
        return Result<ICollection<DeviceType>>.Success(result);
    }

    public async Task<Result<ICollection<DeviceTypeDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceTypes.GetAllAsync<DeviceTypeDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DeviceTypeDto>>.NotFound();
        return Result<ICollection<DeviceTypeDto>>.Success(result);
    }
    #endregion

    #region SelectList
    public async Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<DeviceType, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var list = await _unitOfWork.DeviceTypes.GetAllAsync<SelectItemDto>(select: s => new SelectItemDto { Value = s.Id.ToString(), Text = s.Name }, where: where, cancellationToken: cancellationToken);
        var selectList = list ?? new List<SelectItemDto>();
        return Result<ICollection<SelectItemDto>>.Success(selectList);
    }
    #endregion

    #region Pagination / Datatable
    public async Task<Result<PaginationResponse<DeviceTypeDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceTypes.PaginationAsync<DeviceTypeDto>(configurationProvider: _mapper.ConfigurationProvider, paginationRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<PaginationResponse<DeviceTypeDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseClientSide<DeviceTypeDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceTypes.DatatableClientSideAsync<DeviceTypeDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<DatatableResponseClientSide<DeviceTypeDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseServerSide<DeviceTypeDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceTypes.DatatableServerSideAsync<DeviceTypeDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<DatatableResponseServerSide<DeviceTypeDto>>.Success(result);
    }
    #endregion
}