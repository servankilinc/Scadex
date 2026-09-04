using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Entities;

namespace Scadex.Business.Utils.SnapshotGateway;

/// <summary>Kameradan donen tek kare.</summary>
/// <param name="Content">Ham baytlar — <b>veritabanina YAZILMAZ</b>.</param>
/// <param name="ContentType">Kameranin bildirdigi tip; bilinmiyorsa <c>image/jpeg</c>.</param>
public sealed record SnapshotPayload(byte[] Content, string ContentType);

public interface ISnapshotGateway
{
    Task<Result<SnapshotPayload>> GetSnapshotAsync(Camera camera, CancellationToken cancellationToken = default);
}
