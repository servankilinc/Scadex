using CabinetOs.Core.Utils.ResultPattern;
using CabinetOs.Model.Entities;

namespace CabinetOs.Business.Utils.SnapshotGateway;

/// <summary>Kameradan donen tek kare.</summary>
/// <param name="Content">Ham baytlar — <b>veritabanina YAZILMAZ</b>.</param>
/// <param name="ContentType">Kameranin bildirdigi tip; bilinmiyorsa <c>image/jpeg</c>.</param>
public sealed record SnapshotPayload(byte[] Content, string ContentType);

/// <summary>
/// Kameradan anlik goruntu alan gecit (Hikvision'da ISAPI).
///
/// <b>Kimlik dogrulama ELLE yazilir.</b> <c>HttpClientHandler.Credentials</c>
/// kullanilamaz: kimlik handler'a baglanir, handler ise <c>IHttpClientFactory</c>
/// havuzunda paylasilir — bir kameranin parolasi baska bir kameraya gonderilirdi.
///
/// <b>Tekrar deneme handler'i takili DEGIL</b> (<c>ScadaCommandGateway</c> ile
/// ayni gerekce). Zaman asimi gecidin KENDI
/// <c>CancellationTokenSource</c>'uyla uygulanir ki "kamera yavas" ile "istek
/// iptal edildi" ayni istisnaya dusmesin.
/// </summary>
public interface ISnapshotGateway
{
    Task<Result<SnapshotPayload>> GetSnapshotAsync(Camera camera, CancellationToken cancellationToken = default);
}
