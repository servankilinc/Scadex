using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CabinetOs.Business.Settings;
using CabinetOs.Business.Utils.CameraProtocolProfile;
using CabinetOs.Core.Utils.ResultPattern;
using CabinetOs.Model.Entities;
using Microsoft.Extensions.Logging;
using static CabinetOs.Model.Enums.EntityEnums;

namespace CabinetOs.Business.Utils.MediaGateway;

/// <summary>
/// <see cref="IMediaGateway"/>'in MediaMTX Control API implementasyonu.
///
/// Typed <c>HttpClient</c>: <c>ScadaCommandGateway</c>'den farkli olarak adres
/// GOVDEDE sabittir — tek bir medya gecidi var ve adresi yapilandirmadan gelir.
/// (SCADA'da adres kabin basina degistigi icin <c>BaseAddress</c> kurulamiyordu.)
///
/// <b>Tekrar deneme handler'i takili DEGIL</b>, kod tabaninin genel karari.
/// </summary>
public class MediaMtxGateway : IMediaGateway
{
    private readonly HttpClient _httpClient;
    private readonly MediaMtxSettings _settings;
    private readonly ICameraProtocolProfileResolver _profileResolver;
    private readonly ILogger<MediaMtxGateway> _logger;

    public MediaMtxGateway(
        HttpClient httpClient,
        MediaMtxSettings settings,
        ICameraProtocolProfileResolver profileResolver,
        ILogger<MediaMtxGateway> logger)
    {
        _settings = settings;
        _profileResolver = profileResolver;
        _logger = logger;

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_settings.ApiBaseUrl.TrimEnd('/') + "/");
    }

    public Task<Result> EnsureLivePathAsync(Camera camera, StreamProfile profile, CancellationToken cancellationToken = default)
    {
        var sourceUrl = _profileResolver.Resolve(camera).BuildRtspUrl(camera, profile);

        var payload = new Dictionary<string, object?>
        {
            ["source"] = sourceUrl,
            // Talep uzerine baglan: izleyicisi olmayan bir kamera icin RTSP
            // oturumu acik tutmak, GSM hattinda kamera basina ~2-4 Mbps'i
            // bosuna harcardi.
            ["sourceOnDemand"] = true,
            ["sourceOnDemandCloseAfter"] = _settings.SourceOnDemandCloseAfter,
            ["rtspTransport"] = _settings.RtspTransport
        };

        // Canli yol: ONCE SOR. Gerekcesi asagida, EnsureUnchangedOrWriteAsync'te.
        return EnsureUnchangedOrWriteAsync(IMediaGateway.LivePathName(camera.Id, profile), payload, cancellationToken);
    }

    public Task<Result> EnsureClipPathAsync(
        Camera camera,
        long captureId,
        string recordPath,
        string segmentDuration,
        CancellationToken cancellationToken = default)
    {
        // Klip her zaman ANA AKIMDAN alinir: delil, tali akimin dusuk
        // cozunurlugunde ise yaramaz.
        var sourceUrl = _profileResolver.Resolve(camera).BuildRtspUrl(camera, StreamProfile.Main);

        var payload = new Dictionary<string, object?>
        {
            ["source"] = sourceUrl,
            // Talep BEKLENMEZ: kaydin hemen baslamasi gerekiyor, yoksa ilk
            // izleyici gelene kadar hicbir sey yazilmaz.
            ["sourceOnDemand"] = false,
            ["rtspTransport"] = _settings.RtspTransport,
            ["record"] = true,
            ["recordPath"] = recordPath,
            // fmp4 -> tarayicinin dogrudan oynatabildigi .mp4 dosyasi.
            // mpegts secilseydi ayrica donusturmek gerekirdi.
            ["recordFormat"] = "fmp4",
            ["recordSegmentDuration"] = segmentDuration,
            // Otomatik silme KAPALI: dosyayi biz tasiyacagiz. MediaMTX'in
            // silmesi, tam da okumaya calistigimiz dosyayi kaybettirebilirdi.
            ["recordDeleteAfter"] = "0s"
        };

        // Klip yolu DOGRUDAN yazilir, canli yoldaki gibi once sorulmaz.
        //
        // Ad her cekimde benzersiz (clip_{captureId}), yani yol normalde HIC
        // var olmaz — sormak her klipte bir 404 uretir ve MediaMTX onu ERR
        // seviyesinde loglar. Gercek bir sorun degil ama gercek bir sorun gibi
        // gorunup teshisi zorlastiriyordu.
        //
        // Canli yolda "once sor" olmasinin sebebi buradaki durumu kapsamiyor:
        // orada replace calisan bir yayini kopartiyordu. Klip yolunun izleyicisi
        // yoktur, dolayisiyla add->replace burada zararsiz — ustelik onceki bir
        // cokmeden kalmis bir yolu da kendiliginden onarir.
        return AddOrReplacePathAsync(IMediaGateway.ClipPathName(captureId), payload, cancellationToken);
    }

    /// <summary>
    /// Yolu istenen yapilandirmaya getirir: yoksa ekler, varsa ve FARKLIYSA
    /// degistirir, ayniysa DOKUNMAZ.
    ///
    /// <b>Once sormak sart, once yazmak degil.</b> MediaMTX yollari
    /// yapilandirmasinda KALICIDIR; ilk biletten sonra yol her zaman vardir.
    /// Dolayisiyla "once add dene, cakisirsa replace'e dus" yaklasiminda normal
    /// durum <c>add</c> degil <c>replace</c> olur — ve <c>replace</c> her
    /// cagrida bir yapilandirma yeniden yuklemesi tetikleyip YOLU YENIDEN
    /// KURAR. Bu da o an izleyen oturumlari dusurur: grid'de ikinci kutucuk
    /// baglaninca birincinin goruntusu kopar, o yeniden baglanir ve digerini
    /// kopartir — kendini besleyen bir dongu.
    ///
    /// (Bu kod bir sure tam da oyle yazilmisti; MediaMTX log'unda her bilette
    /// bir <c>path already exists</c> + <c>reloading configuration</c> ciftiyle
    /// yakalandi.)
    ///
    /// Fazladan bir GET'in bedeli olculdu: Control API cagrilari 3-14 ms.
    ///
    /// <b>Yalnizca CANLI yollar icindir.</b> Klip yolu her cekimde benzersiz bir
    /// ad alir ve hicbir zaman onceden var olmaz; orada sormak yalnizca bos bir
    /// 404 (ve MediaMTX log'unda bir ERR satiri) uretirdi —
    /// bkz. <see cref="AddOrReplacePathAsync"/>.
    /// </summary>
    private async Task<Result> EnsureUnchangedOrWriteAsync(
        string pathName, Dictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        try
        {
            using var getResponse = await _httpClient.GetAsync($"v3/config/paths/get/{pathName}", cancellationToken);

            if (getResponse.StatusCode == HttpStatusCode.NotFound)
                return await WritePathAsync("add", pathName, payload, cancellationToken);

            if (getResponse.IsSuccessStatusCode)
            {
                var current = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

                // Hicbir sey degismediyse YAZMA. Tek kazanci gurultuyu azaltmak
                // degil: yazmak yolu yeniden kurar ve izleyenleri dusururdu.
                if (MatchesDesiredConfig(current, payload))
                    return Result.Success();

                return await WritePathAsync("replace", pathName, payload, cancellationToken);
            }

            return Result.Failure(
                description: $"Medya geçidi yolu okunamadı: {await ReadErrorAsync(getResponse, cancellationToken)}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Cagiranin token'i iptal edilmedi => buraya yalnizca HttpClient'in
            // kendi zaman asimi dusebilir. "Gecit sessiz" ile "gecit yok"
            // ayirt edilebilsin diye ayri mesaj.
            _logger.LogError("Medya gecidi {Timeout} sn icinde yanit vermedi ({BaseAddress})",
                _httpClient.Timeout.TotalSeconds, _httpClient.BaseAddress);
            return Result.Failure(description: "Medya geçidi yanıt vermiyor.");
        }
        catch (HttpRequestException exception)
        {
            // Gecit ayakta degil. Kullaniciya gosterilecek en yararli bilgi bu:
            // kamera degil, MediaMTX calismiyor.
            _logger.LogError(exception, "Medya gecidine ulasilamadi ({BaseAddress})", _httpClient.BaseAddress);
            return Result.Failure(description: "Medya geçidine ulaşılamıyor. MediaMTX çalışmıyor olabilir.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Medya gecidi yolu yazilirken beklenmeyen hata: {PathName}", pathName);
            return Result.Failure(description: "Medya geçidi yapılandırılamadı.");
        }
    }

    /// <summary>
    /// Yolu dogrudan ekler; zaten varsa degistirir.
    ///
    /// <b>Yalnizca izleyicisi olmayan yollar icin</b> (bugun: klip). Canli yolda
    /// kullanilamaz — <c>replace</c> bir yapilandirma yeniden yuklemesi tetikler
    /// ve o an izleyen oturumlari dusurur; canli taraf bu yuzden
    /// <see cref="EnsureUnchangedOrWriteAsync"/> kullanir.
    ///
    /// <c>add</c> once deneniyor cunku burada normal durum GERCEKTEN odur:
    /// klip yolunun adi her cekimde benzersizdir. <c>replace</c>'e dusmek ancak
    /// onceki bir cokmeden yol kalmissa gerekir ve o durumu kendiliginden onarir.
    /// </summary>
    private async Task<Result> AddOrReplacePathAsync(
        string pathName, Dictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        try
        {
            using var addResponse = await _httpClient.PostAsJsonAsync($"v3/config/paths/add/{pathName}", payload, cancellationToken);
            if (addResponse.IsSuccessStatusCode) return Result.Success();

            string addError = await ReadErrorAsync(addResponse, cancellationToken);

            if (!addError.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return Result.Failure(description: $"Medya geçidi yolu oluşturulamadı: {addError}");

            return await WritePathAsync("replace", pathName, payload, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Medya gecidi {Timeout} sn icinde yanit vermedi ({BaseAddress})",
                _httpClient.Timeout.TotalSeconds, _httpClient.BaseAddress);
            return Result.Failure(description: "Medya geçidi yanıt vermiyor.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Medya gecidine ulasilamadi ({BaseAddress})", _httpClient.BaseAddress);
            return Result.Failure(description: "Medya geçidine ulaşılamıyor. MediaMTX çalışmıyor olabilir.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Medya gecidi yolu yazilirken beklenmeyen hata: {PathName}", pathName);
            return Result.Failure(description: "Medya geçidi yapılandırılamadı.");
        }
    }

    private async Task<Result> WritePathAsync(
        string verb, string pathName, Dictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync($"v3/config/paths/{verb}/{pathName}", payload, cancellationToken);
        if (response.IsSuccessStatusCode) return Result.Success();

        return Result.Failure(
            description: $"Medya geçidi yolu yazılamadı: {await ReadErrorAsync(response, cancellationToken)}");
    }

    /// <summary>
    /// Gecitteki yol, gondermek istedigimiz alanlarin HEPSINDE ayni mi?
    ///
    /// Karsilastirma <paramref name="desired"/> uzerinden doner, gelen JSON
    /// uzerinden degil: MediaMTX yuzlerce alan dondurur ve bizim yazmadigimiz
    /// her alanin farkli olmasi normaldir. Sozluk uzerinden donmek ayrica
    /// karsilastirmayi gonderilen alan listesiyle KENDILIGINDEN senkron tutar —
    /// payload'a yeni bir alan eklendiginde burayi guncellemeyi unutmak
    /// mumkun degil.
    ///
    /// Bilinmeyen bir alan ya da coz ulemeyen bir tip "farkli" sayilir: fazladan
    /// bir replace, sessizce bayat kalmis bir yapilandirmadan iyidir.
    /// </summary>
    private static bool MatchesDesiredConfig(JsonElement current, Dictionary<string, object?> desired)
    {
        foreach (var (key, expected) in desired)
        {
            if (!current.TryGetProperty(key, out var actual)) return false;

            bool same = expected switch
            {
                string text => actual.ValueKind == JsonValueKind.String && actual.GetString() == text,
                bool flag => actual.ValueKind == (flag ? JsonValueKind.True : JsonValueKind.False),
                null => actual.ValueKind == JsonValueKind.Null,
                _ => false
            };

            if (!same) return false;
        }

        return true;
    }

    public async Task<Result> DeletePathAsync(string pathName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.DeleteAsync($"v3/config/paths/delete/{pathName}", cancellationToken);

            // 404 = yol zaten yok. Cagiran acisindan silinmis olmasindan farksiz.
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                return Result.Success();

            return Result.Failure(description: $"Medya geçidi yolu silinemedi: {await ReadErrorAsync(response, cancellationToken)}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Medya gecidi yolu silinemedi: {PathName}", pathName);
            return Result.Failure(description: "Medya geçidi yolu silinemedi.");
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        string trimmed = body.Trim();

        if (trimmed.Length == 0) return $"HTTP {(int)response.StatusCode}";
        return trimmed.Length <= 512 ? trimmed : trimmed[..512];
    }
}
