namespace Scadex.RemoteDesk.Contracts.Media;

/// <summary>
/// Bir monitör = bir DXGI çıkışı. Yakalama <c>(AdapterIndex, OutputIndex)</c> çiftiyle yapılır: <c>ddagrab</c>
/// yalnızca kendisine verilen adaptörün çıkışlarını görür, bu yüzden tek bir sıra numarası yetmez.
/// Koordinatlar fiziksel pikseldir (sanal masaüstü; birincil monitörün solundakiler negatif olabilir).
/// </summary>
public sealed class MonitorInfo
{
    /// <summary> Makinedeki sıra (0'dan); yayın yolu ve arayüz bu numarayı kullanır. </summary>
    public int Index { get; init; }

    /// <summary> DXGI adaptör sırası — <c>-init_hw_device d3d11va=cap:&lt;AdapterIndex&gt;</c>. </summary>
    public int AdapterIndex { get; init; }

    /// <summary> Adaptör içindeki çıkış sırası — <c>ddagrab=output_idx=&lt;OutputIndex&gt;</c>. </summary>
    public int OutputIndex { get; init; }

    /// <summary> Windows aygıt adı (örn. <c>\\.\DISPLAY2</c>). </summary>
    public string DeviceName { get; init; } = "";

    public int Left { get; init; }
    public int Top { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool IsPrimary { get; init; }

    /// <summary> Monitörün bağlı olduğu ekran kartının üreticisi — kodlayıcı sırasında önce gelir. </summary>
    public GpuVendor GpuVendor { get; init; }

    /// <summary> Ekran kartının adı (DXGI açıklaması). </summary>
    public string GpuName { get; init; } = "";
}
