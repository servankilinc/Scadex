using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Diagram.Commands;
using Scadex.Model.Entities;

namespace Scadex.Business.Concrete;

// Diyagramın SİLME ucu: seçimdeki cihazlar, kablolar ve notlar tek istekte.
public partial class DiagramService
{
    /// <summary>
    /// Bu kabinde karsiligi bulunamayan Id'ler SESSIZCE ATLANIR: iki kez gonderilen ya da zaten kalkmis bir silme hata
    /// degildir. Cihazin pinleri ve kablolari birlikte kalkar; istemcinin onlari ayrica gondermesi gerekmez.
    ///
    /// Tek SaveChanges yeter: istekte yalnizca silme var (soft-delete'ler UPDATE, not DELETE). Eski toplu kaydetmedeki iki
    /// fazli akisin sebebi silme ile ayni pinlere yeniden cizilen kablonun INSERT'inin ayni batch'e dusmesiydi.
    /// </summary>
    public async Task<Result> DeleteAsync(Guid cabinetId, DiagramDeleteRequest request, CancellationToken cancellationToken = default)
    {
        if (!await IsCabinetActiveAsync(cabinetId, cancellationToken))
            return Result.NotFound(description: "Kabin bulunamadi veya pasif durumda");

        var deviceIds = request.DeviceIds.Distinct().ToList();
        var connectionIds = request.ConnectionIds.Distinct().ToList();
        var annotationIds = request.AnnotationIds.Distinct().ToList();

        var devices = deviceIds.Count == 0 ? [] : await _unitOfWork.Devices.GetAllAsync(
            where: d => deviceIds.Contains(d.Id) && d.CabinetId == cabinetId && d.IsActive,
            cancellationToken: cancellationToken) ?? [];
        var liveDeviceIds = devices.Select(d => d.Id).ToList();

        // Pin yalnizca cihaziyla birlikte silinir — cihaz uzerinde tekil pin silme yok.
        var pins = liveDeviceIds.Count == 0 ? [] : await _unitOfWork.Pins.GetAllAsync(
            where: p => liveDeviceIds.Contains(p.DeviceId),
            cancellationToken: cancellationToken) ?? [];

        // Dogrudan silinen kablolar + cihazi kalktigi icin birlikte kalkanlar, tek sorguda.
        var connections = connectionIds.Count == 0 && liveDeviceIds.Count == 0 ? [] : await _unitOfWork.Connections.GetAllAsync(
            where: c => c.CabinetId == cabinetId
                     && (connectionIds.Contains(c.Id)
                         || liveDeviceIds.Contains(c.SourcePin!.DeviceId)
                         || liveDeviceIds.Contains(c.TargetPin!.DeviceId)),
            cancellationToken: cancellationToken) ?? [];

        var annotations = annotationIds.Count == 0 ? [] : await _unitOfWork.DiagramAnnotations.GetAllAsync(
            where: a => annotationIds.Contains(a.Id) && a.CabinetId == cabinetId,
            cancellationToken: cancellationToken) ?? [];

        // Connection ve Pin ISoftDeletableEntity: interceptor Remove'u IsDeleted = true'ya cevirir.
        if (connections.Count > 0) _unitOfWork.Connections.Delete(connections);
        if (pins.Count > 0) _unitOfWork.Pins.Delete(pins);

        // Cihazda Remove() CAGRILMAZ: Device IActivatableEntity oldugu icin interceptor hard delete'te exception atar —
        // kaldirmak IsActive = false demektir.
        foreach (var device in devices) device.IsActive = false;

        // Sistemdeki TEK hard delete: DiagramAnnotation ne aktiflestirilebilir ne soft-delete edilebilir; kimse ona FK ile
        // bagli olmadigi icin oksuz satir birakmaz.
        if (annotations.Count > 0) _unitOfWork.DiagramAnnotations.Delete(annotations);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
