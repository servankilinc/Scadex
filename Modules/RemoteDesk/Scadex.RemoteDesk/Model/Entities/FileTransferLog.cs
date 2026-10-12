using Scadex.Core.Model;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Model.Entities;

/// <summary>
/// Dosya transferi denetim kaydı: <b>kim, hangi PC, hangi yön, hangi dosya, ne zaman, sonuç</b>. Transfer başlayınca yazılır, bitince  kapanır. 
/// Dosyanın İÇERİĞİ saklanmaz; yalnızca ad/yol/boyut. <see cref="IRemoteAccessRecord"/> ile rapor servisindeki ortak filtre/süre hesabına girer.
/// </summary>
public class FileTransferLog : IEntity, IRemoteAccessRecord
{
    /// <summary> Transfer kimliği (merkez + HTTP uçları ile aynı Guid). </summary>
    public Guid Id { get; set; }

    /// <summary> Çekirdekteki <c>Device.Id</c> (FK DEĞİL). </summary>
    public Guid DeviceId { get; set; }

    /// <summary> Çekirdekteki <c>User.Id</c> (FK DEĞİL). </summary>
    public Guid UserId { get; set; }

    public FileTransferDirection Direction { get; set; }

    public string FileName { get; set; } = null!;

    /// <summary> PC tarafındaki yol (kaynak ya da hedef). </summary>
    public string Path { get; set; } = null!;

    public long SizeBytes { get; set; }

    public DateTime StartedUtc { get; set; }

    /// <summary> <c>null</c> = sürüyor (ya da sunucu kapanırken açık kaldı). </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary> <c>null</c> = sürüyor; aksi hâlde başarılı mı. </summary>
    public bool? Success { get; set; }

    public string? Error { get; set; }
}
