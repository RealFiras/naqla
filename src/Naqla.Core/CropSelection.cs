namespace Naqla.Core;

/// <summary>Converts a marquee on an oriented image preview to bounded original-pixel coordinates.</summary>
public static class CropSelection
{
    public static CropRectangle FromNormalized(double x1, double y1, double x2, double y2, int width, int height)
    {
        if (width < 1 || height < 1 || !double.IsFinite(x1) || !double.IsFinite(y1) || !double.IsFinite(x2) || !double.IsFinite(y2)) throw new ArgumentOutOfRangeException(nameof(width));
        x1 = Math.Clamp(x1, 0, 1); y1 = Math.Clamp(y1, 0, 1); x2 = Math.Clamp(x2, 0, 1); y2 = Math.Clamp(y2, 0, 1);
        var left = Math.Clamp((int)Math.Floor(Math.Min(x1, x2) * width), 0, width - 1);
        var top = Math.Clamp((int)Math.Floor(Math.Min(y1, y2) * height), 0, height - 1);
        var right = Math.Clamp((int)Math.Ceiling(Math.Max(x1, x2) * width), left + 1, width);
        var bottom = Math.Clamp((int)Math.Ceiling(Math.Max(y1, y2) * height), top + 1, height);
        return new(left, top, right - left, bottom - top);
    }
}
