using System.IO;
using ImageMagick;
using ImageMagick.Formats;
using Naqla.Core;

namespace Naqla.Conversion;

/// <summary>Actual descending integer-quality search; no assumption that encoded size is monotonic.
/// PNG/TIFF compare lossless recipes. All candidates are private owned output files.</summary>
internal sealed class TargetSizeOptimizer(HeicEncoder heic)
{
    private static readonly string[] Formats = ["PNG", "JPG", "WEBP", "AVIF", "TIFF", "HEIC"];
    public IReadOnlyList<string> AvailableFormats() => Array.AsReadOnly(Formats.Where(format => format == "HEIC" ? heic.Available :
        MagickNET.SupportedFormats.Any(info => info.Format == LocalConversionService.ImageFormat(format) && info.SupportsWriting)).ToArray());

    public ImageOptimizationSource Source(DraggedFile file, CancellationToken token)
    {
        using var source = OpenSource(file);
        var image = RasterImages.Preview(file, token); var formats = AvailableFormats();
        var current = file.Extension == "JPEG" ? "JPG" : file.Extension;
        var preferred = formats.Contains(current) ? current : formats.Contains("PNG") ? "PNG" : formats.First();
        return new(image, source.Length, formats, preferred);
    }
    public async Task<(ConversionResult? Output, ImageOptimizationPreview? Preview)> RunAsync(DraggedFile file, TargetSizeSettings settings, bool preview,
        IProgress<ImageOptimizationProgress>? progress, CancellationToken token)
    {
        settings.Validate(); token.ThrowIfCancellationRequested();
        if (!AvailableFormats().Contains(settings.Format)) throw new NotSupportedException("المرمّز المحلي للصيغة المحددة غير متاح");
        using var source = OpenSource(file);
        using var frames = RasterImages.Open(file); var image = frames[0];
        var sourceWidth = (int)image.Width; var sourceHeight = (int)image.Height;
        if (image.GetColorProfile() is not null) image.TransformColorSpace(ColorProfiles.SRGB);
        else image.ColorSpace = ColorSpace.sRGB;
        image.Strip(); image.Orientation = Orientation.TopLeft; image.Density = new Density(96);
        if (settings.Resize is { } bounds) image.Resize(new MagickGeometry((uint)bounds.Width, (uint)bounds.Height) { Greater = true });
        var flattened = !image.IsOpaque && settings.Format == "JPG";
        if (settings.Format == "JPG") RasterImages.FlattenWhite(image);
        var transparent = !image.IsOpaque;
        var lossless = settings.Format is "PNG" or "TIFF";
        var maximumAttempts = settings.Format == "PNG" ? 3 : settings.Format == "TIFF" ? 2 : 100;
        OutputFile? best = null; long bestBytes = long.MaxValue; int? bestQuality = null; var attempts = 0;
        try
        {
            for (var index = 0; index < maximumAttempts; index++)
            {
                token.ThrowIfCancellationRequested();
                var quality = lossless ? (int?)null : 100 - index;
                OutputFile? candidate = new(file.FullPath, settings.Format, " optimized");
                try
                {
                    using var trial = image.Clone();
                    await EncodeAsync(trial, candidate.Temporary, settings.Format, quality, index, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    var bytes = new FileInfo(candidate.Temporary).Length;
                    if (bytes <= 0) throw new InvalidDataException("المرمّز أعاد ملفًا فارغًا");
                    attempts++;
                    if (bytes < bestBytes)
                    {
                        best?.Dispose(); best = candidate; candidate = null; bestBytes = bytes; bestQuality = quality;
                    }
                    progress?.Report(new(attempts, quality, bestBytes, settings.TargetBytes));
                    if (!lossless && bytes <= settings.TargetBytes) break; // Every higher integer quality was measured first.
                }
                finally { candidate?.Dispose(); }
            }
            token.ThrowIfCancellationRequested();
            var report = new ImageOptimizationReport(source.Length, settings.TargetBytes, bestBytes, settings.Format, sourceWidth, sourceHeight,
                (int)image.Width, (int)image.Height, bestQuality, attempts, flattened);
            Verify(best!.Temporary, report, transparent, lossless ? image : null);
            token.ThrowIfCancellationRequested();
            if (preview)
            {
                using var decoded = new MagickImage(best.Temporary);
                decoded.Resize(new MagickGeometry(800, 500) { Greater = true });
                var thumbnail = new ImagePreview(decoded.ToByteArray(MagickFormat.Png), report.Width, report.Height);
                token.ThrowIfCancellationRequested();
                return (null, new(report, thumbnail));
            }
            if (!report.Achieved) throw new ImageTargetNotMetException(report);
            // Validation and byte count are repeated immediately before publication. No approximate success.
            if (new FileInfo(best.Temporary).Length != report.ResultBytes) throw new InvalidDataException("تغير ملف الناتج أثناء التحقق");
            var published = best.Publish(token);
            return (new(published, report.Width, report.Height, Optimization: report), null);
        }
        finally { best?.Dispose(); }
    }
    private async Task EncodeAsync(IMagickImage<byte> image, string path, string format, int? quality, int recipe, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (quality.HasValue) image.Quality = (uint)quality.Value;
        if (format == "HEIC") { await heic.EncodeAsync(image, path, (uint)quality!.Value, token).ConfigureAwait(false); return; }
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        switch (format)
        {
            case "PNG": image.Write(stream, new PngWriteDefines { CompressionLevel = 9, CompressionStrategy = recipe switch { 0 => PngCompressionStrategy.Filtered, 1 => PngCompressionStrategy.Default, _ => PngCompressionStrategy.RLE } }); break;
            case "TIFF": image.Settings.Compression = recipe == 0 ? CompressionMethod.Zip : CompressionMethod.LZW; image.Write(stream, MagickFormat.Tiff); break;
            case "WEBP": image.Write(stream, new WebPWriteDefines { Lossless = false, Method = 4 }); break;
            default: image.Write(stream, LocalConversionService.ImageFormat(format)); break;
        }
    }
    private static FileStream OpenSource(DraggedFile file)
    {
        if (new DriveInfo(Path.GetPathRoot(file.FullPath)!).DriveType == DriveType.Network ||
            ((uint)File.GetAttributes(file.FullPath) & (0x1000u | 0x40000u | 0x400000u)) != 0)
            throw new NotSupportedException("اختر صورة محلية متاحة بالكامل؛ لا تُحمّل العناصر السحابية تلقائيًا");
        return new(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
    internal static void Verify(string path, ImageOptimizationReport report, bool transparent, IMagickImage<byte>? losslessReference = null)
    {
        if (new FileInfo(path).Length != report.ResultBytes) throw new InvalidDataException("فشل التحقق من حجم الناتج");
        using var decoded = new MagickImage(path);
        if (decoded.Width != report.Width || decoded.Height != report.Height || decoded.Format != LocalConversionService.ImageFormat(report.Format) ||
            decoded.Orientation is not (Orientation.TopLeft or Orientation.Undefined) || transparent && decoded.IsOpaque)
            throw new InvalidDataException("فشل التحقق من صيغة الصورة أو أبعادها أو اتجاهها أو شفافيتها");
        if (losslessReference is not null && decoded.Compare(losslessReference, ErrorMetric.Absolute) != 0)
            throw new InvalidDataException("فشل التحقق من بكسلات الضغط دون فقد");
    }
}
