using Scadex.RemoteDesk.Contracts.Media;
using Vortice.DXGI;

namespace Scadex.RemoteDesk.Windows.Services.Monitors;

public interface IMonitorService
{
    /// <summary> Masaüstüne bağlı monitörler, DXGI sırasıyla. Her çağrıda yeniden okunur (ucuz; yalnızca açılışta ve ekran değişince çağrılır). </summary>
    IReadOnlyList<MonitorInfo> GetMonitors();
}

/// <summary>
/// Monitör listesi <c>Screen.AllScreens</c>'ten değil DXGI'dan okunur: <c>ddagrab</c>'ın <c>output_idx</c>'i DXGI
/// sırasıdır ve monitörün hangi ekran kartına bağlı olduğu (kodlayıcı sırası) yalnızca burada görünür.
/// </summary>
public sealed class MonitorService : IMonitorService
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        using IDXGIFactory1 factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

        for (uint a = 0; factory.EnumAdapters1(a, out IDXGIAdapter1? adapter).Success; a++)
        {
            using (adapter)
            {
                AdapterDescription1 desc = adapter!.Description1;
                // "Microsoft Basic Render Driver" gibi yazılım adaptörlerinde kodlayıcı yoktur.
                if ((desc.Flags & AdapterFlags.Software) != 0) continue;

                for (uint o = 0; adapter.EnumOutputs(o, out IDXGIOutput? output).Success; o++)
                {
                    using (output)
                    {
                        OutputDescription od = output!.Description;
                        if (!od.AttachedToDesktop) continue;

                        var r = od.DesktopCoordinates;
                        monitors.Add(new MonitorInfo
                        {
                            Index = monitors.Count,
                            AdapterIndex = (int)a,
                            OutputIndex = (int)o,
                            DeviceName = od.DeviceName,
                            Left = r.Left,
                            Top = r.Top,
                            Width = r.Right - r.Left,
                            Height = r.Bottom - r.Top,
                            IsPrimary = r.Left == 0 && r.Top == 0,
                            GpuVendor = ToVendor(desc.VendorId),
                            GpuName = desc.Description,
                        });
                    }
                }
            }
        }
        return monitors;
    }

    /// <summary> PCI satıcı kimlikleri. </summary>
    private static GpuVendor ToVendor(uint vendorId) => vendorId switch
    {
        0x10DE => GpuVendor.Nvidia,
        0x8086 => GpuVendor.Intel,
        0x1002 or 0x1022 => GpuVendor.Amd,
        _ => GpuVendor.Unknown,
    };
}
