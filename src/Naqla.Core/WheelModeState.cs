namespace Naqla.Core;

/// <summary>One Ctrl press toggles mode. Release/repeat do not undo a completed press.</summary>
public sealed class WheelModeState
{
    private bool controlDown;
    public WheelMode Mode { get; private set; }

    public void Reset(bool controlAlreadyDown = false)
    {
        Mode = WheelMode.Convert;
        controlDown = controlAlreadyDown;
    }

    public bool ControlChanged(bool down)
    {
        if (controlDown == down) return false;
        controlDown = down;
        if (!down) return false;
        Mode = Mode == WheelMode.Convert ? WheelMode.Tools : WheelMode.Convert;
        return true;
    }
}
