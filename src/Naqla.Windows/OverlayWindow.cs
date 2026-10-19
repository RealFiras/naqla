using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using Naqla.Core;

namespace Naqla.Windows;

public sealed class OverlayWindow : Window
{
    private readonly IDiagnostics diagnostics;
    private readonly double canvasWidth, canvasHeight, anchorY;
    public OverlayWindow(UIElement content, IDiagnostics? diagnostics = null, double width = 328, double height = 390, double anchorY = 164)
    {
        this.diagnostics = diagnostics ?? NullDiagnostics.Instance;
        this.canvasWidth = width; this.canvasHeight = height; this.anchorY = anchorY;
        Title = "Naqla";
        Width = width; Height = height;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Content = content;
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = Native.GetWindowLongPtr(hwnd, -20).ToInt64();
            Native.SetWindowLongPtr(hwnd, -20, (nint)(style | 0x08000000 | 0x80)); // NOACTIVATE | TOOLWINDOW
            this.diagnostics.Write(DiagnosticEvent.OverlayHandleCreated);
        };
    }
    public void ShowNear(GesturePoint point)
    {
        // Physical-pixel placement, clamped to the pointer monitor's work area.
        // No cursor movement, focus stealing, or registration on Explorer's HWND.
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        var monitor = Native.MonitorFromPoint(new Native.Point { X = point.X, Y = point.Y }, 2);
        var info = new Native.MonitorInfo { Size = (uint)Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(monitor, ref info))
        {
            diagnostics.Write(DiagnosticEvent.OverlayFailed, new() { Reason = DiagnosticReason.NativeCallFailed, Error = DiagnosticError.Win32, ErrorCode = Marshal.GetLastWin32Error() });
            return;
        }
        // Move the still-hidden HWND onto the pointer monitor before querying its DPI.
        // A one-pixel probe rectangle avoids selecting a neighbouring monitor at an edge.
        if (!Native.SetWindowPos(hwnd, 0, point.X, point.Y, 1, 1, 0x14))
            diagnostics.Write(DiagnosticEvent.OverlayFailed, new() { Reason = DiagnosticReason.NativeCallFailed, Error = DiagnosticError.Win32, ErrorCode = Marshal.GetLastWin32Error() });
        var dpi = Native.GetDpiForWindow(hwnd);
        if (dpi == 0) dpi = 96;
        var scale = dpi / 96d;
        Width = canvasWidth; Height = canvasHeight;
        var width = (int)Math.Round(canvasWidth * scale); var height = (int)Math.Round(canvasHeight * scale);
        var x = Math.Clamp(point.X - width / 2, info.Work.Left, Math.Max(info.Work.Left, info.Work.Right - width));
        var y = Math.Clamp(point.Y - (int)(anchorY * scale), info.Work.Top, Math.Max(info.Work.Top, info.Work.Bottom - height));
        var placed = Native.SetWindowPos(hwnd, -1, x, y, width, height, 0x10); // NOACTIVATE
        var error = placed ? 0 : Marshal.GetLastWin32Error();
        diagnostics.Write(placed ? DiagnosticEvent.OverlayPlacement : DiagnosticEvent.OverlayFailed,
            new() { Dpi = (int)dpi, Width = width, Height = height, ErrorCode = error });
        Show();
        diagnostics.Write(IsVisible ? DiagnosticEvent.OverlayShown : DiagnosticEvent.OverlayFailed, new() { Visible = IsVisible });
    }
}
