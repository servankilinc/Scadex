using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scadex.Business.Abstract;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.ComponentTemplate.Commands;
using Scadex.Model.Dtos.ComponentTemplate.Queries;
using Scadex.WebAPI.Controllers.Base;
using Scadex.WebAPI.Tools;

namespace Scadex.WebAPI.Controllers;

[EnableRateLimiting(RateLimiterKey.Default)]
public class ComponentTemplateController : BaseController
{
    #region Template Dosya Parametreleri
    private const string RelativeFolder = "uploads/templates";
    private const long MaxBytes = 4 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".svg"] = "image/svg+xml"
    };
    private static string AllowedExtensionList => string.Join(", ", AllowedTypes.Keys); 
    #endregion

    private readonly IComponentTemplateService _componentTemplateService;
    private readonly IWebHostEnvironment _webHostEnvironment;
    public ComponentTemplateController(ILogger<ComponentTemplateController> logger, IComponentTemplateService componentTemplateService, IWebHostEnvironment webHostEnvironment) : base(logger)
    {
        _componentTemplateService = componentTemplateService;
        _webHostEnvironment = webHostEnvironment;
    }


    [HttpGet("palette")]
    public async Task<IActionResult> GetPalette(CancellationToken cancellationToken)
    {
        var result = await _componentTemplateService.GetPaletteAsync(cancellationToken);
        return ToAction(result);
    }

    /// <summary> Sablonu ve pin semasini TEK transaction'da olusturur. </summary>
    [HttpPost]
    public async Task<IActionResult> Create(ComponentTemplateCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await _componentTemplateService.CreateAsync(request, cancellationToken);
        return ToAction(result);
    }

    /// <summary> Sablon arka plan gorselini yukler ve goreli URL'sini doner. Yuklenen dosya <c>wwwroot/uploads/templates</c> altina yazilir </summary>
    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken cancellationToken)
    {
        var result = await SaveTemplateFileAsync(file, cancellationToken);
        return ToAction(result);
    }

    #region Get
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _componentTemplateService.GetComponentTemplateDetailDtoAsync(id: id);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/base")]
    public async Task<IActionResult> GetBase(Guid id)
    {
        var result = await _componentTemplateService.GetBaseAsync(id: id);
        return ToAction(result);
    }

    [HttpGet("{id:guid}/componentTemplateDetailDto")]
    public async Task<IActionResult> GetComponentTemplateDetailDto(Guid id)
    {
        var result = await _componentTemplateService.GetComponentTemplateDetailDtoAsync(id: id);
        return ToAction(result);
    }
    #endregion

    #region List
    [HttpPost("list")]
    public async Task<IActionResult> GetList(DynamicRequest? request = default)
    {
        var result = await _componentTemplateService.GetComponentTemplateDetailDtoListAsync(request);
        return ToAction(result);
    }

    [HttpPost("list/base")]
    public async Task<IActionResult> GetBaseList(DynamicRequest? request = default)
    {
        var result = await _componentTemplateService.GetBaseListAsync(request);
        return ToAction(result);
    }

    [HttpPost("list/componentTemplateDetailDto")]
    public async Task<IActionResult> GetComponentTemplateDetailDtoList(DynamicRequest? request = default)
    {
        var result = await _componentTemplateService.GetComponentTemplateDetailDtoListAsync(request);
        return ToAction(result);
    } 
    #endregion

    #region Helpers
    /// <summary> Dosyayi yazar ve istemcinin kullanacagi GOreli URL'yi doner. </summary>
    public async Task<Result<TemplateImageDto>> SaveTemplateFileAsync(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return Result<TemplateImageDto>.Failure("Dosya boş gönderildi.");

        if (file.Length > MaxBytes)
            return Result<TemplateImageDto>.Failure($"Dosya en fazla {MaxBytes / (1024 * 1024)} MB olabilir");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedTypes.ContainsKey(extension))
            return Result<TemplateImageDto>.Failure($"Yalnizca su uzantilar kabul edilir: {AllowedExtensionList}");

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        var webRoot = _webHostEnvironment.WebRootPath ?? Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");
        var folder = Path.Combine(webRoot, RelativeFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(folder);

        var fullPath = Path.Combine(folder, fileName);
        await using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return Result<TemplateImageDto>.Success(new TemplateImageDto { Url = $"/{RelativeFolder}/{fileName}" });
    }
    #endregion
}
