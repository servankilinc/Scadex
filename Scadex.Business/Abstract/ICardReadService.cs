using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Scada.Commands;

namespace Scadex.Business.Abstract;

public interface ICardReadService
{
    /// <summary> Kabini MAC adresinden, kartı kullanıcılardandan doğrular ve (<c>IScadaEventObserver</c>) ile yayınlar. </summary>
    Task<Result> IngestAsync(ScadaCardReadRequest request, CancellationToken cancellationToken = default);
}
