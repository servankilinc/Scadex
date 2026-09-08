using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Pin.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public class PinService : IPinService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    public PinService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    #region Get
    public async Task<Result<Pin>> GetAsync(Expression<Func<Pin, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Pins.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<Pin>.NotFound();
        return Result<Pin>.Success(result);
    }

    public async Task<Result<Pin>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Pins.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<Pin>.NotFound();
        return Result<Pin>.Success(result);
    }

    public async Task<Result<PinDetailDto>> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Pins.GetAsync<PinDetailDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<PinDetailDto>.NotFound();
        return Result<PinDetailDto>.Success(result);
    }

    public async Task<Result<PinDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Pins.GetAsync<PinDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<PinDto>.NotFound();
        return Result<PinDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<Pin>>> GetListAsync(Expression<Func<Pin, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Pins.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<Pin>>.NotFound();
        return Result<ICollection<Pin>>.Success(result);
    }

    public async Task<Result<ICollection<Pin>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Pins.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<Pin>>.NotFound();
        return Result<ICollection<Pin>>.Success(result);
    }

    public async Task<Result<ICollection<PinDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Pins.GetAllAsync<PinDetailDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<PinDetailDto>>.NotFound();
        return Result<ICollection<PinDetailDto>>.Success(result);
    }

    public async Task<Result<ICollection<PinDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Pins.GetAllAsync<PinDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<PinDto>>.NotFound();
        return Result<ICollection<PinDto>>.Success(result);
    }
    #endregion
}
