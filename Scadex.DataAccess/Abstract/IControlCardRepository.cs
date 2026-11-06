using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IControlCardRepository : IRepository<ControlCard>, IRepositoryAsync<ControlCard>
{
    /// <summary>
    /// Otomasyonun aktif kontrol kartı (SCADA adresi, açık/kapalı, zaman aşımı, son ingest); yoksa <c>null</c>.
    /// Otomasyon başına en fazla bir aktif kart vardır (diyagram doğrulaması zorlar, 2026-11-04).
    /// </summary>
    Task<ControlCard?> GetActiveForAutomationAsync(Guid automationId, bool tracking = false, CancellationToken cancellationToken = default);
}
