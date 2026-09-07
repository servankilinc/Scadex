using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.CanvasSettings.Commands;
using Scadex.Model.Dtos.CanvasSettings.Queries;
using Scadex.Model.Dtos.Diagram.Queries.Items;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public class CanvasSettingsService : ICanvasSettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidationService _validationService;
    public CanvasSettingsService(IUnitOfWork unitOfWork, IMapper mapper, IValidationService validationService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validationService = validationService;
    }

    #region Get
    public async Task<Result<CanvasSettings>> GetAsync(Expression<Func<CanvasSettings, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.CanvasSettings.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<CanvasSettings>.NotFound();
        return Result<CanvasSettings>.Success(result);
    }

    public async Task<Result<CanvasSettings>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.CanvasSettings.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<CanvasSettings>.NotFound();
        return Result<CanvasSettings>.Success(result);
    }

    public async Task<Result<CanvasSettingsDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.CanvasSettings.GetAsync<CanvasSettingsDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<CanvasSettingsDto>.NotFound();
        return Result<CanvasSettingsDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<CanvasSettings>>> GetListAsync(Expression<Func<CanvasSettings, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.CanvasSettings.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<CanvasSettings>>.NotFound();
        return Result<ICollection<CanvasSettings>>.Success(result);
    }

    public async Task<Result<ICollection<CanvasSettings>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.CanvasSettings.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<CanvasSettings>>.NotFound();
        return Result<ICollection<CanvasSettings>>.Success(result);
    }

    public async Task<Result<ICollection<CanvasSettingsDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.CanvasSettings.GetAllAsync<CanvasSettingsDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<CanvasSettingsDto>>.NotFound();
        return Result<ICollection<CanvasSettingsDto>>.Success(result);
    }
    #endregion


    /// <inheritdoc />
    public async Task<Result<DiagramCanvasSettingsDto>> UpsertAsync(Guid cabinetId, CanvasSettingsUpsertDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<DiagramCanvasSettingsDto>.Validation(validationResult.Failures, description: "Validation failed for CanvasSettingsUpsertDto");

        var cabinetExists = await _unitOfWork.Cabinets.IsExistAsync(where: c => c.Id == cabinetId && c.IsActive, cancellationToken: cancellationToken);

        if (!cabinetExists)
            return Result<DiagramCanvasSettingsDto>.NotFound(description: "Kabin bulunamadi veya pasif durumda");

        var existing = await _unitOfWork.CanvasSettings.GetAsync(where: s => s.CabinetId == cabinetId, cancellationToken: cancellationToken);

        if (existing == null)
        {
            var created = _mapper.Map<CanvasSettings>(request);
            created.CabinetId = cabinetId;
            await _unitOfWork.CanvasSettings.AddAndSaveAsync(created, cancellationToken);
        }
        else
        {
            _mapper.Map(request, existing);
            await _unitOfWork.CanvasSettings.UpdateAndSaveAsync(existing, cancellationToken);
        }

        return Result<DiagramCanvasSettingsDto>.Success(_mapper.Map<DiagramCanvasSettingsDto>(request));
    }
}
