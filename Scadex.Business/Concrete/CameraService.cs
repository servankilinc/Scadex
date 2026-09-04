using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Business.Settings;
using Scadex.Business.Utils.CaptureFileStore;
using Scadex.Business.Utils.ClipCaptureQueue;
using Scadex.Business.Utils.MediaGateway;
using Scadex.Business.Utils.SnapshotGateway;
using Scadex.Core.Utils.HttpContextManager;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.Camera.Commands;
using Scadex.Model.Dtos.Camera.Queries;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Concrete;

public partial class CameraService : ICameraService
{
    #region Constructor
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IMediaGateway _mediaGateway;
    private readonly ISnapshotGateway _snapshotGateway;
    private readonly ICaptureFileStore _captureFileStore;
    private readonly IClipCaptureQueue _clipCaptureQueue;
    private readonly IDistributedCache _cache;
    private readonly MediaMtxSettings _mediaMtxSettings;
    private readonly CameraCaptureSettings _captureSettings;
    private readonly IHttpContextManager _httpContextManager;
    private readonly ILogger<CameraService> _logger;
    private readonly IMapper _mapper;

    public CameraService(
        IUnitOfWork unitOfWork,
        IValidationService validationService,
        IMediaGateway mediaGateway,
        ISnapshotGateway snapshotGateway,
        ICaptureFileStore captureFileStore,
        IClipCaptureQueue clipCaptureQueue,
        IDistributedCache cache,
        MediaMtxSettings mediaMtxSettings,
        CameraCaptureSettings captureSettings,
        IHttpContextManager httpContextManager,
        ILogger<CameraService> logger,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _mediaGateway = mediaGateway;
        _snapshotGateway = snapshotGateway;
        _captureFileStore = captureFileStore;
        _clipCaptureQueue = clipCaptureQueue;
        _cache = cache;
        _mediaMtxSettings = mediaMtxSettings;
        _captureSettings = captureSettings;
        _httpContextManager = httpContextManager;
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
            where: c => c.CabinetId == cabinetId && (includePassive || c.IsActive),
            include: i => i.Include(x => x.Cabinet).Include(x => x.DeviceStatus),
            orderBy: q => q.OrderBy(c => c.Name),
            cancellationToken: cancellationToken
        );

        return Result<ICollection<CameraDto>>.Success(cameras ?? []);
    }

    public async Task<Result<CameraDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var camera = await _unitOfWork.Cameras.GetAsync<CameraDto>(
            configurationProvider: _mapper.ConfigurationProvider,
            where: c => c.Id == id,
            include: i => i.Include(x => x.Cabinet).Include(x => x.DeviceStatus),
            cancellationToken: cancellationToken
        );

        if (camera == null)
            return Result<CameraDto>.NotFound("Kamera Bulunamadı");

        return Result<CameraDto>.Success(camera);
    }

    public async Task<Result<CreatedDto>> CreateAsync(CameraCreateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<CreatedDto>.Validation(validationResult.Failures, description: "Validation failed for CameraCreateDto");

        var cabinetExists = await _unitOfWork.Cabinets.IsExistAsync(where: c => c.Id == request.CabinetId && c.IsActive, cancellationToken: cancellationToken);

        if (!cabinetExists)
            return Result<CreatedDto>.Failure("Kabin bulunamadı veya pasif durumda");

        var conflict = await CheckUniquenessAsync(request.CabinetId, request.Name, request.IpAddress, excludeId: null, cancellationToken);
        if (conflict != null)
            return Result<CreatedDto>.Validation(conflict, description: "Camera uniqueness violated");

        var camera = _mapper.Map<Camera>(request);
        //camera.Id = Guid.NewGuid();
        camera.DeviceStatusId = null;
        camera.IsActive = true;

        await _unitOfWork.Cameras.AddAndSaveAsync(camera, cancellationToken);
        return Result<CreatedDto>.Success(new CreatedDto(camera.Id));
    }

    public async Task<Result> UpdateAsync(CameraUpdateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for CameraUpdateDto");

        var camera = await _unitOfWork.Cameras.GetAsync(where: c => c.Id == request.Id, tracking: true, cancellationToken: cancellationToken);

        if (camera == null)
            return Result.NotFound("Kamera bulunamadi");

        var conflict = await CheckUniquenessAsync(camera.CabinetId, request.Name, request.IpAddress, excludeId: camera.Id, cancellationToken);
        if (conflict != null)
            return Result.Validation(conflict, description: "Camera uniqueness violated");

        // Medya gateway (MediaMTX) path kameranin bilgilerine bağımlıdır biri degistiginde ya da kamera pasife alındığında path silinmelidir.
        // Aksi takdirde MediaMTX eski bilgilerle baglanmaya calisir ve zaman asimina duserek baglantiyi keser. 
        // ex: $"rtsp://{camera.Username}:{camera.Password}@{camera.IpAddress}:{camera.RtspPort}/Streaming/Channels/{channel}"
        bool connectionChanged =
            camera.IpAddress != request.IpAddress ||
            camera.RtspPort != request.RtspPort ||
            camera.Username != request.Username ||
            camera.MainStreamChannel != request.MainStreamChannel ||
            camera.SubStreamChannel != request.SubStreamChannel;

        bool passwordChanged = request.Password != null && camera.Password != request.Password;

        bool deactivated = camera.IsActive && !request.IsActive;

        camera = _mapper.Map(request, camera);

        if (passwordChanged)
            camera.Password = request.Password;

        await _unitOfWork.Cameras.UpdateAndSaveAsync(camera, cancellationToken);


        /// <summary> Kameranın Media gateway pathlerini etkileyen bilgileri değişmişse Live Stream pathleri temizlenir </summary>
        if (connectionChanged || deactivated || passwordChanged)
        {
            foreach (var profile in new[] { StreamProfile.Main, StreamProfile.Sub })
            {
                var pathName = IMediaGateway.LivePathName(camera.Id, profile);
                var result = await _mediaGateway.DeletePathAsync(pathName, cancellationToken);

                if (!result.IsSuccess)
                    _logger.LogWarning("Kamera {CameraId} icin güncelleme sonrası {Profile} yolu medya gecidinden silinemedi: {Reason}", camera.Id, profile, result.Error.Description);
            }
        }

        return Result.Success();
    }

    #region Helpers    
    /// <summary> Kamera Ad ve IP, kabin icinde benzersiz olmali. </summary>
    private async Task<Dictionary<string, string[]>?> CheckUniquenessAsync(Guid cabinetId, string name, string ipAddress, Guid? excludeId, CancellationToken cancellationToken)
    {
        var siblings = await _unitOfWork.Cameras.GetAllAsync(
            select: c => new { c.Id, c.Name, c.IpAddress },
            where: c => c.CabinetId == cabinetId && c.IsActive && (excludeId == null || c.Id != excludeId),
            cancellationToken: cancellationToken
        ) ?? [];

        var errors = new Dictionary<string, string[]>();

        if (siblings.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            errors["Name"] = ["Bu kabinde aynı isimde bir kamera zaten var"];

        if (siblings.Any(c => string.Equals(c.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase)))
            errors["IpAddress"] = ["Bu kabinde aynı IP adresine sahip bir kamera zaten var"];

        return errors.Count > 0 ? errors : null;
    }
    #endregion 
}
