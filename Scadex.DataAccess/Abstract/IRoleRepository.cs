using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IRoleRepository : IRepository<Role>, IRepositoryAsync<Role>
{
    /// <summary> Rol başına atanmış kullanıcı sayısı (pasif kullanıcılar dahil). Kullanıcısı olmayan rol sözlükte yer almaz. </summary>
    Task<Dictionary<Guid, int>> GetUserCountsAsync(CancellationToken cancellationToken = default);
}
