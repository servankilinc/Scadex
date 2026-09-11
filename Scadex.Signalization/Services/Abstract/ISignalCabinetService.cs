using Scadex.Core.Utils.ResultPattern;
using Scadex.Signalization.Dtos.Config.Commands;
using Scadex.Signalization.Dtos.Config.Queries;

namespace Scadex.Signalization.Services.Abstract;

public interface ISignalCabinetService
{
    /// <summary> Kabinin yapilandirma agaci; hic yapilandirilmadiysa varsayilanlarla <c>IsConfigured = false</c>. </summary>
    Task<Result<SignalCabinetDto>> GetAsync(Guid cabinetId, CancellationToken cancellationToken = default);

    /// <summary> Yapilandirma ekraninin secenekleri: kabinin kanallari, kameralari ve kurumlar. </summary>
    Task<Result<SignalCabinetOptionsDto>> GetOptionsAsync(Guid cabinetId, CancellationToken cancellationToken = default);

    /// <summary> Tam agaci kaydeder (tek transaction). Tek yazim yolu budur. </summary>
    Task<Result> SaveAsync(Guid cabinetId, SignalCabinetSaveRequest request, CancellationToken cancellationToken = default);
}
