using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Device.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public class DeviceService : IDeviceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    public DeviceService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    #region Get
    public async Task<Result<Device>> GetAsync(Expression<Func<Device, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Devices.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<Device>.NotFound();
        return Result<Device>.Success(result);
    }

    public async Task<Result<Device>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Devices.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<Device>.NotFound();
        return Result<Device>.Success(result);
    }

    public async Task<Result<DeviceDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Devices.GetAsync<DeviceDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DeviceDto>.NotFound();
        return Result<DeviceDto>.Success(result);
    }

    public async Task<Result<DeviceDetailDto>> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Devices.GetAsync<DeviceDetailDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DeviceDetailDto>.NotFound();
        return Result<DeviceDetailDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<Device>>> GetListAsync(Expression<Func<Device, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Devices.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<Device>>.NotFound();
        return Result<ICollection<Device>>.Success(result);
    }

    public async Task<Result<ICollection<Device>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Devices.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<Device>>.NotFound();
        return Result<ICollection<Device>>.Success(result);
    }

    public async Task<Result<ICollection<DeviceDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Devices.GetAllAsync<DeviceDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DeviceDto>>.NotFound();
        return Result<ICollection<DeviceDto>>.Success(result);
    }

    public async Task<Result<ICollection<DeviceDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Devices.GetAllAsync<DeviceDetailDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DeviceDetailDto>>.NotFound();
        return Result<ICollection<DeviceDetailDto>>.Success(result);
    }
    #endregion
}
