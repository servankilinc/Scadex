using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Dtos.Diagram.Queries;

namespace Scadex.Business.Abstract;

/// <summary>
/// Diyagram tablolarının (Device, Pin, IoChannel, Connection, DiagramAnnotation) tek yazım yolu. Her editör eylemi kendi
/// ucuna anında gider; toplu "kaydet" deltası 2026-10-27'de kaldırıldı.
/// </summary>
public interface IDiagramService
{
    Task<Result<DiagramDto>> GetAsync(Guid automationId, CancellationToken cancellationToken = default);

    /// <summary> Cihaz + şablondan pinler + kanallar + ek satır. Yanıt, sunucunun ürettiği pin/kanal Id'lerini taşır. </summary>
    Task<Result<DiagramDeviceDto>> CreateDeviceAsync(Guid automationId, DeviceCreateRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateDeviceAsync(Guid automationId, Guid deviceId, DeviceUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result> CreateConnectionAsync(Guid automationId, ConnectionCreateRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateConnectionAsync(Guid automationId, Guid connectionId, ConnectionUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result> CreateAnnotationAsync(Guid automationId, DiagramAnnotationCreateRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateAnnotationAsync(Guid automationId, Guid annotationId, DiagramAnnotationUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result> UpdateLayoutAsync(Guid automationId, DiagramLayoutRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid automationId, DiagramDeleteRequest request, CancellationToken cancellationToken = default);
}
