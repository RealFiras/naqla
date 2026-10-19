namespace Naqla.Core;

/// <summary>Approved 2026-10-19: only Tools is paged; Ctrl's independent mode state is unchanged.</summary>
public sealed class WheelPaging
{
    public const int ToolsPageSize = 6;
    public int Page { get; private set; }
    public int Pages { get; private set; } = 1;
    public void Reset() { Page = 0; Pages = 1; }
    public void Update(int tools) { Pages = Math.Max(1, (tools + ToolsPageSize - 1) / ToolsPageSize); Page = Math.Clamp(Page, 0, Pages - 1); }
    public bool Turn(int delta)
    {
        if (Pages <= 1 || delta == 0) return false;
        Page = (Page + Math.Sign(delta) + Pages) % Pages; return true;
    }
    public IReadOnlyList<WheelOption> Visible(IReadOnlyList<WheelOption> options, WheelMode mode)
    {
        if (mode == WheelMode.Convert) return options;
        Update(options.Count); return options.Skip(Page * ToolsPageSize).Take(ToolsPageSize).ToArray();
    }
}
