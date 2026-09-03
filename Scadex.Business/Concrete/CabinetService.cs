using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.DynamicQuery;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Cabinet.Commands;
using Scadex.Model.Dtos.Cabinet.Queries;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public class CabinetService : ICabinetService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMapper _mapper;
    public CabinetService(IUnitOfWork unitOfWork, IValidationService validationService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mapper = mapper;
    }

    #region Get
    public async Task<Result<Cabinet>> GetAsync(Expression<Func<Cabinet, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<Cabinet>.NotFound();
        return Result<Cabinet>.Success(result);
    }

    public async Task<Result<Cabinet>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<Cabinet>.NotFound();
        return Result<Cabinet>.Success(result);
    }

    public async Task<Result<CabinetBaseDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.GetAsync<CabinetBaseDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<CabinetBaseDto>.NotFound();
        return Result<CabinetBaseDto>.Success(result);
    }

    public async Task<Result<CabinetDetailDto>> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.GetAsync<CabinetDetailDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<CabinetDetailDto>.NotFound();
        return Result<CabinetDetailDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<Cabinet>>> GetListAsync(Expression<Func<Cabinet, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<Cabinet>>.NotFound();
        return Result<ICollection<Cabinet>>.Success(result);
    }

    public async Task<Result<ICollection<Cabinet>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<Cabinet>>.NotFound();
        return Result<ICollection<Cabinet>>.Success(result);
    }

    public async Task<Result<ICollection<CabinetBaseDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.GetAllAsync<CabinetBaseDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<CabinetBaseDto>>.NotFound();
        return Result<ICollection<CabinetBaseDto>>.Success(result);
    }

    public async Task<Result<ICollection<CabinetDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.GetAllAsync<CabinetDetailDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<CabinetDetailDto>>.NotFound();
        return Result<ICollection<CabinetDetailDto>>.Success(result);
    }
    #endregion

    #region SelectList
    public async Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<Cabinet, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        Expression<Func<Cabinet, bool>> activeOnly = f => f.IsActive;
        var list = await _unitOfWork.Cabinets.GetAllAsync<SelectItemDto>(select: s => new SelectItemDto { Value = s.Id.ToString(), Text = s.Name }, where: activeOnly.AndAlso(where), cancellationToken: cancellationToken);
        var selectList = list ?? new List<SelectItemDto>();
        return Result<ICollection<SelectItemDto>>.Success(selectList);
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
        await _unitOfWork.Cabinets.UpdateAndSaveAsync(_mapper.Map(request, entity), cancellationToken);
        return Result.Success();
    }
    #endregion

    #region Pagination / Datatable
    public async Task<Result<PaginationResponse<CabinetDetailDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.PaginationAsync<CabinetDetailDto>(configurationProvider: _mapper.ConfigurationProvider, paginationRequest: request, include: i => i.Include(x => x.Company).Include(x => x.DeviceStatus), cancellationToken: cancellationToken);
        return Result<PaginationResponse<CabinetDetailDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseClientSide<CabinetDetailDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.DatatableClientSideAsync<CabinetDetailDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: i => i.Include(x => x.Company).Include(x => x.DeviceStatus), cancellationToken: cancellationToken);
        return Result<DatatableResponseClientSide<CabinetDetailDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseServerSide<CabinetDetailDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Cabinets.DatatableServerSideAsync<CabinetDetailDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: i => i.Include(x => x.Company).Include(x => x.DeviceStatus), cancellationToken: cancellationToken);
        return Result<DatatableResponseServerSide<CabinetDetailDto>>.Success(result);
    }
    #endregion
}