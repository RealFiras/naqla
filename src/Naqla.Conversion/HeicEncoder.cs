using System.IO;
using ImageMagick;

namespace Naqla.Conversion;

internal sealed class HeicEncoder(string directory)
{
    public bool Available => File.Exists(Path.Combine(directory, "heif-enc.exe")) && File.Exists(Path.Combine(directory, "libheif.dll"));
    public async Task EncodeAsync(IMagickImage<byte> image, string outputPath, uint quality, CancellationToken token)
    {
        if (!Available) throw new NotSupportedException("The bundled local HEIC encoder is missing.");
        // Relative ASCII argv avoids native narrow-path limitations for Arabic source folders/names.
        var work = Path.Combine(Path.GetDirectoryName(outputPath)!, $".naqla-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);
        try
        {
            token.ThrowIfCancellationRequested();
            using (var input = new FileStream(Path.Combine(work, "input.png"), FileMode.CreateNew, FileAccess.Write)) image.Write(input, MagickFormat.Png);
            await LocalProcess.RunAsync(Path.Combine(directory, "heif-enc.exe"),
                ["--hevc", "-e", "x265", "-q", quality.ToString(System.Globalization.CultureInfo.InvariantCulture), "-p", "x265:pools=2", "-p", "x265:frame-threads=2", "-p", "x265:ctu=16", "-p", "x265:log-level=error", "-o", "output.heic", "input.png"],
                token, workingDirectory: work).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            RasterImages.Verify(Path.Combine(work, "output.heic"), "HEIC", image.Width, image.Height);
            File.Move(Path.Combine(work, "output.heic"), outputPath, false);
        }
        finally
        {
            // Only this exact random directory was created and populated by this call.
            if (Directory.Exists(work)) Directory.Delete(work, true);
        }
    }
}
