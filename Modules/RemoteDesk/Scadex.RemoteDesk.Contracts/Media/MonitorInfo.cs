namespace Scadex.RemoteDesk.Contracts.Media;

/// <summary>
/// PC'nin bir ekranı. Sunucunun ve arayüzün kullandığı alanlar platformdan bağımsızdır: <see cref="Index"/>, konum/boyut,
/// <see cref="IsPrimary"/>. Koordinatlar fiziksel pikseldir (sanal masaüstü; birincil monitörün solundakiler negatif olabilir).
/// <para/>
/// <see cref="AdapterIndex"/>, <see cref="OutputIndex"/>, <see cref="DeviceName"/>, <see cref="GpuVendor"/>, <see cref="GpuName"/> Windows
/// istemcisinin yakalama ayrıntısıdır (bir monitör = bir DXGI çıkışı; <c>ddagrab</c> yalnızca verilen adaptörün çıkışlarını görür). Sunucu
/// bunları YORUMLAMAZ, yalnızca gösterir; başka platformun istemcisi 0 / boş / <see cref="Media.GpuVendor.Unknown"/> gönderir.
/// </summary>
public sealed class MonitorInfo
{
    /// <summary> Makinedeki sıra (0'dan); yayın yolu ve arayüz bu numarayı kullanır. </summary>
    public int Index { get; init; }

    /// <summary> Windows: DXGI adaptör sırası — <c>-init_hw_device d3d11va=cap:&lt;AdapterIndex&gt;</c>. Başka platform: 0. </summary>
    public int AdapterIndex { get; init; }

    /// <summary> Windows: adaptör içindeki çıkış sırası — <c>ddagrab=output_idx=&lt;OutputIndex&gt;</c>. Başka platform: 0. </summary>
    public int OutputIndex { get; init; }

    /// <summary> Platformun ekran adı, yalnızca gösterim için (Windows: <c>\\.\DISPLAY2</c>). </summary>
    public string DeviceName { get; init; } = "";

    public int Left { get; init; }
    public int Top { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool IsPrimary { get; init; }

    /// <summary> Monitörün bağlı olduğu ekran kartının üreticisi — kodlayıcı sırasında önce gelir. </summary>
    public GpuVendor GpuVendor { get; init; }

    /// <summary> Ekran kartının adı, yalnızca gösterim için (Windows: DXGI açıklaması). </summary>
    public string GpuName { get; init; } = "";
}
