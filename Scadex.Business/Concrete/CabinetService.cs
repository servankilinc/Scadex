using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Cabinet.Commands;
using Scadex.Model.Dtos.Cabinet.Queries;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Entities;
using DeviceStatus = Scadex.Model.Enums.EntityEnums.DeviceStatus;

namespace Scadex.Business.Concrete;

public class CabinetService : ICabinetService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMapper _mapper;
    private readonly ICabinetStatusService _cabinetStatusService;
    public CabinetService(IUnitOfWork unitOfWork, IValidationService validationService, IMapper mapper, ICabinetStatusService cabinetStatusService)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mapper = mapper;
        _cabinetStatusService = cabinetStatusService;
    }

    #region List
    public async Task<Result<ICollection<CabinetDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.GetAllAsync<CabinetDetailDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<CabinetDetailDto>>.NotFound();
        return Result<ICollection<CabinetDetailDto>>.Success(result);
    }
    #endregion

    #region Create
    public async Task<Result<CreatedDto>> CreateAsync(CabinetCreateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<CreatedDto>.Validation(validationResult.Failures, description: $"Validation failed for CabinetCreateDto");
        var entity = _mapper.Map<Cabinet>(request);
        entity.IsActive = true;
        var created = await _unitOfWork.Cabinets.AddAndSaveAsync(entity, cancellationToken);
        return Result<CreatedDto>.Success(new CreatedDto(created.Id));
    }
    #endregion

    #region Update
    public async Task<Result<CabinetUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.GetAsync<CabinetUpdateDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<CabinetUpdateDto>.NotFound();
        return Result<CabinetUpdateDto>.Success(result);
    }

    public async Task<Result> UpdateAsync(CabinetUpdateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures);
        var entity = await _unitOfWork.Cabinets.GetAsync(where: (f) => f.Id == request.Id, cancellationToken: cancellationToken);
        if (entity == null)
            return Result.NotFound();
        bool wasUnderMaintenance = entity.DeviceStatusId == (int)DeviceStatus.Maintenance;
        await _unitOfWork.Cabinets.UpdateAndSaveAsync(_mapper.Map(request, entity), cancellationToken);

        // Kabin durumu yalnizca durum servisi uzerinden yazılır ve degişen durum oradan signalR ile yayınlanır
        if (request.IsUnderMaintenance != wasUnderMaintenance)
            await _cabinetStatusService.SetMaintenanceAsync(request.Id, request.IsUnderMaintenance, cancellationToken);
        return Result.Success();
    }
    #endregion
}