using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Camera.Queries;

/// <summary>
/// Merkeze alinmis tek bir cekim. Sozlesme: <c>docs/api-contract/11-camera.md</c>
///
/// Ayna: <c>Frontend/src/models/camera/queries/cameraCaptureDto.ts</c>
///
/// <b>Basarisiz cekim de bir satirdir</b> (<c>Status = Failed</c>,
/// <c>FailureReason</c> dolu, <c>StorageKey</c> null): "o anda goruntu YOK"
/// bilgisinin kendisi delildir ve satiri hic yazmamak o bilgiyi silerdi.
/// </summary>
public class CameraCaptureDto : IDto
{
    public long Id { get; set; }
    public Guid CameraId { get; set; }

    public CaptureType Type { get; set; }
    public CaptureStatus Status { get; set; }

    /// <summary>
    /// Goruntunun ANI (UTC) — satirin olusturulma zamani degil. Klipte kaydin
    /// fiilen basladigi andir, istegin geldigi an degil.
    /// </summary>
    public DateTime CapturedAtUtc { get; set; }

    /// <summary>Klip suresi (saniye); anlik goruntude <c>null</c>.</summary>
    public int? DurationSec { get; set; }

    /// <summary>
    /// Dosyanin <c>wwwroot</c>'a gore goreli yolu. Tam URL DEGILDIR — istemci
    /// kendi API kokuyle birlestirir. <c>Pending</c> ve <c>Failed</c> iken null.
    /// </summary>
    public string? StorageKey { get; set; }

    public long? SizeBytes { get; set; }

    /// <summary><c>Failed</c> ise sebep.</summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Saklama suresinin sonu (UTC); <c>null</c> ise suresiz. Yazma aninda
    /// sabitlenir ki politika sonradan kisaltildiginda mevcut delilin omru
    /// geriye donuk degismesin.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Cekimi elle isteyen operator.</summary>
    public Guid? RequestedByUserId { get; set; }
}
