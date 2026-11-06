using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Diagram.Queries.Items;

/// <summary> Basic Auth kullanıcı adı cihazın Id'sidir; sır sunucuda yalnızca hash olarak durur, geri okunamaz. </summary>
public class DiagramPcDeviceDto : IDto
{
    public bool HasCredential { get; set; }
}
