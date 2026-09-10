using AutoMapper;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Scadex.Business.Abstract;
using Scadex.Business.Settings;
using Scadex.Core.Utils.Caching;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.CameraCaptureSetting.Commands;
using Scadex.Model.Dtos.CameraCaptureSetting.Queries;
using Scadex.Model.Entities;

namespace Scadex.Business.Concrete;

public class CameraCaptureSettingService : ICameraCaptureSettingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CameraCaptureSettingService> _logger;
    private readonly IMapper _mapper;

    private const string CacheKey = "scadex_settings_camera_capture";

    public CameraCaptureSettingService(IUnitOfWork unitOfWork, IValidationService validationService, ICacheService cacheService, ILogger<CameraCaptureSettingService> logger, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _cacheService = cacheService;
        _logger = logger;
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<CameraCaptureSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var cached = _cacheService.Get(CacheKey);
        if (cached.IsSuccess && !string.IsNullOrWhiteSpace(cached.Data))
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<CameraCaptureSettings>(cached.Data);
                if (parsed is not null) return parsed;
            }
            catch (JsonException)
            {
                // Bozuk girdi "onbellek yok" gibi ele alinir.
            }
        }

        var row = await _unitOfWork.CameraCaptureSettings.GetAsync(
            where: s => s.Id == CameraCaptureSetting.SingleRowId,
            cancellationToken: cancellationToken);

        if (row is null)
        {
            _logger.LogWarning("Kamera cekim ayar satiri yok; sinif varsayilanlari kullaniliyor.");
            return new CameraCaptureSettings();
        }

        var settings = _mapper.Map<CameraCaptureSettings>(row);

        _cacheService.Add(CacheKey, settings);
        return settings;
    }

    /// <inheritdoc/>
    public async Task<Result<CameraCaptureSettingDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);

        return Result<CameraCaptureSettingDto>.Success(_mapper.Map<CameraCaptureSettingDto>(settings));
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateAsync(CameraCaptureSettingUpdateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for CameraCaptureSettingUpdateDto");

        var row = await _unitOfWork.CameraCaptureSettings.GetAsync(
            where: s => s.Id == CameraCaptureSetting.SingleRowId,
            tracking: true,
            cancellationToken: cancellationToken);

        if (row is null)
            return Result.NotFound(description: "Kamera cekim ayar satiri bulunamadi");

        // Izlenen satirin UZERINE yazilir; Id ve audit alanlari profilde Ignore'dur.
        _mapper.Map(request, row);

        await _unitOfWork.CameraCaptureSettings.UpdateAndSaveAsync(row, cancellationToken);

        _cacheService.Remove(CacheKey);
        return Result.Success();
    }
}
