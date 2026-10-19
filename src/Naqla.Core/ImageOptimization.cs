using System.Globalization;

namespace Naqla.Core;

public enum ImageSizeUnit { KB, MB }
public sealed record ImageResizeBounds(int Width, int Height)
{
    public void Validate()
    {
        if (Width is < 1 or > 30000 || Height is < 1 or > 30000)
            throw new ArgumentException("حدود الأبعاد بين 1 و30000 بكسل");
    }
}
public sealed record TargetSizeSettings(long TargetBytes, string Format, ImageResizeBounds? Resize = null)
{
    public static IReadOnlyList<long> Presets { get; } = Array.AsReadOnly(new long[] { 100 * 1024, 200 * 1024, 500 * 1024, 1024 * 1024 });
    public void Validate()
    {
        if (TargetBytes is < 1 or > 1024L * 1024 * 1024) throw new ArgumentException("الحجم المستهدف بين 1 بايت و1 GB");
        if (Format is not ("PNG" or "JPG" or "WEBP" or "AVIF" or "TIFF" or "HEIC")) throw new NotSupportedException("اختر صيغة صورة متاحة");
        Resize?.Validate();
    }
    public static long ParseBytes(string value, ImageSizeUnit unit)
    {
        var normalized = new string(value.Trim().Select(c => c is >= '\u0660' and <= '\u0669' ? (char)('0' + c - '\u0660') : c is >= '\u06f0' and <= '\u06f9' ? (char)('0' + c - '\u06f0') : c == '\u066b' ? '.' : c).ToArray());
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)) throw new ArgumentException("أدخل حجمًا موجبًا؛ استخدم النقطة للكسر العشري");
        var bytes = decimal.Floor(number * (unit == ImageSizeUnit.KB ? 1024m : 1048576m));
        if (bytes is < 1 or > 1073741824m) throw new ArgumentException("الحجم المستهدف بين 1 بايت و1 GB");
        return (long)bytes;
    }
    public static string Size(long bytes) => bytes < 1024 ? $"{bytes} B" : bytes < 1048576 ? $"{bytes / 1024d:0.##} KB" : $"{bytes / 1048576d:0.##} MB";
}
public sealed record ImageOptimizationReport(long OriginalBytes, long TargetBytes, long ResultBytes, string Format,
    int SourceWidth, int SourceHeight, int Width, int Height, int? Quality, int Attempts, bool TransparencyFlattened)
{
    public bool Achieved => ResultBytes > 0 && ResultBytes <= TargetBytes;
    public string Description => $"{TargetSizeSettings.Size(OriginalBytes)} → {TargetSizeSettings.Size(ResultBytes)} · الحد {TargetSizeSettings.Size(TargetBytes)} · {Width}×{Height}";
}
public sealed record ImageOptimizationPreview(ImageOptimizationReport Report, ImagePreview Image);
public sealed record ImageOptimizationSource(ImagePreview Image, long Bytes, IReadOnlyList<string> Formats, string DefaultFormat, int PreviewIndex = 0);
public sealed record ImageOptimizationProgress(int Attempts, int? Quality, long BestBytes, long TargetBytes);
public sealed class ImageTargetNotMetException(ImageOptimizationReport report) : IOException("لم يتحقق الحجم المستهدف. " + report.Description)
{
    public ImageOptimizationReport Report { get; } = report;
}
