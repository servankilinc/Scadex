using Scadex.Core.Model;

namespace Scadex.RemoteDesk.Model.Dtos.View.Queries;

/// <summary>
/// İzleme başladı. <c>WhepUrl</c> / <c>Token</c> / <c>ExpirationUtc</c> kameranın <c>StreamTokenDto</c>'suyla aynı adlardadır: tarayıcıdaki
/// WHEP oynatıcısı değişmeden çalışır. Kiralama <c>LeaseRenewSec</c>'te bir yenilenmezse düşer (RemoteDesk.md § 8.3).
/// </summary>
public class ScreenViewDto : IDto
{
    /// <summary> Kiralama kimliği — yenileme ve bırakma bununla yapılır. </summary>
    public Guid ViewId { get; set; }

    public string WhepUrl { get; set; } = null!;

    /// <summary> Okuma bileti (WHEP parolası); kısa ömürlü, el sıkışmada bir kez doğrulanır. </summary>
    public string Token { get; set; } = null!;

    public DateTime ExpirationUtc { get; set; }

    public int LeaseRenewSec { get; set; }
}
