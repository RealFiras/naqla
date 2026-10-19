using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using ImageMagick.Drawing;
using Naqla.Conversion;
using Naqla.Core;

internal static partial class Program
{
    private static string Components
    {
        get { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "Naqla.sln"))) return Path.Combine(directory.FullName, "artifacts", "optional-components"); throw new DirectoryNotFoundException("Run the developer tests from inside the Naqla checkout."); }
    }
    private static LocalModelService Models() => new(Components, Path.Combine(AppContext.BaseDirectory, "media-engine"));
    private static void RunModelTests()
    {
        Run("Real local Tesseract English/Arabic/mixed OCR, blank rejection, Unicode and source hashes", LocalOcr);
        Run("Real CPU U2NetP inference generates validated nontrivial alpha without changing source", LocalSegmentation);
        Run("Real multilingual whisper.cpp JFK transcription produces monotonic editable SRT/WebVTT", LocalSpeech);
        Run("Subtitle timestamp validation and missing/tampered local model rejection", LocalModelGuards);
    }
    private static void OcrFixture(string path, string text, FlowDirection direction)
    {
        var drawing = new DrawingVisual(); using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1400, 220));
            context.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("ar-JO"), direction, new Typeface("Arial"), 70, Brushes.Black, 1), new(direction == FlowDirection.RightToLeft ? 1360 : 40, 55));
        }
        var bitmap = new RenderTargetBitmap(1400, 220, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var output = File.Create(path); png.Save(output);
    }
    private static void LocalOcr()
    {
        var model = Models(); var english = Path.Combine(folder, "نص إنجليزي.png"); OcrFixture(english, "Naqla offline image text 123", FlowDirection.LeftToRight);
        var hash = SHA256.HashData(File.ReadAllBytes(english));
        var result = model.RecognizeAsync(FileClassifier.Inspect(english), "eng").GetAwaiter().GetResult();
        Check(result.Text.Contains("offline", StringComparison.OrdinalIgnoreCase) && result.Text.Contains("123"), "Actual English OCR text mismatch: " + result.Text);
        var saved = model.SaveOcrAsync(FileClassifier.Inspect(english), result).GetAwaiter().GetResult(); Check(File.ReadAllText(saved.OutputPath) == result.Text, "OCR saved content mismatch");
        var arabic = Path.Combine(folder, "نص عربي.png"); OcrFixture(arabic, "مرحبا بكم في عالم الصور", FlowDirection.RightToLeft);
        var ar = model.RecognizeAsync(FileClassifier.Inspect(arabic), "ara").GetAwaiter().GetResult(); Check(ar.Text.Contains("عالم") && ar.Text.Contains("الصور"), "Actual Arabic OCR mismatch: " + ar.Text);
        var mixedPath = Path.Combine(folder, "نص مختلط.png");
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1600, 380));
            dc.DrawText(new FormattedText("Naqla offline text 123", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 70, Brushes.Black, 1), new(40, 45));
            dc.DrawText(new FormattedText("مرحبا بكم في عالم الصور", CultureInfo.GetCultureInfo("ar-JO"), FlowDirection.RightToLeft, new Typeface("Arial"), 70, Brushes.Black, 1), new(1550, 210));
        }
        var mixedBitmap = new RenderTargetBitmap(1600, 380, 96, 96, PixelFormats.Pbgra32); mixedBitmap.Render(visual); var mixedPng = new PngBitmapEncoder(); mixedPng.Frames.Add(BitmapFrame.Create(mixedBitmap)); using (var write = File.Create(mixedPath)) mixedPng.Save(write);
        var mixed = model.RecognizeAsync(FileClassifier.Inspect(mixedPath), "eng+ara").GetAwaiter().GetResult(); Check(mixed.Text.Contains("offline", StringComparison.OrdinalIgnoreCase) && mixed.Text.Contains("الصور"), "Actual mixed-script fixture OCR: " + mixed.Text);
        var blank = Path.Combine(folder, "صورة فارغة.png"); using (var image = new MagickImage(MagickColors.White, 800, 400)) image.Write(blank);
        var empty = model.RecognizeAsync(FileClassifier.Inspect(blank)).GetAwaiter().GetResult(); Check(!empty.HasText, "Blank image invented text");
        var rejected = false; try { model.SaveOcrAsync(FileClassifier.Inspect(blank), empty).GetAwaiter().GetResult(); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Empty OCR reported success");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(english))), "OCR changed source");
    }
    private static void LocalSegmentation()
    {
        var path = Path.Combine(folder, "عزل محلي.png");
        using (var image = new MagickImage(MagickColors.White, 400, 300))
        { new Drawables().FillColor(MagickColors.Coral).StrokeColor(MagickColors.DarkRed).Circle(200, 150, 200, 65).Draw(image); image.Write(path); }
        var hash = SHA256.HashData(File.ReadAllBytes(path)); var result = Models().SaveBackgroundAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult();
        using var decoded = new MagickImage(result.OutputPath); Check(decoded.Width == 400 && decoded.Height == 300 && decoded.HasAlpha && decoded.Format == MagickFormat.Png, "Segmentation PNG validation");
        using var alpha = decoded.Clone(); alpha.Alpha(AlphaAction.Extract); var range = alpha.ToByteArray(MagickFormat.Gray); Check(range.Min() < 60 && range.Max() > 190, "Actual model did not produce nontrivial alpha");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Segmentation changed source");
        File.WriteAllText(Path.Combine(folder, "segmentation-test-scope.txt"), "Real U2NetP inference on an explicitly synthetic acceptance fixture. Photo/hair/edge quality still requires manual representative images.");
    }
    private static void LocalSpeech()
    {
        var source = Path.Combine(folder, "خطاب JFK.wav"); File.Copy(Path.Combine(Components, "fixtures", "jfk.wav"), source);
        var hash = SHA256.HashData(File.ReadAllBytes(source)); var model = Models();
        var document = model.TranscribeAsync(FileClassifier.Inspect(source), "en").GetAwaiter().GetResult();
        var text = string.Join(' ', document.Cues.Select(x => x.Text));
        Check(text.Contains("country", StringComparison.OrdinalIgnoreCase) && text.Contains("you", StringComparison.OrdinalIgnoreCase), "Real speech transcript does not match the known JFK reference: " + text);
        var srt = model.SaveSubtitlesAsync(FileClassifier.Inspect(source), document, false).GetAwaiter().GetResult();
        var vtt = model.SaveSubtitlesAsync(FileClassifier.Inspect(source), document, true).GetAwaiter().GetResult();
        Check(File.ReadAllText(srt.OutputPath).Contains(" --> ") && File.ReadAllText(vtt.OutputPath).StartsWith("WEBVTT\n"), "Subtitle encodings");
        foreach (var output in new[] { srt, vtt })
        {
            using var actual = System.Text.Json.JsonDocument.Parse(CodecOutput("ffprobe.exe", "-v", "error", "-show_packets", "-show_entries", "packet=pts_time,duration_time", "-of", "json", output.OutputPath));
            var packets = actual.RootElement.GetProperty("packets").EnumerateArray().ToArray(); Check(packets.Length == document.Cues.Count, "Independent FFmpeg subtitle parser cue count");
            for (var i = 0; i < packets.Length; i++) { var start = double.Parse(packets[i].GetProperty("pts_time").GetString()!, CultureInfo.InvariantCulture); var duration = double.Parse(packets[i].GetProperty("duration_time").GetString()!, CultureInfo.InvariantCulture); Check(Math.Abs(start - document.Cues[i].Start) <= .001 && Math.Abs(start + duration - document.Cues[i].End) <= .002, "Independent subtitle timestamp mismatch"); }
        }
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Speech changed source");
        File.WriteAllText(Path.Combine(folder, "jfk-actual-transcript.txt"), text);
        var arabicSource = Path.GetFullPath(Path.Combine(Components, "../../assets/test-fixtures/arabic-marhaba.wav"));
        var arabic = Path.Combine(folder, "كلام عربي.wav"); File.Copy(arabicSource, arabic);
        var arabicDocument = model.TranscribeAsync(FileClassifier.Inspect(arabic), "ar").GetAwaiter().GetResult();
        var arabicText = string.Join(' ', arabicDocument.Cues.Select(x => x.Text));
        Check(arabicText.Contains("مرحب"), "Actual human Arabic speech reference mismatch: " + arabicText);
        model.SaveSubtitlesAsync(FileClassifier.Inspect(arabic), arabicDocument, false).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(folder, "arabic-actual-transcript.txt"), arabicText + "\nSingle-word fixture only; continuous speech and dialect accuracy require manual review.");
    }
    private static void LocalModelGuards()
    {
        foreach (var cues in new[] { new[] { new SubtitleCue(1, 2, "a"), new SubtitleCue(1.5, 3, "b") }, new[] { new SubtitleCue(0, double.NaN, "x") }, new[] { new SubtitleCue(0, 2, "") }, new[] { new SubtitleCue(0, 2, "a\n\nb") } })
        { var rejected = false; try { new SubtitleDocument(cues, 4, "en").Encode(false); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid subtitle accepted"); }
        Check(new SubtitleDocument([new(0, 1, "a < b & c")], 2, "en").Encode(true).Contains("a &lt; b &amp; c"), "WebVTT speech text was interpreted as markup");
        var missing = new LocalModelService(Path.Combine(folder, "missing-model-pack"), ""); var rejectedMissing = false;
        try { missing.RecognizeAsync(FileClassifier.Inspect(Path.Combine(folder, "نص إنجليزي.png"))).GetAwaiter().GetResult(); } catch (NotSupportedException) { rejectedMissing = true; }
        Check(rejectedMissing, "Missing local pack did not fail explicitly");
        var corrupt = Path.Combine(folder, "tampered-model"); Directory.CreateDirectory(corrupt); File.WriteAllText(Path.Combine(corrupt, "u2netp.onnx"), "invalid model");
        var rejectedHash = false; try { new LocalModelService(corrupt, "").RemoveBackgroundPreviewAsync(FileClassifier.Inspect(Path.Combine(folder, "عزل محلي.png"))).GetAwaiter().GetResult(); } catch (InvalidDataException) { rejectedHash = true; }
        Check(rejectedHash, "Tampered model accepted");
    }
}
