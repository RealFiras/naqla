using System.IO;
using ImageMagick;
using ImageMagick.Formats;
using Naqla.Core;

namespace Naqla.Conversion;

internal sealed class ImageConversions(HeicEncoder heic)
{
    public async Task<ConversionResult> ConvertAsync(ConversionRequest request, CancellationToken token)
    {
        if (request.Files.Count > 1) return Merge(request.Files, token);
        var file = request.Files[0]; var target = request.Target;
        var options = request.ImageOptions ?? new(); options.Validate();
        using var frames = RasterImages.Open(file); var image = frames[0];
        token.ThrowIfCancellationRequested();
        var operation = target is "CROP" or "ROTATE" or "STRIP" or "COMPRESS";
        var format = operation ? (file.Extension == "JPEG" ? "JPG" : file.Extension) : target;
        if (target == "CROP")
        {
            var crop = request.Crop ?? throw new ArgumentException("Choose a crop rectangle before saving.");
            crop.Validate((int)image.Width, (int)image.Height);
            image.Crop(new MagickGeometry(crop.X, crop.Y, (uint)crop.Width, (uint)crop.Height)); image.ResetPage();
        }
        if (target == "ROTATE") image.Rotate(options.Rotation);
        // Removing ICC without converting its colors changes CMYK/profiled appearance.
        if (target == "STRIP" && image.GetColorProfile() is not null) image.TransformColorSpace(ColorProfiles.SRGB);
        if (target == "STRIP" && image.ColorSpace != ColorSpace.sRGB) image.ColorSpace = ColorSpace.sRGB;
        if (target == "STRIP") image.Strip();
        image.Quality = target == "COMPRESS" ? (uint)options.Quality : 92u;
        if (target == "COMPRESS" && format == "BMP") format = "PNG";
        if (target == "COMPRESS" && format == "TIFF") image.Settings.Compression = CompressionMethod.Zip;
        if (format is "JPG" or "PDF") RasterImages.FlattenWhite(image);
        var suffix = target switch { "CROP" => " cropped", "ROTATE" => " rotated", "STRIP" => " clean", "COMPRESS" => " compressed", _ => "" };
        using var output = new OutputFile(file.FullPath, format, suffix);
        if (format == "PDF")
        {
            ImagePdf.Write(output.Temporary, image.ToByteArray(MagickFormat.Jpeg), (int)image.Width, (int)image.Height);
            PdfConversions.Verify(output.Temporary, token: token);
        }
        else if (format == "HEIC")
        {
            await heic.EncodeAsync(image, output.Temporary, image.Quality, token).ConfigureAwait(false);
        }
        else
        {
            using (var stream = new FileStream(output.Temporary, FileMode.CreateNew, FileAccess.Write))
            {
                if (target == "COMPRESS" && format == "PNG") image.Write(stream, new PngWriteDefines { CompressionLevel = 9, CompressionStrategy = PngCompressionStrategy.Filtered });
                else image.Write(stream, LocalConversionService.ImageFormat(format));
            }
            RasterImages.Verify(output.Temporary, format, image.Width, image.Height);
        }
        token.ThrowIfCancellationRequested();
        if (target == "COMPRESS" && new FileInfo(output.Temporary).Length >= file.Size)
            throw new InvalidDataException("This image is already compact; recompression did not reduce its size. The original was preserved.");
        var bytes = new FileInfo(output.Temporary).Length;
        return new(output.Publish(token), (int)image.Width, (int)image.Height, Report: operation ? new(file.Size, bytes, $"{file.Size:N0} B → {bytes:N0} B", format is "JPG" or "WEBP" or "AVIF" or "HEIC") : null);
    }
    private static ConversionResult Merge(IReadOnlyList<DraggedFile> files, CancellationToken token)
    {
        using var output = new OutputFile(files[0].FullPath, "PDF", " merged");
        ImagePdf.WritePages(output.Temporary, files.Count, index =>
        {
            token.ThrowIfCancellationRequested();
            using var frames = RasterImages.Open(files[index]); var image = frames[0];
            RasterImages.FlattenWhite(image); image.Quality = 92;
            token.ThrowIfCancellationRequested();
            return new(image.ToByteArray(MagickFormat.Jpeg), (int)image.Width, (int)image.Height);
        }, token);
        PdfConversions.Verify(output.Temporary, files.Count, token);
        return new(output.Publish(token), 0, 0, files.Count);
    }
}
