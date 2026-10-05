namespace Scadex.RemoteDesk.Model.Entities;

/// <summary>
/// İki denetim tablosunun (<see cref="ScreenViewLog"/>, <see cref="RemoteControlSession"/>) ortak interface'i: kim, hangi PC, ne zaman.
/// Rapor servisi filtreyi ve süre hesabını bu arayüz üzerinden TEK yerde kurar; EF sorguyu her tabloya ayrı çevirir.
/// </summary>
public interface IRemoteAccessRecord
{
    Guid DeviceId { get; }
    Guid UserId { get; }
    DateTime StartedUtc { get; }
    DateTime? EndedUtc { get; }
}
