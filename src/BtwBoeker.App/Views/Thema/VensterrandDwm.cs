using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BtwBoeker.App.Views.Thema;

/// <summary>
/// Vraagt Windows 11 (DWM) om afgeronde hoeken. Schaduw en rand zelf komen van de <c>GlassFrameThickness</c> van de
/// WindowChrome. Een mislukte aanroep (Windows 10, Extern bureaublad) wordt genegeerd: het venster werkt dan gewoon met
/// de rand van Windows.
/// </summary>
public static class VensterrandDwm
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    public static void ZetHoeken(Window venster)
    {
        ArgumentNullException.ThrowIfNull(venster);
        if (!Vensterrand.IsWindows11(Environment.OSVersion.Version))
        {
            return;
        }

        var handle = new WindowInteropHelper(venster).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var voorkeur = DwmwcpRound;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref voorkeur, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int eigenschap, ref int waarde, int grootte);
}
