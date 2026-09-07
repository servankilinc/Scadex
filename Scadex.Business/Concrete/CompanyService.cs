using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.Company.Commands;
using Scadex.Model.Dtos.Company.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public class CompanyService : ICompanyService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMapper _mapper;
    public CompanyService(IUnitOfWork unitOfWork, IValidationService validationService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mapper = mapper;
    }

    #region Get
    public async Task<Result<Company>> GetAsync(Expression<Func<Company, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Companies.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<Company>.NotFound();
        return Result<Company>.Success(result);
    }

    public async Task<Result<Company>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Companies.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<Company>.NotFound();
        return Result<Company>.Success(result);
    }

    public async Task<Result<CompanyDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Companies.GetAsync<CompanyDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<CompanyDto>.NotFound();
        return Result<CompanyDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<Company>>> GetListAsync(Expression<Func<Company, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Companies.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<Company>>.NotFound();
        return Result<ICollection<Company>>.Success(result);
    }

    public async Task<Result<ICollection<Company>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Companies.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<Company>>.NotFound();
        return Result<ICollection<Company>>.Success(result);
    }

    public async Task<Result<ICollection<CompanyDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Companies.GetAllAsync<CompanyDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<CompanyDto>>.NotFound();
        return Result<ICollection<CompanyDto>>.Success(result);
    }
    #endregion

    #region SelectList
    public async Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<Company, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var list = await _unitOfWork.Companies.GetAllAsync<SelectItemDto>(select: s => new SelectItemDto { Value = s.Id.ToString(), Text = s.Name }, where: where, cancellationToken: cancellationToken);
        var selectList = list ?? new List<SelectItemDto>();
        return Result<ICollection<SelectItemDto>>.Success(selectList);
    }
    #endregion

    #region Create
    public async Task<Result> CreateAsync(CompanyCreateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: $"Validation failed for CompanyCreateDto");
        var entity = _mapper.Map<Company>(request);
        entity.IsActive = true;
        await _unitOfWork.Companies.AddAndSaveAsync(entity, cancellationToken);
        return Result.Success();
    }
    #endregion

    #region Update
    public async Task<Result<CompanyUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Companies.GetAsync<CompanyUpdateDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<CompanyUpdateDto>.NotFound();
        return Result<CompanyUpdateDto>.Success(result);
    }

    public async Task<Result> UpdateAsync(CompanyUpdateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures);

        var entity = await _unitOfWork.Companies.GetAsync(where: (f) => f.Id == request.Id, cancellationToken: cancellationToken);
        if (entity == null)
            return Result.NotFound();
        await _unitOfWork.Companies.UpdateAndSaveAsync(_mapper.Map(request, entity), cancellationToken);
        return Result.Success();
    }
    #endregion

    #region Pagination / Datatable
    public async Task<Result<PaginationResponse<CompanyDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Companies.PaginationAsync<CompanyDto>(configurationProvider: _mapper.ConfigurationProvider, paginationRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<PaginationResponse<CompanyDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseClientSide<CompanyDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Companies.DatatableClientSideAsync<CompanyDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<DatatableResponseClientSide<CompanyDto>>.Success(result);
    }

    public async Task<Result<DatatableResponseServerSide<CompanyDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Companies.DatatableServerSideAsync<CompanyDto>(configurationProvider: _mapper.ConfigurationProvider, datatableRequest: request, include: null, cancellationToken: cancellationToken);
        return Result<DatatableResponseServerSide<CompanyDto>>.Success(result);
    }
    #endregion
}