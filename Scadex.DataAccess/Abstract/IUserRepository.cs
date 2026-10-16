using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IUserRepository : IRepository<User>, IRepositoryAsync<User>
{
    /// <summary> Kullanıcı başına rol adları (pasif roller dahil). Rolü olmayan kullanıcı sözlükte yer almaz. </summary>
    Task<Dictionary<Guid, List<string>>> GetRoleNamesByUserAsync(CancellationToken cancellationToken = default);

    /// <summary> Role atanmış kullanıcılar (pasifler dahil), ada göre sıralı. </summary>
    Task<List<User>> GetUsersInRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
}
