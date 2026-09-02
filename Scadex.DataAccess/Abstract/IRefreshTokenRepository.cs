using System.Linq.Expressions;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Abstract;

public interface IRefreshTokenRepository : IRepository<RefreshToken>, IRepositoryAsync<RefreshToken>
{
    void RevokeDeviceRefreshTokens(Expression<Func<RefreshToken, bool>> where);
    Task RevokeDeviceRefreshTokensAsync(Expression<Func<RefreshToken, bool>> where, CancellationToken cancellationToken = default);
}