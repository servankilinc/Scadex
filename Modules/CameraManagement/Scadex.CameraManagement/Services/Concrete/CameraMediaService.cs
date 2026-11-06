using AutoMapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Scadex.Business.Settings;
using Scadex.Business.Utils.CameraProtocolProfile;
using Scadex.Business.Utils.CameraProtocolProfile.Resolver;
using Scadex.Business.Utils.MediaGateway;
using Scadex.CameraManagement.Capture;
using Scadex.CameraManagement.DataAccess;
using Scadex.CameraManagement.Media;
using Scadex.CameraManagement.Services.Abstract;
using Scadex.Core.Utils.HttpContextManager;
using Scadex.Core.Utils.Logging;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.CameraManagement.Services.Concrete;

/// <summary>
/// Kamera bilgisi çekirdekten (<c>IUnitOfWork.Cameras</c>) YALNIZCA projeksiyonla okunur, çekirdek tablolarına yazılmaz.
/// Çekimler ve çekim ayarı modülün kendi context'indedir (<see cref="CameraManagementDbContext"/>).
/// </summary>
public partial class CameraMediaService : ICameraMediaService
{
    #region Constructor
    private readonly IUnitOfWork _unitOfWork;
    private readonly CameraManagementDbContext _context;
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

    public CameraMediaService(
        IUnitOfWork unitOfWork,
        CameraManagementDbContext context,
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
        _context = context;
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
                string pathName = CameraMediaPaths.LivePathName(camera.Endpoint.Id, profile);

                // Pasif (silinmis) kameranin yolu her kosulda gider; aktifte yalnizca yapilandirmasi eskimisse.
                var result = camera.IsActive
                    ? await _mediaGateway.DeleteSourcePathIfStaleAsync(pathName, BuildRtspSourceUrl(camera.Endpoint, profile), cancellationToken)
                    : await _mediaGateway.DeletePathAsync(pathName, cancellationToken);

                if (!result.IsSuccess)
                    _logger.LogWarning($"Kamera {camera.Endpoint.Id} icin kayit sonrasi {profile} yolu medya gecidinde yenilenemedi: {result.Error.Description}");
            }
        }
    }

    #region Helpers
    /// <summary> MediaMTX yolunun kaynagi: markanin RTSP adresi. Kameraya YALNIZCA MediaMTX baglanir; markaya ozgu tek bilgi budur. </summary>
    private string BuildRtspSourceUrl(CameraEndpoint camera, StreamProfile profile) =>
        _cameraProtocolProfileResolver.Resolve(camera.Brand).BuildRtspUrl(camera, profile);

    /// <summary> Kameraya baglanmak icin gereken her sey + cihazin aktifligi, TEK sorguda (Device JOIN'i EF kurar). </summary>
    private Task<CameraRow?> LoadCameraAsync(Guid cameraId, CancellationToken cancellationToken) =>
        _unitOfWork.Cameras.GetAsync(
            select: c => new CameraRow(new CameraEndpoint(c.DeviceId, c.Device!.IpAddress ?? "", c.Brand, c.Username, c.Password), c.Device.IsActive),
            where: c => c.DeviceId == cameraId,
            cancellationToken: cancellationToken);

    private sealed record CameraRow(CameraEndpoint Endpoint, bool IsActive);
    #endregion
}
