using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Naqla.Core;

namespace Naqla.Conversion;

/// <summary>Offline WPF/WIC codecs. Reads the real source, composites alpha onto white,
/// encodes and decodes a private temporary file, then atomically publishes beside source.</summary>
public sealed class PngToJpegConverter : IFileConverter
{
    public Task<ConversionResult> ConvertAsync(DraggedFile file, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<ConversionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(Convert(file, cancellationToken)); }
            catch (OperationCanceledException) { completion.SetCanceled(cancellationToken); }
            catch (Exception error) { completion.SetException(error); }
        }) { IsBackground = true, Name = "Naqla image conversion" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static ConversionResult Convert(DraggedFile file, CancellationToken token)
    {
        if (file.Extension != "PNG") throw new NotSupportedException("This development build converts PNG to JPG only.");
        token.ThrowIfCancellationRequested();
        BitmapSource source;
        using (var input = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            // Check the signature as well as the extension.
            Span<byte> signature = stackalloc byte[8];
            input.ReadExactly(signature);
            if (!signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new InvalidDataException("The source is not a PNG image.");
            input.Position = 0;
            var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            source = decoder.Frames[0];
            if ((long)source.PixelWidth * source.PixelHeight > 40_000_000)
                throw new NotSupportedException("The prototype limits images to 40 megapixels.");
        }
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, source.PixelWidth, source.PixelHeight);
            dc.DrawRectangle(Brushes.White, null, bounds);
            dc.DrawImage(source, bounds);
        }
        var flattened = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        flattened.Render(visual);
        var directory = Path.GetDirectoryName(file.FullPath)!;
        var stem = Path.GetFileNameWithoutExtension(file.FullPath);
        var temporary = Path.Combine(directory, $".naqla-{Guid.NewGuid():N}.tmp");
        try
        {
            token.ThrowIfCancellationRequested();
            var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
            encoder.Frames.Add(BitmapFrame.Create(flattened));
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) encoder.Save(output);
            using (var check = File.OpenRead(temporary))
            {
                var decoded = new JpegBitmapDecoder(check, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                if (decoded.PixelWidth != source.PixelWidth || decoded.PixelHeight != source.PixelHeight)
                    throw new InvalidDataException("JPG verification failed.");
                // Force a pixel decode; merely reading the JPEG header is insufficient.
                var stride = (decoded.PixelWidth * decoded.Format.BitsPerPixel + 7) / 8;
                decoded.CopyPixels(new byte[checked(stride * decoded.PixelHeight)], stride, 0);
            }
            for (var suffix = 0; suffix < 10000; suffix++)
            {
                token.ThrowIfCancellationRequested();
                var target = Path.Combine(directory, suffix == 0 ? $"{stem}.jpg" : $"{stem} ({suffix}).jpg");
                try
                {
                    File.Move(temporary, target, overwrite: false);
                    return new(target, source.PixelWidth, source.PixelHeight);
                }
                catch (IOException) when (File.Exists(target)) { }
            }
            throw new IOException("No available output name was found.");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
