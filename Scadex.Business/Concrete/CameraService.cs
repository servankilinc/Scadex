using AutoMapper;
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
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Scadex.Business.Concrete;

public partial class CameraService : ICameraService
{
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

    public async Task<Result<ICollection<CameraDto>>> GetListAsync(Guid cabinetId, bool includePassive = false, CancellationToken cancellationToken = default)
    {
        var cabinetExists = await _unitOfWork.Cabinets.IsExistAsync(where: c => c.Id == cabinetId, cancellationToken: cancellationToken);

        // "Kabin yok" ile "kabinde kamera yok" ayirt edilebilmeli.
        if (!cabinetExists)
            return Result<ICollection<CameraDto>>.NotFound(description: "Kabin bulunamadi");

        var cameras = await _unitOfWork.Cameras.GetAllAsync(
            select: Projection,
            where: c => c.CabinetId == cabinetId && (includePassive || c.IsActive),
            orderBy: q => q.OrderBy(c => c.Name),
            cancellationToken: cancellationToken);

        return Result<ICollection<CameraDto>>.Success(cameras ?? []);
    }

    public async Task<Result<CameraDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var camera = await _unitOfWork.Cameras.GetAsync(
            select: Projection,
            where: c => c.Id == id,
            cancellationToken: cancellationToken);

        if (camera == null)
            return Result<CameraDto>.NotFound(description: "Kamera bulunamadi");

