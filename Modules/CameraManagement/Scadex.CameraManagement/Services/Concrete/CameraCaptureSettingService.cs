using AutoMapper;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using Scadex.CameraManagement.DataAccess;
using Scadex.CameraManagement.Model.Dtos.CaptureSetting.Commands;
using Scadex.CameraManagement.Model.Dtos.CaptureSetting.Queries;
using Scadex.CameraManagement.Model.Entities;
using Scadex.CameraManagement.Model.Settings;
using Scadex.CameraManagement.Services.Abstract;
using Scadex.Core.Utils.Caching;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;

namespace Scadex.CameraManagement.Services.Concrete;

public class CameraCaptureSettingService : ICameraCaptureSettingService
{
    private readonly CameraManagementDbContext _context;
    private readonly IValidationService _validationService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CameraCaptureSettingService> _logger;
    private readonly IMapper _mapper;

    private const string CacheKey = "scadex_settings_camera_capture";

    public CameraCaptureSettingService(CameraManagementDbContext context, IValidationService validationService, ICacheService cacheService, ILogger<CameraCaptureSettingService> logger, IMapper mapper)
    {
        _context = context;
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

        var row = await _context.CameraCaptureSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == CameraCaptureSetting.SingleRowId, cancellationToken);

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

        var row = await _context.CameraCaptureSettings
            .FirstOrDefaultAsync(s => s.Id == CameraCaptureSetting.SingleRowId, cancellationToken);

        if (row is null)
            return Result.NotFound(description: "Kamera cekim ayar satiri bulunamadi");

        // Izlenen satirin UZERINE yazilir; Id ve audit alanlari profilde Ignore'dur.
        _mapper.Map(request, row);

        await _context.SaveChangesAsync(cancellationToken);

        _cacheService.Remove(CacheKey);
        return Result.Success();
    }
}
