using FluentValidation;
using Scadex.Core.Model;

namespace Scadex.Model.Dtos.MediaGatewaySetting.Commands;

public class MediaGatewaySettingUpdateDto : IDto
{
    public int ApiTimeoutMs { get; set; }
    public string ApiBaseUrl { get; set; } = null!;
    public string WebRtcPublicBaseUrl { get; set; } = null!;
    public int TokenTtlSeconds { get; set; }

    /// <summary> Son izleyici ayrıldıktan sonra RTSP oturumunun kapanma süresi (Path silinmez) </summary>
    public int SourceOnDemandCloseAfterSec { get; set; }

    public string RtspTransport { get; set; } = null!;
    public string RecordRoot { get; set; } = null!;
}

public class MediaGatewaySettingUpdateDtoValidator : AbstractValidator<MediaGatewaySettingUpdateDto>
{
    public MediaGatewaySettingUpdateDtoValidator()
    {
        RuleFor(v => v.ApiTimeoutMs).InclusiveBetween(1000, 300000).WithMessage("Zaman aşımı 1.000-300.000 ms arasında olmalı");

        RuleFor(v => v.ApiBaseUrl).NotEmpty().WithMessage("Control API adresi zorunlu");
        RuleFor(v => v.ApiBaseUrl).Must(BeAbsoluteHttpUrl).WithMessage("Control API adresi geçerli bir http(s) adresi olmalı");

        RuleFor(v => v.WebRtcPublicBaseUrl).NotEmpty().WithMessage("WebRTC adresi zorunlu");
        RuleFor(v => v.WebRtcPublicBaseUrl).Must(BeAbsoluteHttpUrl).WithMessage("WebRTC adresi geçerli bir http(s) adresi olmalı");

        RuleFor(v => v.TokenTtlSeconds).InclusiveBetween(10, 3600).WithMessage("Bilet ömrü 10-3600 saniye arasında olmalı");

        RuleFor(v => v.SourceOnDemandCloseAfterSec).InclusiveBetween(0, 3600).WithMessage("Oturum kapanma süresi 0-3600 saniye arasında olmalı");

        // MediaMTX yalnizca bu ucunu tanir; serbest metin gonderilirse yol kurulamaz.
        RuleFor(v => v.RtspTransport).Must(v => v is "tcp" or "udp" or "multicast" or "automatic")
            .WithMessage("RTSP taşıma katmanı tcp, udp, multicast veya automatic olmalı");

        RuleFor(v => v.RecordRoot).NotEmpty().WithMessage("Kayıt kök dizini zorunlu");
    }

    private static bool BeAbsoluteHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
