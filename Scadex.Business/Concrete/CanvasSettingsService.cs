using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.CanvasSettings.Commands;
using Scadex.Model.Dtos.Diagram.Queries.Items;
using Scadex.Model.Entities;

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