        return Result<CameraDto>.Success(camera);
    }

    public async Task<Result<CreatedDto>> CreateAsync(CameraCreateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result<CreatedDto>.Validation(validationResult.Failures, description: "Validation failed for CameraCreateDto");

        // FK kontrolu ACIKCA yapiliyor: kontrole birakilsaydi kisit ihlali 500
        // olarak donerdi, oysa bu istemcinin duzeltebilecegi siradan bir girdi hatasi.
        var cabinetExists = await _unitOfWork.Cabinets.IsExistAsync(
            where: c => c.Id == request.CabinetId && c.IsActive,
            cancellationToken: cancellationToken);

        if (!cabinetExists)
            return Result<CreatedDto>.Validation(
                new Dictionary<string, string[]> { ["CabinetId"] = ["Kabin bulunamadı veya pasif durumda"] },
                description: "Cabinet not found");

        var conflict = await CheckUniquenessAsync(request.CabinetId, request.Name, request.IpAddress, excludeId: null, cancellationToken);
        if (conflict != null) return Result<CreatedDto>.Validation(conflict, description: "Camera uniqueness violated");

        var camera = new Camera
        {
            Id = Guid.NewGuid(),
            CabinetId = request.CabinetId,
            Name = request.Name,
            Description = request.Description,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            IpAddress = request.IpAddress,
            RtspPort = request.RtspPort,
            HttpPort = request.HttpPort,
            HttpsPort = request.HttpsPort,
            Username = request.Username,
            // Duz metin saklaniyor ve okuma DTO'sunda duz metin donuyor
            // (kullanici karari) — araya bir koruma katmani KONULMUYOR.
            Password = request.Password,
            MainStreamChannel = request.MainStreamChannel,
            SubStreamChannel = request.SubStreamChannel,
            MainStreamEnabled = request.MainStreamEnabled,
            SubStreamEnabled = request.SubStreamEnabled,
            SnapshotChannel = request.SnapshotChannel,
            // Bos birakilirsa RTSP portu: kamerada anlamli sonda, servis portuna
            // TCP connect'tir (bkz. IMonitoredAsset.MonitoringPort).
            MonitoringPort = request.MonitoringPort ?? request.RtspPort,
            PingIntervalSec = request.PingIntervalSec,
            IsMonitoringEnabled = request.IsMonitoringEnabled,
            // Hic yoklanmadi: durum "Offline" DEGIL, "bilgim yok" (null).
            DeviceStatusId = null,
            // Pasif dogsaydi listede gorunmez ve kullanici sebebini anlamazdi —
            // B5'te Cabinet/Device icin duzeltilen kusurun aynisi.
            IsActive = true
        };

        await _unitOfWork.Cameras.AddAndSaveAsync(camera, cancellationToken);
        return Result<CreatedDto>.Success(new CreatedDto(camera.Id));
    }

    public async Task<Result> UpdateAsync(CameraUpdateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for CameraUpdateDto");

        var camera = await _unitOfWork.Cameras.GetAsync(
            where: c => c.Id == request.Id,
            tracking: true,
            cancellationToken: cancellationToken);

        if (camera == null)
            return Result.NotFound(description: "Kamera bulunamadi");

        var conflict = await CheckUniquenessAsync(camera.CabinetId, request.Name, request.IpAddress, excludeId: camera.Id, cancellationToken);
        if (conflict != null) return Result.Validation(conflict, description: "Camera uniqueness violated");

        // Medya gecidindeki yol, kameranin adresini VE PAROLASINI icinde tasir.
        // Bu alanlardan biri degistiginde ya da kamera pasife alindiginda yol
        // bayatlamis olur: pasif bir kameranin parolasi MediaMTX'in calisan
        // yapilandirmasinda asili kalmamali, degisen bir adres de eski degerle
        // baglanmaya devam etmemeli.
        //
        // Yol SILINIR, guncellenmez: bir sonraki bilet istegi onu zaten guncel
        // degerlerle yeniden kurar (EnsureLivePathAsync kendi kendini onarir).
        bool connectionChanged =
            camera.IpAddress != request.IpAddress ||
            camera.RtspPort != request.RtspPort ||
            camera.Username != request.Username ||
            camera.MainStreamChannel != request.MainStreamChannel ||
            camera.SubStreamChannel != request.SubStreamChannel ||
            (request.Password != null && camera.Password != (request.Password.Length == 0 ? null : request.Password));

        bool deactivated = camera.IsActive && !request.IsActive;

        camera.Name = request.Name;
        camera.Description = request.Description;
        camera.Manufacturer = request.Manufacturer;
        camera.Model = request.Model;
        camera.IpAddress = request.IpAddress;
        camera.RtspPort = request.RtspPort;
        camera.HttpPort = request.HttpPort;
        camera.HttpsPort = request.HttpsPort;
        camera.Username = request.Username;
        camera.MainStreamChannel = request.MainStreamChannel;
        camera.SubStreamChannel = request.SubStreamChannel;
        camera.MainStreamEnabled = request.MainStreamEnabled;
        camera.SubStreamEnabled = request.SubStreamEnabled;
        camera.SnapshotChannel = request.SnapshotChannel;
        camera.MonitoringPort = request.MonitoringPort ?? request.RtspPort;
        camera.PingIntervalSec = request.PingIntervalSec;
        camera.IsMonitoringEnabled = request.IsMonitoringEnabled;
        camera.IsActive = request.IsActive;

        // PAROLA UC DURUMLU:
        //   null       -> dokunma
        //   ""         -> sil
        //   dolu metin -> degistir
        //
        // Okuma DTO'su artik parolayi donduruyor, yani form onu onceden doldurabilir
        // ve "" gercekten "sil" anlamina gelir. Uc durum yine de korunuyor: alani
        // govdeden tumden cikaran bir istemci (ornegin yalnizca IP guncelleyen bir
        // betik) parolayi yanlislikla ucurmasin.
        if (request.Password != null)
            camera.Password = request.Password.Length == 0 ? null : request.Password;

        await _unitOfWork.Cameras.UpdateAndSaveAsync(camera, cancellationToken);

        // Kayit basariyla yazildiktan SONRA temizlik yapiliyor: gecit erisilemez
        // olsa bile kullanicinin duzenlemesi kaybolmamali. Bu yuzden sonuc da
        // yutuluyor — yol silinememesi, guncellemeyi basarisiz saymaz.
        if (connectionChanged || deactivated)
            await RemoveLivePathsAsync(camera, cancellationToken);

        return Result.Success();
    }

    // ==================== YARDIMCILAR ====================

    /// <summary>
    /// Ad ve IP, kabin icinde benzersiz olmali.
    ///
    /// DB'de filtreli UNIQUE index var; burada ONCEDEN kontrol edilmesinin sebebi
    /// kisit ihlalinin 500 olarak donmesi — oysa ikisi de istemcinin
    /// duzeltebilecegi siradan girdi hatalari. Ayni yaklasim
    /// <c>ComponentTemplateService.CreateAsync</c>'te de var.
    ///
    /// Karsilastirma DB tarafinda yapilir; SQL Server collation'i buyuk/kucuk harf
    /// duyarsiz oldugu icin "cam-1" ile "CAM-1" ayni satirdir ve .NET'in ordinal
    /// karsilastirmasi kullanilsaydi cakisma kacirilirdi.
    /// </summary>
    private async Task<Dictionary<string, string[]>?> CheckUniquenessAsync(
        Guid cabinetId, string name, string ipAddress, Guid? excludeId, CancellationToken cancellationToken)
    {
        var siblings = await _unitOfWork.Cameras.GetAllAsync(
            select: c => new { c.Id, c.Name, c.IpAddress },
            where: c => c.CabinetId == cabinetId && c.IsActive && (excludeId == null || c.Id != excludeId),
            cancellationToken: cancellationToken) ?? [];

        var errors = new Dictionary<string, string[]>();

        if (siblings.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            errors["Name"] = ["Bu kabinde aynı isimde bir kamera zaten var"];

        if (siblings.Any(c => string.Equals(c.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase)))
            errors["IpAddress"] = ["Bu kabinde aynı IP adresine sahip bir kamera zaten var"];

        return errors.Count > 0 ? errors : null;
    }

    /// <summary>
    /// Entity -> DTO projeksiyonu.
    ///
    /// AutoMapper yerine elle yaziliyor: bu projeksiyon, okuma yoluna hangi
    /// alanlarin ciktiginin TEK ve acik listesidir. Otomatik eslesme olsaydi
    /// entity'ye eklenen her yeni alan sessizce disari cikardi — parola bugun
    /// bilerek donduruluyor, ama bu kararin AÇIK verilmis olmasi gerekiyor.
    ///
    /// <b>Metot degil ALAN olmasi zorunlu.</b> <c>select: c => Project(c)</c>
    /// yazildiginda EF Core ifade agacindaki statik metot cagrisini SQL'e
    /// ceviremez ve calisma aninda patlar (derleme sessiz kalir, hata ancak ilk
    /// okumada gorulur — nitekim once oyle yazilip canli testte yakalandi).
    /// <c>Expression</c> olarak tutuldugunda EF govdeyi gorur ve ceviri yapar.
    /// </summary>
    private static readonly System.Linq.Expressions.Expression<Func<Camera, CameraDto>> Projection = c => new CameraDto
    {
        Id = c.Id,
        CabinetId = c.CabinetId,
        CabinetName = c.Cabinet!.Name,
        Name = c.Name,
        Description = c.Description,
        Manufacturer = c.Manufacturer,
        Model = c.Model,
        IpAddress = c.IpAddress,
        RtspPort = c.RtspPort,
        HttpPort = c.HttpPort,
        HttpsPort = c.HttpsPort,
        Username = c.Username,
        Password = c.Password,
        MainStreamChannel = c.MainStreamChannel,
        SubStreamChannel = c.SubStreamChannel,
        MainStreamEnabled = c.MainStreamEnabled,
        SubStreamEnabled = c.SubStreamEnabled,
        SnapshotChannel = c.SnapshotChannel,
        MonitoringPort = c.MonitoringPort,
        DeviceStatusId = c.DeviceStatusId,
        DeviceStatusName = c.DeviceStatus != null ? c.DeviceStatus.Name : null,
        LastSeen = c.LastSeen,
        PingIntervalSec = c.PingIntervalSec,
        IsMonitoringEnabled = c.IsMonitoringEnabled,
        LastConnectionError = c.LastConnectionError,
        IsActive = c.IsActive,
        CreateDateUtc = c.CreateDateUtc,
        UpdateDateUtc = c.UpdateDateUtc
    };


    private static Result<T> StreamValidationProblem<T>(string field, string message) =>
        Result<T>.Validation(new Dictionary<string, string[]> { [field] = [message] }, description: message);

    private static string? Truncate(string? value, int max) => 
        value == null || value.Length <= max ? value : value[..max];
}
