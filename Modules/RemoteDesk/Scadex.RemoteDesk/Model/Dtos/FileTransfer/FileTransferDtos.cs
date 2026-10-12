using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Model.Dtos.FileTransfer;

/// <summary> Sunucu → tarayıcı: bir transferin durumu değişti (hazır / tamamlandı / başarısız). </summary>
public class FileTransferStateNotice
{
    public Guid TransferId { get; set; }
    public FileTransferStatus Status { get; set; }
    public string FileName { get; set; } = "";
    public long Bytes { get; set; }
    public string? Error { get; set; }
}

/// <summary> Tarayıcı dizin gezme isteğinin yanıtı (viewer hub). </summary>
public class ListDirectoryResponse
{
    public bool Allowed { get; set; }
    public string? Message { get; set; }
    public string Path { get; set; } = "";
    public bool IsRoot { get; set; }
    public string? ParentPath { get; set; }
    public List<DirEntryDto> Entries { get; set; } = [];
    public string? Error { get; set; }
}

public class DirEntryDto
{
    public string Name { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    /// <summary> UTC; <c>Z</c> soneki olmayabilir — tarayıcıda <c>toUtcDate</c>/<c>formatUtcDateTime</c>. </summary>
    public DateTime ModifiedUtc { get; set; }
}

/// <summary> Tarayıcı indirme/yükleme isteğinin yanıtı: kabul edildiyse <c>TransferId</c>. </summary>
public class FileTransferResponse
{
    public bool Accepted { get; set; }
    public Guid? TransferId { get; set; }
    public string? Message { get; set; }
}
