using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.DiagramAnnotation.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public class DiagramAnnotationService : IDiagramAnnotationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    public DiagramAnnotationService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    #region Get
    public async Task<Result<DiagramAnnotation>> GetAsync(Expression<Func<DiagramAnnotation, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DiagramAnnotations.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DiagramAnnotation>.NotFound();
        return Result<DiagramAnnotation>.Success(result);
    }

    public async Task<Result<DiagramAnnotation>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DiagramAnnotations.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DiagramAnnotation>.NotFound();
        return Result<DiagramAnnotation>.Success(result);
    }

    public async Task<Result<DiagramAnnotationDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DiagramAnnotations.GetAsync<DiagramAnnotationDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DiagramAnnotationDto>.NotFound();
        return Result<DiagramAnnotationDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<DiagramAnnotation>>> GetListAsync(Expression<Func<DiagramAnnotation, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DiagramAnnotations.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DiagramAnnotation>>.NotFound();
        return Result<ICollection<DiagramAnnotation>>.Success(result);
    }

    public async Task<Result<ICollection<DiagramAnnotation>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DiagramAnnotations.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DiagramAnnotation>>.NotFound();
        return Result<ICollection<DiagramAnnotation>>.Success(result);
    }

    public async Task<Result<ICollection<DiagramAnnotationDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DiagramAnnotations.GetAllAsync<DiagramAnnotationDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DiagramAnnotationDto>>.NotFound();
        return Result<ICollection<DiagramAnnotationDto>>.Success(result);
    }
    #endregion
}
