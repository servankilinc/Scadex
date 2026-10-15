using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Scadex.Business.Abstract;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.ComponentTemplate.Commands;
using Scadex.Model.Dtos.ComponentTemplate.Queries;
using Scadex.Model.Entities;

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
                IsMonitorable = t.IsMonitorable,
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

    public async Task<Result<ICollection<ComponentTemplateTypeCountDto>>> GetTypeCountsAsync(CancellationToken cancellationToken = default)
    {
        // RepositoryBase GroupBy sunmuyor — bunun için repository'ye özel metot açmaya değmez.
        var deviceTypeIds = await _unitOfWork.ComponentTemplates.GetAllAsync(
            select: t => t.DeviceTypeId,
            where: t => t.IsActive,
            cancellationToken: cancellationToken
        );

        var counts = (deviceTypeIds ?? [])
            .GroupBy(id => id)
            .Select(g => new ComponentTemplateTypeCountDto { DeviceTypeId = g.Key, Count = g.Count() })
            .OrderBy(c => c.DeviceTypeId)
            .ToList();

        return Result<ICollection<ComponentTemplateTypeCountDto>>.Success(counts);
    }

    public async Task<Result<ICollection<ComponentTemplateListItemDto>>> GetListByDeviceTypeAsync(int deviceTypeId, CancellationToken cancellationToken = default)
    {
        var templates = await _unitOfWork.ComponentTemplates.GetAllAsync(
            select: t => new ComponentTemplateListItemDto
            {
                Id = t.Id,
                Name = t.Name,
                DeviceTypeId = t.DeviceTypeId,
                IsSystemTemplate = t.IsSystemTemplate,
                Width = t.Width,
                Height = t.Height,
                BackgroundColor = t.BackgroundColor,
                BackgroundImageUrl = t.BackgroundImageUrl,
                IsMonitorable = t.IsMonitorable,
                // SQL'de alt sorgu COUNT'u olur; pin satırları istemciye taşınmaz.
                PinCount = t.ComponentTemplatePins!.Count
            },
            where: t => t.IsActive && t.DeviceTypeId == deviceTypeId,
            orderBy: q => q.OrderBy(t => t.Name),
            cancellationToken: cancellationToken
        );

        return Result<ICollection<ComponentTemplateListItemDto>>.Success(templates ?? []);
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
