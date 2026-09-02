using System.Text.Json.Serialization;
using Scadex.Core.Model;
using Scadex.Model.Dtos.Diagram.Commands.Abstract;

namespace Scadex.Model.Dtos.Diagram.Commands;

/// <summary>
/// Tek bir tipin(DeviceDraft, ConnectionDraft, DiagramAnnotationDraft ...) degisiklik kumesi. <para/>
/// Guid'i istemci uretir, draftın yeni mi yoksa mevcut mu oldugu Id'nin veritabaninda bulunup bulunmamasinda öğrenilir.
/// </summary>
public class EntityDelta<T> : IDto where T : IIdentifiableDraft
{
    public List<T> Upserted { get; set; } = [];
    public List<Guid> Deleted { get; set; } = [];

    /// <summary> Iki liste de bossa bu aile icin hicbir is yapilmaz. </summary>
    [JsonIgnore]
    public bool IsEmpty => Upserted.Count == 0 && Deleted.Count == 0;
}
