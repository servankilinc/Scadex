using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.Utils.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Automation.Commands;
using Scadex.Model.Dtos.Automation.Queries;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Entities;
using DeviceStatus = Scadex.Model.Enums.EntityEnums.DeviceStatus;

namespace Scadex.Business.Concrete;

public class AutomationService : IAutomationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMapper _mapper;
    private readonly IAutomationStatusService _automationStatusService;
    public AutomationService(IUnitOfWork unitOfWork, IValidationService validationService, IMapper mapper, IAutomationStatusService automationStatusService)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mapper = mapper;
        _automationStatusService = automationStatusService;
    }

    #region List
    public async Task<Result<ICollection<AutomationDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Automations.GetAllAsync<AutomationDetailDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<AutomationDetailDto>>.NotFound();
        return Result<ICollection<AutomationDetailDto>>.Success(result);
    }
    #endregion

    #region Create
    public async Task<Result<CreatedDto>> CreateAsync(AutomationCreateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<CreatedDto>.Validation(validationResult.Failures, description: $"Validation failed for AutomationCreateDto");
        var entity = _mapper.Map<Automation>(request);
        entity.IsActive = true;
        var created = await _unitOfWork.Automations.AddAndSaveAsync(entity, cancellationToken);
        return Result<CreatedDto>.Success(new CreatedDto(created.Id));
    }
    #endregion

    #region Update
    public async Task<Result<AutomationUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Automations.GetAsync<AutomationUpdateDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<AutomationUpdateDto>.NotFound();
        return Result<AutomationUpdateDto>.Success(result);
    }

    public async Task<Result> UpdateAsync(AutomationUpdateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures);
        var entity = await _unitOfWork.Automations.GetAsync(where: (f) => f.Id == request.Id, cancellationToken: cancellationToken);
        if (entity == null)
            return Result.NotFound();
        bool wasUnderMaintenance = entity.DeviceStatusId == (int)DeviceStatus.Maintenance;
        await _unitOfWork.Automations.UpdateAndSaveAsync(_mapper.Map(request, entity), cancellationToken);

        // Otomasyon durumu yalnizca durum servisi uzerinden yazılır ve degişen durum oradan signalR ile yayınlanır
        if (request.IsUnderMaintenance != wasUnderMaintenance)
            await _automationStatusService.SetMaintenanceAsync(request.Id, request.IsUnderMaintenance, cancellationToken);
        return Result.Success();
    }
    #endregion
}