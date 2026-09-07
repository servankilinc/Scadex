using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.ComponentTemplate.Commands;
using Scadex.Model.Dtos.ComponentTemplate.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Concrete;

public partial class ComponentTemplateService : IComponentTemplateService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidationService _validationService;
    public ComponentTemplateService(IUnitOfWork unitOfWork, IMapper mapper, IValidationService validationService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validationService = validationService;
    }
     
    #region Get
    public async Task<Result<ComponentTemplate>> GetAsync(Expression<Func<ComponentTemplate, bool>> where, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.ComponentTemplates.GetAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ComponentTemplate>.NotFound();
        return Result<ComponentTemplate>.Success(result);
    }

    public async Task<Result<ComponentTemplate>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.ComponentTemplates.GetAsync(where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ComponentTemplate>.NotFound();
        return Result<ComponentTemplate>.Success(result);
    }

    public async Task<Result<ComponentTemplateBaseDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.ComponentTemplates.GetAsync<ComponentTemplateBaseDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ComponentTemplateBaseDto>.NotFound();
        return Result<ComponentTemplateBaseDto>.Success(result);
    }

    public async Task<Result<ComponentTemplateDetailDto>> GetComponentTemplateDetailDtoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.ComponentTemplates.GetAsync<ComponentTemplateDetailDto>(configurationProvider: _mapper.ConfigurationProvider, where: (f) => f.Id == id, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ComponentTemplateDetailDto>.NotFound();
        return Result<ComponentTemplateDetailDto>.Success(result);
    }
    #endregion

    #region List
    public async Task<Result<ICollection<ComponentTemplate>>> GetListAsync(Expression<Func<ComponentTemplate, bool>>? where = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.ComponentTemplates.GetAllAsync(where: where, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<ComponentTemplate>>.NotFound();
        return Result<ICollection<ComponentTemplate>>.Success(result);
    }

    public async Task<Result<ICollection<ComponentTemplate>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.ComponentTemplates.GetAllAsync(filter: request?.Filter, sorts: request?.Sorts, tracking: false, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<ComponentTemplate>>.NotFound();
        return Result<ICollection<ComponentTemplate>>.Success(result);
    }

    public async Task<Result<ICollection<ComponentTemplateBaseDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.ComponentTemplates.GetAllAsync<ComponentTemplateBaseDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<ComponentTemplateBaseDto>>.NotFound();
        return Result<ICollection<ComponentTemplateBaseDto>>.Success(result);
    }

    public async Task<Result<ICollection<ComponentTemplateDetailDto>>> GetComponentTemplateDetailDtoListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default)
    {
        var result = await _unitOfWork.ComponentTemplates.GetAllAsync<ComponentTemplateDetailDto>(configurationProvider: _mapper.ConfigurationProvider, filter: request?.Filter, sorts: request?.Sorts, cancellationToken: cancellationToken);
        if (result == null)
            return Result<ICollection<ComponentTemplateDetailDto>>.NotFound();
        return Result<ICollection<ComponentTemplateDetailDto>>.Success(result);
    }
    #endregion
     
    public async Task<Result<ICollection<ComponentTemplatePaletteDto>>> GetPaletteAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _unitOfWork.ComponentTemplates.GetAllAsync(
            select: t => new ComponentTemplatePaletteDto
            {
                Id = t.Id,
                Name = t.Name,
                DeviceTypeId = t.DeviceTypeId,
                IsSystemTemplate = t.IsSystemTemplate,
                Width = t.Width,
                Height = t.Height,
                BackgroundColor = t.BackgroundColor,
                BackgroundImageUrl = t.BackgroundImageUrl,
                Pins = t.ComponentTemplatePins!
                    .OrderBy(p => p.Name)
                    .Select(p => new ComponentTemplatePalettePinDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        RelativeX = p.RelativeX,
                        RelativeY = p.RelativeY,
                        Side = p.Side,
                        Function = p.Function,
                        Direction = p.Direction,
                        VoltageLevel = p.VoltageLevel,
                        ChannelNumber = p.ChannelNumber
                    }).ToList()
            },
            where: t => t.IsActive,
            include: i => i.Include(t => t.ComponentTemplatePins),
            orderBy: q => q.OrderBy(t => t.DeviceTypeId).ThenBy(t => t.Name),
            cancellationToken: cancellationToken
        );

        return Result<ICollection<ComponentTemplatePaletteDto>>.Success(templates ?? []);
    }

    /// <summary> ComponentTemplate + ComponentTemplatePin'leri TEK transaction'da olusturur. </summary>
    public async Task<Result<CreatedDto>> CreateAsync(ComponentTemplateCreateRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<CreatedDto>.Validation(validationResult.Failures, description: "Validation failed for ComponentTemplateCreateRequest");

        var deviceTypeExists = await _unitOfWork.DeviceTypes.IsExistAsync(where: t => t.Id == request.DeviceTypeId, cancellationToken: cancellationToken);
        if (!deviceTypeExists)
        {
            return Result<CreatedDto>.Failure("Cihaz tipi bulunamadi");
        }

        var template = _mapper.Map<ComponentTemplate>(request);
        template.IsActive = true;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _unitOfWork.ComponentTemplates.Add(template);         
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (request.Pins.Count > 0)
            {
                foreach (var draft in request.Pins)
                {
                    var pin = _mapper.Map<ComponentTemplatePin>(draft);
                    pin.ComponentTemplateId = template.Id;
                    _unitOfWork.ComponentTemplatePins.Add(pin);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result<CreatedDto>.Success(new CreatedDto(template.Id));
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
