using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using ImageMagick;
using Naqla.Conversion;
using Naqla.Core;

internal static partial class Program
{
    private static void RunAdvancedTests()
    {
        Run("Advanced settings reject invalid ranges, nonfinite values and unsafe frame counts", AdvancedSettings);
        Run("30-second video: real frames every five seconds, requested/actual timing, decode and collisions", FrameExtraction);
        Run("Accurate video/audio trims validate duration and retain original hashes", AccurateTrim);
        Run("Two-pass normalization independently measures LUFS/true peak; silence edges and channels", NormalizeAudio);
        Run("Advanced media cancellation, corrupt input and impossible options publish nothing", AdvancedFailures);
    }
    private static void AdvancedSettings()
    {
        foreach (var settings in new[] { new FrameSettings(EverySeconds: 0, Selection: FrameSelection.Every), new FrameSettings(Start: double.NaN), new FrameSettings(End: 300), new FrameSettings(Count: 301, Selection: FrameSelection.Count) })
        { var rejected = false; try { settings.Timestamps(30); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid frame setting accepted"); }
        Check(new FrameSettings(Selection: FrameSelection.Every).Timestamps(30).SequenceEqual(new double[] { 0, 5, 10, 15, 20, 25 }), "Every-five-second schedule");
        foreach (var range in new[] { new TrimRange(-1, 2), new TrimRange(2, 1), new TrimRange(0, 31), new TrimRange(double.NaN, 1) })
        { var rejected = false; try { range.Validate(30); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid trim accepted"); }
    }
    private static string LongVideo()
    {
        var path = Path.Combine(folder, "ثلاثون ثانية.mp4"); if (File.Exists(path)) return path;
        Engine("-v", "error", "-nostdin", "-n", "-f", "lavfi", "-i", "testsrc2=size=96x64:rate=10:duration=30", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-threads", "2", path); return path;
    }
    private static void FrameExtraction()
    {
        var path = LongVideo(); var original = SHA256.HashData(File.ReadAllBytes(path));
        var request = new ConversionRequest([FileClassifier.Inspect(path)], "FRAMES", MediaOptions: new(Frames: new(FrameSelection.Every)));
        var result = Service.ConvertAsync(request).GetAwaiter().GetResult();
        Check(Directory.Exists(result.OutputPath) && result.Items == 6, "Six frames expected");
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.OutputPath, "frames.json")));
        var index = 0; foreach (var frame in json.RootElement.GetProperty("Frames").EnumerateArray())
        {
            var expected = index++ * 5; Check(Math.Abs(frame.GetProperty("RequestedSeconds").GetDouble() - expected) < .001, "Requested time mismatch");
            Check(Math.Abs(frame.GetProperty("ActualSeconds").GetDouble() - expected) <= .101, "Actual PTS mismatch");
            using var image = new MagickImage(Path.Combine(result.OutputPath, frame.GetProperty("File").GetString()!)); Check(image.Width == 96 && image.Height == 64 && image.Format == MagickFormat.Png, "Decoded frame mismatch");
        }
        var single = Service.ConvertAsync(request with { MediaOptions = new(Frames: new(FrameSelection.Single, Start: 1.23, Format: "JPG")) }).GetAwaiter().GetResult();
        Check(single.Items == 1 && single.OutputPath != result.OutputPath && Directory.GetFiles(single.OutputPath, "*.jpg").Length == 1, "Single JPG/collision");
        var count = Service.ConvertAsync(request with { MediaOptions = new(Frames: new(FrameSelection.Count, Start: 5, End: 15, Count: 3)) }).GetAwaiter().GetResult();
        Check(count.Items == 3, "Count/range frame extraction"); Check(original.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Source changed");
    }
    private static void AccurateTrim()
    {
        var path = LongVideo(); var hash = SHA256.HashData(File.ReadAllBytes(path));
        var result = Service.ConvertAsync(new ConversionRequest([FileClassifier.Inspect(path)], "TRIM", MediaOptions: new(Trim: new(1.23, 4.77, true)))).GetAwaiter().GetResult();
        var info = Service.AdvancedMedia.InspectAsync(FileClassifier.Inspect(result.OutputPath)).GetAwaiter().GetResult();
        Check(Math.Abs(info.Duration - 3.54) <= .15 && result.Report?.Reencoded == true, "Accurate duration / truthful encoding report");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Trim modified source");
    }
    private static void NormalizeAudio()
    {
        var path = Path.Combine(folder, "صوت هادئ.wav");
        Engine("-v", "error", "-nostdin", "-n", "-f", "lavfi", "-i", "aevalsrc=if(between(t\\,1\\,5)\\,0.015*sin(2*PI*440*t)\\,0):s=48000:d=6", "-c:a", "pcm_s16le", path);
        var hash = SHA256.HashData(File.ReadAllBytes(path)); var source = FileClassifier.Inspect(path);
        var normalized = Service.ConvertAsync(new ConversionRequest([source], "NORMALIZE", MediaOptions: new(Loudness: new(-16, -1.5)))).GetAwaiter().GetResult();
        Check(normalized.Report?.MeasuredLoudness is double level && Math.Abs(level + 16) < 1.2 && normalized.Report.MeasuredTruePeak <= -1.2, "Independent loudness / true peak missing");
        var trimmed = Service.ConvertAsync(new ConversionRequest([source], "SILENCE", MediaOptions: new(Silence: new()))).GetAwaiter().GetResult();
        var info = Service.AdvancedMedia.InspectAsync(FileClassifier.Inspect(trimmed.OutputPath)).GetAwaiter().GetResult(); Check(info.Duration is > 3.8 and < 4.4, "Silence was not trimmed at the edges");
        var stereo = Service.ConvertAsync(new ConversionRequest([source], "STEREO", MediaOptions: new())).GetAwaiter().GetResult();
        Check(Service.AdvancedMedia.InspectAsync(FileClassifier.Inspect(stereo.OutputPath)).GetAwaiter().GetResult().Channels == 2, "Dual mono stereo missing");
        var mono = Service.ConvertAsync(new ConversionRequest([FileClassifier.Inspect(stereo.OutputPath)], "MONO", MediaOptions: new())).GetAwaiter().GetResult();
        Check(Service.AdvancedMedia.InspectAsync(FileClassifier.Inspect(mono.OutputPath)).GetAwaiter().GetResult().Channels == 1, "Stereo to mono missing");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Audio source changed");
    }
    private static void AdvancedFailures()
    {
        var source = FileClassifier.Inspect(LongVideo()); var before = Directory.GetFileSystemEntries(folder).ToHashSet();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var rejected = false; try { Service.ConvertAsync(new ConversionRequest([source], "FRAMES", MediaOptions: new(Frames: new())), cancellation.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { rejected = true; }
        Check(rejected, "Cancellation ignored");
        rejected = false; try { Service.ConvertAsync(new ConversionRequest([source], "TRIM", MediaOptions: new(Trim: new(30, 40)))).GetAwaiter().GetResult(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Out-of-range trim accepted");
        Check(before.SetEquals(Directory.GetFileSystemEntries(folder)), "Failure published leftover files");
    }
}
