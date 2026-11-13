using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using PlayVoice.Resources.Themes;

namespace PlayVoice;

public partial class MainWindow
{
    private bool materialClosed;
    [StructLayout(LayoutKind.Sequential)]
    private struct GlassMargins { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref GlassMargins margins);

    private void InitializeWindowMaterial()
    {
        RefreshWindowMaterial();
        SourceInitialized += (_, _) => RefreshWindowMaterial();
        ThemeManager.ThemeChanged += MaterialThemeChanged;
        SystemEvents.UserPreferenceChanged += MaterialPreferenceChanged;
        Closed += (_, _) =>
        {
            materialClosed = true;
            ThemeManager.ThemeChanged -= MaterialThemeChanged;
            SystemEvents.UserPreferenceChanged -= MaterialPreferenceChanged;
        };
    }
    private void MaterialThemeChanged(ThemeManager.ThemeInfo theme) => RefreshWindowMaterial();
    private void MaterialPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (!Dispatcher.HasShutdownStarted)
        {
            try { Dispatcher.BeginInvoke(RefreshWindowMaterial); }
            catch (InvalidOperationException) when (Dispatcher.HasShutdownStarted) { }
        }
    }

    private void RefreshWindowMaterial()
    {
        if (materialClosed) return;
        var profile = GlobalData.Inst.Config.Appearance ?? new();
        var hwnd = new WindowInteropHelper(this).Handle;
        bool enabled = false;
        var baseColor = ((SolidColorBrush)FindResource("BgMedium")).Color;
        try
        {
            bool available = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)
                && !SystemParameters.HighContrast
                && Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1)) != 0;
            if (hwnd != IntPtr.Zero && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                int rounded = 2;
                DwmSetWindowAttribute(hwnd, 33, ref rounded, sizeof(int));
                int dark = baseColor.R * .299 + baseColor.G * .587 + baseColor.B * .114 < 128 ? 1 : 0;
                DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            }
            if (hwnd != IntPtr.Zero && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
            {
                int type = available ? profile.WindowMaterial switch { "Acrylic" => 3, "Mica" => 2, _ => 1 } : 1;
                var margins = new GlassMargins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                enabled = DwmExtendFrameIntoClientArea(hwnd, ref margins) >= 0
                    && DwmSetWindowAttribute(hwnd, 38, ref type, sizeof(int)) >= 0 && type != 1;
            }
        }
        catch (Exception exception) when (exception is ExternalException or System.Security.SecurityException
            or System.IO.IOException or UnauthorizedAccessException or InvalidCastException or FormatException or OverflowException)
        {
            Diagnostics.AppLog.Warning(exception, "系统材质不可用，使用实色界面。", "Window.Material");
        }
        Resources["WindowSurface"] = enabled ? Brushes.Transparent : FindResource("BgDark");
        var materialColor = baseColor;
        materialColor.A = enabled ? (byte)Math.Round(Math.Clamp(profile.MaterialOpacity, .6, 1) * 255) : (byte)255;
        Resources["MaterialSurface"] = new SolidColorBrush(materialColor);
        if (hwnd != IntPtr.Zero && HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target)
            target.BackgroundColor = enabled ? Colors.Transparent : ((SolidColorBrush)FindResource("BgDark")).Color;
    }
}
