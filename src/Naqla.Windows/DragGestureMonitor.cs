using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Naqla.Core;

namespace Naqla.Windows;

public sealed record GesturePoint(int X, int Y);

/// <summary>Recognizes only a candidate gesture. Never claims knowledge of an OLE
/// drag or its file. Hooks always pass input onward and never inspect other apps.</summary>
public sealed class DragGestureMonitor : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly IDiagnostics diagnostics;
    private readonly Native.HookProc mouseProc, keyboardProc;
    private readonly nint mouseHook, keyboardHook;
    private bool leftDown, moved, armed, control, shellSource, disposed;
    private ShellOrigin origin;
    private Native.Point start, latest;
    public event Action<GesturePoint>? Candidate;
    public event Action? Released;
    public event Action? Cancelled;
    public event Action<bool>? ControlChanged;
    public event Action<int>? PageRequested;
    private bool pageNavigationEnabled, tabConsumed;
    public bool PageNavigationEnabled { get => Volatile.Read(ref pageNavigationEnabled); set => Volatile.Write(ref pageNavigationEnabled, value); }

    public DragGestureMonitor(Dispatcher dispatcher, IDiagnostics? diagnostics = null)
    {
        this.dispatcher = dispatcher;
        this.diagnostics = diagnostics ?? NullDiagnostics.Instance;
        mouseProc = MouseHook;
        keyboardProc = KeyboardHook;
        mouseHook = Native.SetWindowsHookEx(14, mouseProc, Native.GetModuleHandle(null), 0);
        var mouseError = mouseHook == 0 ? Marshal.GetLastWin32Error() : 0;
        this.diagnostics.Write(mouseHook == 0 ? DiagnosticEvent.MouseHookInstallFailed : DiagnosticEvent.MouseHookInstalled,
            new() { Error = mouseHook == 0 ? DiagnosticError.Win32 : DiagnosticError.None, ErrorCode = mouseError });
        keyboardHook = Native.SetWindowsHookEx(13, keyboardProc, Native.GetModuleHandle(null), 0);
        var keyboardError = keyboardHook == 0 ? Marshal.GetLastWin32Error() : 0;
        this.diagnostics.Write(keyboardHook == 0 ? DiagnosticEvent.KeyboardHookInstallFailed : DiagnosticEvent.KeyboardHookInstalled,
            new() { Error = keyboardHook == 0 ? DiagnosticError.Win32 : DiagnosticError.None, ErrorCode = keyboardError });
        if (mouseHook == 0 || keyboardHook == 0)
        {
            Dispose();
            throw new Win32Exception(mouseError != 0 ? mouseError : keyboardError, "Naqla could not install the gesture hooks.");
        }
    }

    private nint MouseHook(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var info = Marshal.PtrToStructure<Native.MouseData>(data);
            latest = info.Point;
            switch ((int)message)
            {
                case 0x201:
                    leftDown = true; moved = armed = false; start = latest;
                    var name = new System.Text.StringBuilder(128);
                    Native.GetClassName(Native.GetAncestor(Native.WindowFromPoint(latest), 2), name, name.Capacity);
                    origin = name.ToString() switch
                    {
                        "CabinetWClass" or "ExploreWClass" => ShellOrigin.Explorer,
                        "Progman" or "WorkerW" => ShellOrigin.Desktop,
                        "" => ShellOrigin.Unknown,
                        _ => ShellOrigin.Other
                    };
                    shellSource = origin is ShellOrigin.Explorer or ShellOrigin.Desktop;
                    if (shellSource)
                    {
                        diagnostics.BeginAttempt();
                        diagnostics.Write(DiagnosticEvent.ShellLeftDown, new() { Origin = origin });
                    }
                    break;
                case 0x200 when leftDown:
                    var previouslyMoved = moved;
                    moved |= Math.Abs(latest.X - start.X) >= Native.GetSystemMetrics(68) || Math.Abs(latest.Y - start.Y) >= Native.GetSystemMetrics(69);
                    if (moved && !previouslyMoved && shellSource)
                        diagnostics.Write(DiagnosticEvent.DragThresholdPassed, new() { Origin = origin });
                    break;
                case 0x202:
                    if (leftDown && (shellSource || armed))
                        diagnostics.Write(DiagnosticEvent.LeftReleased, new() { Origin = origin, Armed = armed, ThresholdPassed = moved });
                    leftDown = moved = armed = false;
                    dispatcher.BeginInvoke(() => Released?.Invoke());
                    break;
            }
        }
        return Native.CallNextHookEx(mouseHook, code, message, data);
    }

    private nint KeyboardHook(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            var key = Marshal.PtrToStructure<Native.KeyData>(data).Key;
            var down = (int)message is 0x100 or 0x104;
            if (key == 0x09)
            {
                if (!down && tabConsumed) { tabConsumed = false; return 1; }
                if (down && tabConsumed) return 1; // no key-repeat page cycling
                if (down && armed && leftDown && PageNavigationEnabled && !control && (Native.GetAsyncKeyState(0x12) & 0x8000) == 0 && (Native.GetAsyncKeyState(0x5B) & 0x8000) == 0 && (Native.GetAsyncKeyState(0x5C) & 0x8000) == 0)
                {
                    tabConsumed = true; var direction = (Native.GetAsyncKeyState(0x10) & 0x8000) != 0 ? -1 : 1;
                    dispatcher.BeginInvoke(() => PageRequested?.Invoke(direction));
                    return 1; // Only this explicitly approved, OLE-confirmed wheel shortcut is consumed.
                }
            }
            if (key is 0xA0 or 0xA1 or 0x10)
            {
                diagnostics.Write(down ? DiagnosticEvent.ShiftDown : DiagnosticEvent.ShiftUp,
                    new() { LeftDown = leftDown, ThresholdPassed = moved, Origin = origin, Armed = armed });
                if (down && leftDown && moved && shellSource && !armed)
                {
                    armed = true;
                    diagnostics.Write(DiagnosticEvent.GestureCandidate);
                    var position = new GesturePoint(latest.X, latest.Y);
                    dispatcher.BeginInvoke(() => { diagnostics.Write(DiagnosticEvent.CandidateDispatched); Candidate?.Invoke(position); });
                }
                else if (down)
                {
                    var reason = !leftDown ? DiagnosticReason.LeftNotDown : !moved ? DiagnosticReason.BelowDragThreshold :
                        !shellSource ? DiagnosticReason.NonShellOrigin : DiagnosticReason.AlreadyArmed;
                    diagnostics.Write(DiagnosticEvent.GestureSuppressed, new() { Reason = reason });
                }
            }
            if (key is 0xA2 or 0xA3 or 0x11)
            {
                // Hook state arrives before GetAsyncKeyState reflects this key.
                var other = key == 0xA2 ? 0xA3 : 0xA2;
                var next = down || (Native.GetAsyncKeyState(other) & 0x8000) != 0;
                if (control != next)
                {
                    control = next;
                    if (armed) diagnostics.Write(next ? DiagnosticEvent.ControlDown : DiagnosticEvent.ControlUp);
                    dispatcher.BeginInvoke(() => ControlChanged?.Invoke(next));
                }
            }
            if (down && key == 0x1B)
            {
                if (armed) diagnostics.Write(DiagnosticEvent.EscapeDuringGesture);
                dispatcher.BeginInvoke(() => Cancelled?.Invoke());
            }
        }
        return Native.CallNextHookEx(keyboardHook, code, message, data);
    }

    public static bool IsLeftButtonDown => (Native.GetAsyncKeyState(1) & 0x8000) != 0;
    public static bool IsControlButtonDown => (Native.GetAsyncKeyState(0x11) & 0x8000) != 0;
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        var mouseReleased = mouseHook == 0 || Native.UnhookWindowsHookEx(mouseHook);
        var mouseError = mouseReleased ? 0 : Marshal.GetLastWin32Error();
        var keyboardReleased = keyboardHook == 0 || Native.UnhookWindowsHookEx(keyboardHook);
        var keyboardError = keyboardReleased ? 0 : Marshal.GetLastWin32Error();
        diagnostics.Write(DiagnosticEvent.HooksReleased, new() { Succeeded = mouseReleased && keyboardReleased,
            Error = mouseReleased && keyboardReleased ? DiagnosticError.None : DiagnosticError.Win32, ErrorCode = mouseError != 0 ? mouseError : keyboardError });
    }
}
