using AutoMapper;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Company.Commands;
using Scadex.Model.Dtos.Company.Queries;
using Scadex.Model.Entities;

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

    #region List
    public async Task<Result<ICollection<CompanyDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.Companies.GetAllAsync<CompanyDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<CompanyDto>>.NotFound();
        return Result<ICollection<CompanyDto>>.Success(result);
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
}