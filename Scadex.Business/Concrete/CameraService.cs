using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Scadex.Business.Abstract;
using Scadex.Business.Settings;
using Scadex.Business.Utils.CameraCaptureGateway;
using Scadex.Business.Utils.CameraProtocolProfile;
using Scadex.Business.Utils.CameraProtocolProfile.Resolver;
using Scadex.Business.Utils.CaptureFileStore;
using Scadex.Business.Utils.ClipCaptureQueue;
using Scadex.Business.Utils.MediaGateway;
using Scadex.Core.Utils.HttpContextManager;
using Scadex.Core.Utils.Logging;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Camera.Commands;
using Scadex.Model.Dtos.Camera.Queries;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

public partial class CameraService : ICameraService
{
    #region Constructor
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMediaGateway _mediaGateway;
    private readonly ICameraCaptureGateway _captureGateway;
    private readonly ICaptureFileStore _captureFileStore;
    private readonly IClipCaptureQueue _clipCaptureQueue;
    private readonly IDistributedCache _cache;
    private readonly MediaGatewaySettings _mediaGatewaySettings;
    private readonly ICameraCaptureSettingService _captureSettingService;
    private readonly IHttpContextManager _httpContextManager;
    private readonly ILoggingService _logger;
    private readonly IMapper _mapper;
    private readonly ICameraProtocolProfileResolver _cameraProtocolProfileResolver;

    public CameraService(
        IUnitOfWork unitOfWork,
        IValidationService validationService,
        IMediaGateway mediaGateway,
        ICameraCaptureGateway captureGateway,
        ICaptureFileStore captureFileStore,
        IClipCaptureQueue clipCaptureQueue,
        IDistributedCache cache,
        IOptions<MediaGatewaySettings> mediaGatewaySettings,
        ICameraCaptureSettingService captureSettingService,
        ICameraProtocolProfileResolver cameraProtocolProfileResolver,
        IHttpContextManager httpContextManager,
        ILoggingService logger,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mediaGateway = mediaGateway;
        _captureGateway = captureGateway;
        _captureFileStore = captureFileStore;
        _clipCaptureQueue = clipCaptureQueue;
        _cache = cache;
        _mediaGatewaySettings = mediaGatewaySettings.Value;
        _httpContextManager = httpContextManager;
        _captureSettingService = captureSettingService;
        _cameraProtocolProfileResolver = cameraProtocolProfileResolver;
        _logger = logger;
        _mapper = mapper;
    }
    #endregion

    public async Task<Result<ICollection<CameraDto>>> GetListAsync(Guid cabinetId, bool includePassive = false, CancellationToken cancellationToken = default)
    {
        var cabinetExists = await _unitOfWork.Cabinets.IsExistAsync(where: c => c.Id == cabinetId, cancellationToken: cancellationToken);

        if (!cabinetExists)
            return Result<ICollection<CameraDto>>.NotFound("İlgili Kabin bulunamadi");

        var cameras = await _unitOfWork.Cameras.GetAllAsync<CameraDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: c => c.Device!.CabinetId == cabinetId && (includePassive || c.Device.IsActive),
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

        var conflict = await CheckUniquenessAsync(device.CabinetId, request.Name, request.IpAddress, excludeId: device.Id, cancellationToken);
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

        // MediaMTX yolu kamera bilgisine bagli: degistiyse yol dusurulur (commit sonrasi).
        await RefreshStreamPathsAsync([device.Id], cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task RefreshStreamPathsAsync(IReadOnlyCollection<Guid> deviceIds, CancellationToken cancellationToken = default)
    {
        if (deviceIds.Count == 0) return;

        var ids = deviceIds.ToList();
        var cameras = await _unitOfWork.Cameras.GetAllAsync(
            select: c => new CameraRow(new CameraEndpoint(c.DeviceId, c.Device!.IpAddress ?? "", c.Brand, c.Username, c.Password), c.Device.IsActive),
            where: c => ids.Contains(c.DeviceId),
            cancellationToken: cancellationToken) ?? [];

        foreach (var camera in cameras)
        {
            foreach (var profile in new[] { StreamProfile.Main, StreamProfile.Sub })
            {
                // Pasif (silinmis) kameranin yolu her kosulda gider; aktifte yalnizca yapilandirmasi eskimisse.
                var result = camera.IsActive
                    ? await _mediaGateway.DeleteLivePathIfStaleAsync(camera.Endpoint, profile, cancellationToken)
                    : await _mediaGateway.DeletePathAsync(IMediaGateway.LivePathName(camera.Endpoint.Id, profile), cancellationToken);

                if (!result.IsSuccess)
                    _logger.LogWarning($"Kamera {camera.Endpoint.Id} icin kayit sonrasi {profile} yolu medya gecidinde yenilenemedi: {result.Error.Description}");
            }
        }
    }

    #region Helpers
    /// <summary>
    /// Kamera adi ve IP'si KABIN ICINDE (aktif kameralar arasinda) tekil olmali. Diyagram kaydetme de IP tekilligini ayni
    /// kumede dogrular (<c>ValidateDeviceExtensions</c>); ad tekilligi yalnizca bu ekranin kuralidir.
    /// </summary>
    private async Task<Dictionary<string, string[]>?> CheckUniquenessAsync(Guid cabinetId, string name, string ipAddress, Guid? excludeId, CancellationToken cancellationToken)
    {
        var siblings = await _unitOfWork.Cameras.GetAllAsync(
            select: c => new { c.DeviceId, c.Device!.Name, c.Device.IpAddress },
            where: c => c.Device!.CabinetId == cabinetId && c.Device.IsActive && (excludeId == null || c.DeviceId != excludeId),
            cancellationToken: cancellationToken
        ) ?? [];

        var errors = new Dictionary<string, string[]>();

        if (siblings.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            errors["Name"] = ["Bu kabinde aynı isimde bir kamera zaten var"];

        if (siblings.Any(c => string.Equals(c.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase)))
            errors["IpAddress"] = ["Bu kabinde aynı IP adresine sahip bir kamera zaten var"];

        return errors.Count > 0 ? errors : null;
    }

    /// <summary> Kameraya baglanmak icin gereken her sey + cihazin aktifligi, TEK sorguda (Device JOIN'i EF kurar). </summary>
    private Task<CameraRow?> LoadCameraAsync(Guid cameraId, CancellationToken cancellationToken) =>
        _unitOfWork.Cameras.GetAsync(
            select: c => new CameraRow(new CameraEndpoint(c.DeviceId, c.Device!.IpAddress ?? "", c.Brand, c.Username, c.Password), c.Device.IsActive),
            where: c => c.DeviceId == cameraId,
            cancellationToken: cancellationToken);

    private sealed record CameraRow(CameraEndpoint Endpoint, bool IsActive);
    #endregion
}
