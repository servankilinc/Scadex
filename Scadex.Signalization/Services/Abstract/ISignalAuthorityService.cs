using Scadex.Core.Utils.ResultPattern;
using Scadex.Signalization.Dtos.Authority.Commands;
using Scadex.Signalization.Dtos.Authority.Queries;

namespace Scadex.Signalization.Services.Abstract;

public interface ISignalAuthorityService
{
    /// <summary> Tum kurumlar (pasifler dahil — geri alinabilsin diye), rol adiyla. </summary>
    Task<Result<ICollection<SignalAuthorityDto>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Kurum listesini tam haliyle esitler: kimlige gore upsert, gövdede olmayan pasife. Aktif bir ic kapida kullanilan
    /// kurum pasife alinamaz (400).
    /// </summary>
    Task<Result> SaveAsync(SignalAuthoritySaveRequest request, CancellationToken cancellationToken = default);
}
