using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Dtos.Diagram.Queries;
using Scadex.Model.Dtos.Diagram.Queries.Items;

namespace Scadex.Business.Abstract;

/// <summary>
/// Diyagram tablolarının (Device, Pin, IoChannel, Connection, DiagramAnnotation) tek yazım yolu. Her editör eylemi kendi
/// ucuna anında gider; toplu "kaydet" deltası 2026-10-27'de kaldırıldı.
/// </summary>
public interface IDiagramService
{
    Task<Result<DiagramDto>> GetAsync(Guid cabinetId, CancellationToken cancellationToken = default);

    /// <summary> Cihaz + şablondan pinler + kanallar + ek satır. Yanıt, sunucunun ürettiği pin/kanal Id'lerini taşır. </summary>
    Task<Result<DiagramDeviceDto>> CreateDeviceAsync(Guid cabinetId, DeviceCreateRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateDeviceAsync(Guid cabinetId, Guid deviceId, DeviceUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result> CreateConnectionAsync(Guid cabinetId, ConnectionCreateRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateConnectionAsync(Guid cabinetId, Guid connectionId, ConnectionUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result> CreateAnnotationAsync(Guid cabinetId, DiagramAnnotationCreateRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateAnnotationAsync(Guid cabinetId, Guid annotationId, DiagramAnnotationUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result> UpdateLayoutAsync(Guid cabinetId, DiagramLayoutRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid cabinetId, DiagramDeleteRequest request, CancellationToken cancellationToken = default);
}
