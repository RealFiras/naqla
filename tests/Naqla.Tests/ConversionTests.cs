using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Naqla.Conversion;
using Naqla.Core;
using Naqla.Wheel;
using UglyToad.PdfPig;

internal static partial class Program
{
    private static readonly LocalConversionService Service = new();
    private static string? mediaFixture, pdfFixture;
    private static ConversionResult ConvertFile(string path, string target) => Service.ConvertAsync(FileClassifier.Inspect(path), target).GetAwaiter().GetResult();
    private static void ImageFormats()
    {
        var source = CreatePng("ألوان حقيقية.png");
        var hash = SHA256.HashData(File.ReadAllBytes(source));
        foreach (var target in new[] { "JPG", "WEBP", "AVIF", "TIFF", "HEIC", "PDF" })
        {
            var result = ConvertFile(source, target);
            Check(File.Exists(result.OutputPath) && result.Width == 32 && result.Height == 32, target);
            Check(Path.GetDirectoryName(result.OutputPath) == folder, "Output is not beside source");
            if (target == "PDF") { using var document = PdfDocument.Open(result.OutputPath); Check(document.NumberOfPages == 1, "Image PDF"); continue; }
            using var image = new MagickImage(result.OutputPath);
            Check(image.Width == 32 && image.Height == 32, "Decoded image dimensions");
            if (target == "JPG")
            {
                var white = image.GetPixels().GetPixel(0, 0).ToColor()!;
                Check(white.R > 240 && white.G > 240 && white.B > 240, "JPG alpha matte");
            }
            var back = ConvertFile(result.OutputPath, "PNG");
            using var png = new MagickImage(back.OutputPath); Check(png.Width == 32 && png.Format == MagickFormat.Png, "Roundtrip PNG");
        }
        var collision = ConvertFile(source, "JPG"); Check(collision.OutputPath.Contains("("), "Collision must get suffix");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Image source changed");
    }
    private static void ImageTools()
    {
        var source = Path.Combine(folder, "أداة صورة.png");
        using (var image = new MagickImage(MagickColors.Red, 80, 40))
        { image.SetAttribute("comment", "metadata-for-removal"); image.Write(source); }
        var hash = SHA256.HashData(File.ReadAllBytes(source));
        var rotated = ConvertFile(source, "ROTATE"); using (var image = new MagickImage(rotated.OutputPath)) Check(image.Width == 40 && image.Height == 80, "Rotation dimensions");
        var stripped = ConvertFile(source, "STRIP"); using (var image = new MagickImage(stripped.OutputPath)) Check(image.GetAttribute("comment") is null, "Metadata retained");
        var jpeg = Path.Combine(folder, "compression.jpg");
        using (var image = new MagickImage(MagickColors.Red, 512, 512)) { image.AddNoise(NoiseType.Gaussian); image.Quality = 100; image.Write(jpeg); }
        var compressed = ConvertFile(jpeg, "COMPRESS"); Check(new FileInfo(compressed.OutputPath).Length < new FileInfo(jpeg).Length, "Compression did not reduce size");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Tool changed source");
    }
    private static void Engine(params string[] args)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "media-engine", "ffmpeg.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!; var error = process.StandardError.ReadToEnd(); process.WaitForExit();
        Check(process.ExitCode == 0, "Fixture engine failed: " + error);
    }
    private static string MediaSource()
    {
        if (mediaFixture is not null) return mediaFixture;
        mediaFixture = Path.Combine(folder, "فيديو وصوت حقيقي.mp4");
        // Actual encoded test media, never passed off as Explorer/OLE integration evidence.
        Engine("-v", "error", "-nostdin", "-n", "-f", "lavfi", "-i", "testsrc2=size=96x64:rate=12:duration=2", "-f", "lavfi", "-i", "sine=frequency=440:duration=2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-threads", "2", mediaFixture);
        return mediaFixture;
    }
    private static void MediaFormats()
    {
        var source = MediaSource(); var hash = SHA256.HashData(File.ReadAllBytes(source));
        foreach (var target in new[] { "MOV", "M4A", "MP3", "GIF", "WEBM" })
        {
            var result = ConvertFile(source, target);
            Check(File.Exists(result.OutputPath) && Path.GetDirectoryName(result.OutputPath) == folder, target);
            if (target is "MOV" or "WEBM") { var back = ConvertFile(result.OutputPath, "MP4"); Check(back.Width == 96 && back.Height == 64, "Video roundtrip"); }
        }
        foreach (var extension in new[] { "mkv", "avi" })
        {
            var path = Path.Combine(folder, "video-fixture." + extension);
            Engine("-v", "error", "-nostdin", "-n", "-i", source, "-c:v", "mpeg4", "-c:a", "libmp3lame", path);
            Check(ConvertFile(path, "MP4").Width == 96, "Video source " + extension);
        }
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Video source modified");
    }
    private static void AudioFormats()
    {
        var audio = Path.Combine(folder, "audio-fixture.wav");
        Engine("-v", "error", "-nostdin", "-n", "-i", MediaSource(), "-vn", "-c:a", "pcm_s16le", audio);
        foreach (var target in new[] { "MP3", "M4A", "FLAC", "AIFF", "OPUS" })
        {
            var result = ConvertFile(audio, target); Check(new FileInfo(result.OutputPath).Length > 0, target);
            Check(new FileInfo(ConvertFile(result.OutputPath, "WAV").OutputPath).Length > 0, "Audio source " + target);
        }
    }
    private static void MediaFailures()
    {
        var silent = Path.Combine(folder, "silent.mp4");
        Engine("-v", "error", "-nostdin", "-n", "-i", MediaSource(), "-an", "-c:v", "copy", silent);
        var failed = false;
        try { ConvertFile(silent, "MP3"); } catch (InvalidDataException) { failed = true; }
        Check(failed && !File.Exists(Path.ChangeExtension(silent, ".mp3")), "Silent video produced fake audio");
        var corrupt = Path.Combine(folder, "corrupt.mp4"); File.WriteAllText(corrupt, "not a movie"); failed = false;
        try { ConvertFile(corrupt, "MOV"); } catch (InvalidDataException) { failed = true; }
        Check(failed, "Corrupt media accepted");
        using var cancellation = new CancellationTokenSource(100);
        var cancelled = false;
        try { Service.ConvertAsync(FileClassifier.Inspect(MediaSource()), "WEBM", cancellation.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && !Directory.EnumerateFileSystemEntries(folder, ".naqla-*").Any(), "Cancellation leaked temporary output");
    }
    private static string PdfSource()
    {
        if (pdfFixture is not null) return pdfFixture;
        pdfFixture = Path.Combine(folder, "مستند نصي صفحتين.pdf");
        using var stream = File.Create(pdfFixture); var positions = new List<long> { 0 };
        void Text(string value) => stream.Write(Encoding.ASCII.GetBytes(value));
        void Obj(string body) { positions.Add(stream.Position); Text($"{positions.Count - 1} 0 obj\n{body}\nendobj\n"); }
        Text("%PDF-1.4\n"); Obj("<< /Type /Catalog /Pages 2 0 R >>"); Obj("<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>");
        Obj("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << /Font << /F1 5 0 R >> >> /Contents 6 0 R >>");
        Obj("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << /Font << /F1 5 0 R >> >> /Contents 7 0 R >>");
        Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        foreach (var line in new[] { "Naqla first page", "Second page preserved" }) { var content = $"BT /F1 12 Tf 12 50 Td ({line}) Tj ET\n"; Obj($"<< /Length {content.Length} >>\nstream\n{content}endstream"); }
        var xref = stream.Position; Text($"xref\n0 {positions.Count}\n0000000000 65535 f \n");
        foreach (var position in positions.Skip(1)) Text($"{position:0000000000} 00000 n \n");
        Text($"trailer\n<< /Size {positions.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"); return pdfFixture;
    }
    private static void PdfFormats()
    {
        var source = PdfSource(); var hash = SHA256.HashData(File.ReadAllBytes(source));
        foreach (var target in new[] { "PNG", "JPG" })
        {
            var result = ConvertFile(source, target);
            Check(Directory.Exists(result.OutputPath) && result.Items == 2 && Directory.GetFiles(result.OutputPath).Length == 2, "PDF lost pages");
            foreach (var path in Directory.GetFiles(result.OutputPath)) { using var image = new MagickImage(path); Check(image.Width == 400 && image.Height == 200, "PDF raster size"); }
            Check(ConvertFile(source, target).OutputPath != result.OutputPath, "PDF folder collision");
        }
        foreach (var target in new[] { "TXT", "DOCX", "RTF" })
        {
            var path = ConvertFile(source, target).OutputPath; string text;
            if (target == "DOCX") { using var zip = ZipFile.OpenRead(path); using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()); text = reader.ReadToEnd(); }
            else text = File.ReadAllText(path);
            Check(text.Contains("Naqla first page") && text.Contains("Second page preserved"), "Text pages missing");
        }
        var raster = ConvertFile(CreatePng("raster-pdf-source.png"), "PDF");
        var failed = false; try { ConvertFile(raster.OutputPath, "TXT"); } catch (NotSupportedException) { failed = true; }
        Check(failed, "Raster PDF advertised fake OCR");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "PDF source changed");
    }
    private static void NativeWheel()
    {
        _ = Application.Current ?? new Application();
        var sheet = new DrawingVisual(); using (var dc = sheet.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 35, 43)), null, new Rect(0, 0, 1320, 326));
            var paths = new[] { Path.Combine(folder, "ألوان حقيقية.png"), MediaSource(), Path.Combine(folder, "فيديو وصوت حقيقي.mp3"), PdfSource(), Path.Combine(folder, "ألوان حقيقية.png") };
            for (var i = 0; i < paths.Length; i++)
            {
                var wheel = new PixelWheel(Service.CanExecute); wheel.SetFile(FileClassifier.Inspect(paths[i])); if (i == 4) wheel.SetMode(true, false);
                wheel.Track(new Point(130, 45)); wheel.Measure(new Size(260, 306)); wheel.Arrange(new Rect(0, 0, 260, 306));
                var bitmap = new RenderTargetBitmap(260, 306, 96, 96, PixelFormats.Pbgra32); bitmap.Render(wheel);
                var pixels = new byte[260 * 306 * 4]; bitmap.CopyPixels(pixels, 1040, 0);
                Check(pixels[3] == 0 && pixels[(305 * 260 + 259) * 4 + 3] == 0, "Opaque exterior rectangle");
                Check(pixels[(130 * 260 + 130) * 4 + 3] > 200, "Hub isn't a real OLE hit surface");
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var output = File.Create(Path.Combine(folder, $"native-wheel-{i}.png"))) encoder.Save(output);
                dc.DrawImage(bitmap, new Rect(2 + i * 264, 10, 260, 306));
            }
        }
        var contact = new RenderTargetBitmap(1320, 326, 96, 96, PixelFormats.Pbgra32); contact.Render(sheet);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(contact)); using var file = File.Create(Path.Combine(folder, "native-wheel-family.png")); png.Save(file);
    }
}
