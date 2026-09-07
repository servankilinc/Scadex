using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Connection.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public class ConnectionService : IConnectionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    public ConnectionService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    #region Get
    public async Task<Result<Connection>> GetAsync(Expression<Func<Connection, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Connections.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<Connection>.NotFound();
        return Result<Connection>.Success(result);
    }

    public async Task<Result<Connection>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Connections.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<Connection>.NotFound();
        return Result<Connection>.Success(result);
    }

    public async Task<Result<ConnectionDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Connections.GetAsync<ConnectionDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ConnectionDto>.NotFound();
        return Result<ConnectionDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<Connection>>> GetListAsync(Expression<Func<Connection, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Connections.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<Connection>>.NotFound();
        return Result<ICollection<Connection>>.Success(result);
    }

    public async Task<Result<ICollection<Connection>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Connections.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<Connection>>.NotFound();
        return Result<ICollection<Connection>>.Success(result);
    }

    public async Task<Result<ICollection<ConnectionDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Connections.GetAllAsync<ConnectionDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<ConnectionDto>>.NotFound();
        return Result<ICollection<ConnectionDto>>.Success(result);
    }
    #endregion
}
