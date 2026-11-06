using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Business.Utils.CameraProtocolProfile.Resolver;
using Scadex.Business.Utils.DeviceObserver;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Camera.Commands;
using Scadex.Model.Dtos.Camera.Queries;

namespace Scadex.Business.Concrete;

public class CameraService : ICameraService
{
    #region Constructor
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IEnumerable<IDeviceChangeObserver> _deviceObservers;
    private readonly ILogger<CameraService> _logger;
    private readonly IMapper _mapper;
    private readonly ICameraProtocolProfileResolver _cameraProtocolProfileResolver;

    public CameraService(
        IUnitOfWork unitOfWork,
        IValidationService validationService,
        IEnumerable<IDeviceChangeObserver> deviceObservers,
        ICameraProtocolProfileResolver cameraProtocolProfileResolver,
        ILogger<CameraService> logger,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _deviceObservers = deviceObservers;
        _cameraProtocolProfileResolver = cameraProtocolProfileResolver;
        _logger = logger;
        _mapper = mapper;
    }
    #endregion

    public async Task<Result<ICollection<CameraDto>>> GetListAsync(Guid automationId, bool includePassive = false, CancellationToken cancellationToken = default)
    {
        var automationExists = await _unitOfWork.Automations.IsExistAsync(where: c => c.Id == automationId, cancellationToken: cancellationToken);

        if (!automationExists)
            return Result<ICollection<CameraDto>>.NotFound("İlgili Otomasyon bulunamadi");

        var cameras = await _unitOfWork.Cameras.GetAllAsync<CameraDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: c => c.Device!.AutomationId == automationId && (includePassive || c.Device.IsActive),
            orderBy: q => q.OrderBy(c => c.Device!.Name),
            cancellationToken: cancellationToken
        );

        return Result<ICollection<CameraDto>>.Success(cameras ?? []);
    }

    public async Task<Result<CameraDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var camera = await _unitOfWork.Cameras.GetAsync<CameraDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: c => c.DeviceId == id,
            cancellationToken: cancellationToken
        );

        if (camera == null)
            return Result<CameraDto>.NotFound("Kamera Bulunamadı");

        return Result<CameraDto>.Success(camera);
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateAsync(CameraUpdateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for CameraUpdateDto");

        // Pasif kamera = diyagramdan kaldirilmis cihaz (pinleri silinmis): bilgisi bu ekrandan degismez, geri de getirilemez.
        var camera = await _unitOfWork.Cameras.GetAsync(
            where: c => c.DeviceId == request.Id && c.Device!.IsActive,
            include: q => q.Include(c => c.Device),
            tracking: true,
            cancellationToken: cancellationToken);
        if (camera?.Device == null)
            return Result.NotFound("Kamera bulunamadı ya da diyagramdan kaldırılmış");

        var device = camera.Device;

        var conflict = await CheckUniquenessAsync(device.AutomationId, request.Name, request.IpAddress, excludeId: device.Id, cancellationToken);
        if (conflict != null)
            return Result.Validation(conflict, description: "Camera uniqueness violated");

        device.Name = request.Name;
        device.IpAddress = request.IpAddress;
        device.MonitoringPort = request.MonitoringPort ?? _cameraProtocolProfileResolver.Resolve(request.Brand).RtspPort;
        device.PingIntervalSec = request.PingIntervalSec;
        device.IsMonitoringEnabled = request.IsMonitoringEnabled;

        camera.Brand = request.Brand;
        camera.Model = request.Model;
        camera.Description = request.Description;
        camera.Username = request.Username;
        // null = dokunma; "" = temizle (diyagram taslagiyla ayni kural).
        if (request.Password != null)
            camera.Password = request.Password == "" ? null : request.Password;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MediaMTX yolu kamera bilgisine bagli: degistiyse modül (CameraManagement) yolu dusurur — commit sonrasi.
        await _deviceObservers.PublishDevicesChangedAsync([device.Id], _logger, cancellationToken);

        return Result.Success();
    }

    #region Helpers
    /// <summary>
    /// Kamera adi ve IP'si OTOMASYON ICINDE (aktif kameralar arasinda) tekil olmali. Diyagram kaydetme de IP tekilligini ayni
    /// kumede dogrular (<c>ValidateDeviceExtensions</c>); ad tekilligi yalnizca bu ekranin kuralidir.
    /// </summary>
    private async Task<Dictionary<string, string[]>?> CheckUniquenessAsync(Guid automationId, string name, string ipAddress, Guid? excludeId, CancellationToken cancellationToken)
    {
        var siblings = await _unitOfWork.Cameras.GetAllAsync(
            select: c => new { c.DeviceId, c.Device!.Name, c.Device.IpAddress },
            where: c => c.Device!.AutomationId == automationId && c.Device.IsActive && (excludeId == null || c.DeviceId != excludeId),
            cancellationToken: cancellationToken
        ) ?? [];

        var errors = new Dictionary<string, string[]>();

        if (siblings.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            errors["Name"] = ["Bu otomasyonda aynı isimde bir kamera zaten var"];

        if (siblings.Any(c => string.Equals(c.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase)))
            errors["IpAddress"] = ["Bu otomasyonda aynı IP adresine sahip bir kamera zaten var"];

        return errors.Count > 0 ? errors : null;
    }
    #endregion
}
