using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.DiagramNotifier;
using Scadex.Business.Utils.ScadaCommandGateway;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.HttpContextManager;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.DeviceCommand.Commands;
using Scadex.Model.Dtos.DeviceCommand.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public partial class DeviceCommandService : IDeviceCommandService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMapper _mapper;
    private readonly IScadaCommandGateway _scadaCommandGateway;
    private readonly IDiagramNotifier _notifier;
    private readonly IHttpContextManager _httpContextManager;
    private readonly ILogger<DeviceCommandService> _logger;

    public DeviceCommandService(IUnitOfWork unitOfWork, IValidationService validationService, IMapper mapper, IScadaCommandGateway scadaCommandGateway, IDiagramNotifier notifier, IHttpContextManager httpContextManager, ILogger<DeviceCommandService> logger)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mapper = mapper;
        _scadaCommandGateway = scadaCommandGateway;
        _notifier = notifier;
        _httpContextManager = httpContextManager;
        _logger = logger;
    }

    #region Get
    public async Task<Result<DeviceCommand>> GetAsync(Expression<Func<DeviceCommand, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceCommands.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DeviceCommand>.NotFound();
        return Result<DeviceCommand>.Success(result);
    }

    public async Task<Result<DeviceCommand>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceCommands.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DeviceCommand>.NotFound();
        return Result<DeviceCommand>.Success(result);
    }

    public async Task<Result<DeviceCommandDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceCommands.GetAsync<DeviceCommandDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DeviceCommandDto>.NotFound();
        return Result<DeviceCommandDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<DeviceCommand>>> GetListAsync(Expression<Func<DeviceCommand, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceCommands.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DeviceCommand>>.NotFound();
        return Result<ICollection<DeviceCommand>>.Success(result);
    }

    public async Task<Result<ICollection<DeviceCommand>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceCommands.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DeviceCommand>>.NotFound();
        return Result<ICollection<DeviceCommand>>.Success(result);
    }

    public async Task<Result<ICollection<DeviceCommandDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceCommands.GetAllAsync<DeviceCommandDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<DeviceCommandDto>>.NotFound();
        return Result<ICollection<DeviceCommandDto>>.Success(result);
    }
    #endregion

    #region SelectList
    public async Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<DeviceCommand, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var list = await _unitOfWork.DeviceCommands.GetAllAsync<SelectItemDto>(select: s => new SelectItemDto { Value = s.Id.ToString(), Text = s.PayloadJson ?? string.Empty }, where: where, cancellationToken: cancellationToken);
        var selectList = list ?? new List<SelectItemDto>();
        return Result<ICollection<SelectItemDto>>.Success(selectList);
    }
    #endregion

    #region Create
    public async Task<Result> CreateAsync(DeviceCommandCreateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: $"Validation failed for DeviceCommandCreateDto");
        await _unitOfWork.DeviceCommands.AddAndSaveAsync(_mapper.Map<DeviceCommand>(request), cancellationToken);
        return Result.Success();
    }
    #endregion

    #region Update
    public async Task<Result<DeviceCommandUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceCommands.GetAsync<DeviceCommandUpdateDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<DeviceCommandUpdateDto>.NotFound();
        return Result<DeviceCommandUpdateDto>.Success(result);
    }

    public async Task<Result> UpdateAsync(DeviceCommandUpdateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures);
        var entity = await _unitOfWork.DeviceCommands.GetAsync(where: (f) => f.Id == request.Id, cancellationToken: cancellationToken);
        if (entity == null)
            return Result.NotFound();
        await _unitOfWork.DeviceCommands.UpdateAndSaveAsync(_mapper.Map(request, entity), cancellationToken);
        return Result.Success();
    }
    #endregion

    #region Delete / Restore
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var affected = await _unitOfWork.DeviceCommands.DeleteAndSaveAsync(where: (f) => f.Id == id, cancellationToken);
        if (affected == 0)
            return Result.NotFound();
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var restored = await _unitOfWork.DeviceCommands.RestoreAndSaveAsync(where: (f) => f.Id == id, cancellationToken);
        if (restored == 0)
            return Result.NotFound();
        return Result.Success();
    }
    #endregion

    #region Pagination / Datatable
    public async Task<Result<PaginationResponse<DeviceCommandDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceCommands.PaginationAsync<DeviceCommandDto>(configurationProvider: _mapper.ConfigurationProvider, paginationRequest: request, include: i => i.Include(x => x.Device).Include(x => x.RequesterUser), cancellationToken: cancellationToken);
        return Result<PaginationResponse<DeviceCommandDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseClientSide<DeviceCommandDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceCommands.DatatableClientSideAsync<DeviceCommandDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: i => i.Include(x => x.Device).Include(x => x.RequesterUser), cancellationToken: cancellationToken);
        return Result<DatatableResponseClientSide<DeviceCommandDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseServerSide<DeviceCommandDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.DeviceCommands.DatatableServerSideAsync<DeviceCommandDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: i => i.Include(x => x.Device).Include(x => x.RequesterUser), cancellationToken: cancellationToken);
        return Result<DatatableResponseServerSide<DeviceCommandDto>>.Success(result);
    } 
    #endregion
}