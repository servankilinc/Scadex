using Scadex.Core.Model;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Model.Dtos.Report.Queries;

/// <summary> Dosya transferi raporunun satırı (<c>FileTransferLog</c>): kim, hangi PC, yön, dosya, boyut, ne zaman, sonuç. </summary>
public class FileTransferReportItemDto : IDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? CabinetName { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserName { get; set; }
    public FileTransferDirection Direction { get; set; }
    public string FileName { get; set; } = null!;
    public string Path { get; set; } = null!;
    public long SizeBytes { get; set; }
    public DateTime StartedUtc { get; set; }

    /// <summary> <c>null</c> = sürüyor. </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary> <c>null</c> = sürüyor. </summary>
    public bool? Success { get; set; }

    public string? Error { get; set; }
}
