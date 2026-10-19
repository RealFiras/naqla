using System.IO;
using ImageMagick;
using Naqla.Core;

namespace Naqla.Conversion;

internal static class RasterImages
{
    public const long MaximumPixels = 40_000_000;
    private static readonly Lazy<bool> Limits = new(() =>
    {
        ResourceLimits.Thread = 2; ResourceLimits.Memory = 128 * 1024 * 1024;
        ResourceLimits.Disk = 512 * 1024 * 1024; ResourceLimits.Width = 30000; ResourceLimits.Height = 30000;
        ResourceLimits.ListLength = 201; return true;
    });
    public static MagickImageCollection Open(DraggedFile file)
    {
        _ = Limits.Value;
        using var input = File.OpenRead(file.FullPath);
        var frames = new MagickImageCollection();
        try
        {
            frames.Read(input, new MagickReadSettings { Format = LocalConversionService.ImageFormat(file.Extension) });
            if (frames.Count != 1) throw new NotSupportedException("Multi-frame images need a multi-frame workflow; the original was preserved.");
            var image = frames[0];
            if ((long)image.Width * image.Height > MaximumPixels) throw new NotSupportedException("Images are limited to 40 megapixels.");
            image.AutoOrient();
            return frames;
        }
        catch { frames.Dispose(); throw; }
    }
    public static void FlattenWhite(IMagickImage<byte> image)
    {
        image.ColorSpace = ColorSpace.sRGB; image.BackgroundColor = MagickColors.White;
        image.Alpha(AlphaAction.Remove); image.ColorType = ColorType.TrueColor;
    }
    public static ImagePreview Preview(DraggedFile file, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var frames = Open(file); var image = frames[0];
        var width = (int)image.Width; var height = (int)image.Height;
        var size = new MagickGeometry(1200, 900) { Greater = true };
        image.Resize(size); image.Strip(); image.Density = new Density(96);
        token.ThrowIfCancellationRequested();
        return new(image.ToByteArray(MagickFormat.Png), width, height);
    }
    public static void Verify(string path, string format, uint width, uint height)
    {
        using var decoded = new MagickImage(path);
        if (decoded.Width != width || decoded.Height != height || decoded.Format != LocalConversionService.ImageFormat(format))
            throw new InvalidDataException("Image decode verification failed.");
    }
}
