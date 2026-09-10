using AutoMapper;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Scadex.Business.Abstract;
using Scadex.Business.Settings;
using Scadex.Core.Utils.Caching;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Core.Utils.Validation;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.MediaGatewaySetting.Commands;
using Scadex.Model.Dtos.MediaGatewaySetting.Queries;
using Scadex.Model.Entities;

namespace Scadex.Business.Concrete;

public class MediaGatewaySettingService : IMediaGatewaySettingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<MediaGatewaySettingService> _logger;
    private readonly IMapper _mapper;

    private const string CacheKey = "scadex_settings_media_gateway";

    public MediaGatewaySettingService(IUnitOfWork unitOfWork, IValidationService validationService, ICacheService cacheService, ILogger<MediaGatewaySettingService> logger, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _cacheService = cacheService;
        _logger = logger;
        _mapper = mapper;
    }

    /// <inheritdoc/>
    public async Task<MediaGatewaySettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var cached = _cacheService.Get(CacheKey);
        if (cached.IsSuccess && !string.IsNullOrWhiteSpace(cached.Data))
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<MediaGatewaySettings>(cached.Data);
                if (parsed is not null) return parsed;
            }
            catch (JsonException)
            {
                // Bozuk girdi "onbellek yok" gibi ele alinir; asagida veritabanina dusulur.
            }
        }

        var row = await _unitOfWork.MediaGatewaySettings.GetAsync(
            where: s => s.Id == MediaGatewaySetting.SingleRowId,
            cancellationToken: cancellationToken);

        if (row is null)
        {
            // Satir seed ile geliyor; yoksa migration uygulanmamis ya da satir elle
            // silinmistir. ONBELLEGE YAZILMAZ ki satir dogdugu anda okunabilsin.
            _logger.LogWarning("Medya gecidi ayar satiri yok; sinif varsayilanlari kullaniliyor.");
            return new MediaGatewaySettings();
        }

        var settings = _mapper.Map<MediaGatewaySettings>(row);

        _cacheService.Add(CacheKey, settings);
        return settings;
    }

    /// <inheritdoc/>
    public async Task<Result<MediaGatewaySettingDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);

        return Result<MediaGatewaySettingDto>.Success(_mapper.Map<MediaGatewaySettingDto>(settings));
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateAsync(MediaGatewaySettingUpdateDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validationService.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Result.Validation(validationResult.Failures, description: "Validation failed for MediaGatewaySettingUpdateDto");

        var row = await _unitOfWork.MediaGatewaySettings.GetAsync(
            where: s => s.Id == MediaGatewaySetting.SingleRowId,
            tracking: true,
            cancellationToken: cancellationToken);

        if (row is null)
            return Result.NotFound(description: "Medya gecidi ayar satiri bulunamadi");

        // Izlenen satirin UZERINE yazilir; Id ve audit alanlari profilde Ignore'dur.
        // Saniye -> "10s" cevirisi de mapping profilindedir (tek yer).
        _mapper.Map(request, row);

        await _unitOfWork.MediaGatewaySettings.UpdateAndSaveAsync(row, cancellationToken);

        _cacheService.Remove(CacheKey);
        return Result.Success();
    }
}
