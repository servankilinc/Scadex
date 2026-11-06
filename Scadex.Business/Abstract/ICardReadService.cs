using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Scada.Commands;

namespace Scadex.Business.Abstract;

public interface ICardReadService
{
    /// <summary>
    /// Kartı kullanıcılardan doğrular ve (<c>IScadaEventObserver</c>) ile yayınlar. <paramref name="automationId"/> isteği gönderen kontrol
    /// kartının Basic Auth kimliğinden gelir (controller), gövdeden değil.
    /// </summary>
    Task<Result> IngestAsync(Guid automationId, ScadaCardReadRequest request, CancellationToken cancellationToken = default);
}
