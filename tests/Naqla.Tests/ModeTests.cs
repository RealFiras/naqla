using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Naqla.Core;
using Naqla.Wheel;

internal static partial class Program
{
    private static void CtrlModeTransitions()
    {
        var state = new WheelModeState(); state.Reset();
        Check(state.Mode == WheelMode.Convert, "New drag must begin in Convert");
        Check(state.ControlChanged(true) && state.Mode == WheelMode.Tools, "First Ctrl down");
        Check(!state.ControlChanged(true) && state.Mode == WheelMode.Tools, "Key repeat toggled mode");
        Check(!state.ControlChanged(false) && state.Mode == WheelMode.Tools, "Ctrl release reverted quick tap");
        Check(state.ControlChanged(true) && state.Mode == WheelMode.Convert, "Second press must return to Convert");
        state.ControlChanged(false); state.ControlChanged(true);
        state.Reset(controlAlreadyDown: true);
        Check(state.Mode == WheelMode.Convert && !state.ControlChanged(true), "Held key/auto-repeat changed a new drag");
        Check(!state.ControlChanged(false) && state.ControlChanged(true) && state.Mode == WheelMode.Tools, "Release and fresh press after reset");
        state.Reset(); Check(state.Mode == WheelMode.Convert, "Mode leaked between drags");
    }
    private static void ToolsWheelModes()
    {
        _ = Application.Current ?? new Application();
        var state = new WheelModeState(); state.Reset(); state.ControlChanged(true);
        var wheel = new PixelWheel(Service.CanExecute);
        // A quick tap can arrive before the source IDataObject is confirmed. Mode must survive SetFile.
        wheel.SetMode(state.Mode == WheelMode.Tools, false);
        state.ControlChanged(false);
        wheel.SetFile(FileClassifier.Inspect(MediaSource()));
        Check(wheel.Mode == WheelMode.Tools && wheel.OptionsCount == 4, "MP4 tools were rejected or pending mode lost");
        wheel.Track(new Point(130, 45)); Check(wheel.Selection?.Id == "COMPRESS" && wheel.Selection.Enabled, "Video tool not executable");
        wheel.SetMode(false, false); Check(wheel.Selection is null, "Old tool selection survived mode switch");
        wheel.Track(new Point(130, 45)); Check(wheel.Selection?.Id == "MOV", "Convert mode didn't return");
        wheel.SetMode(true, false); Check(wheel.Selection is null, "Old format selection survived mode switch");
        var paths = new[] { MediaSource(), Path.Combine(folder, "فيديو وصوت حقيقي.mp3"), PdfSource() };
        var expected = new[] { 4, 2, 7 };
        var sheet = new DrawingVisual();
        using (var dc = sheet.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 35, 43)), null, new Rect(0, 0, 792, 326));
            for (var i = 0; i < paths.Length; i++)
            {
                var file = FileClassifier.Inspect(paths[i]); wheel.Reset(); wheel.SetFile(file); wheel.SetMode(true, false);
                Check(wheel.Mode == WheelMode.Tools && wheel.OptionsCount == expected[i], "Wrong tools for " + file.Extension);
                Check(WheelOptions.For(file, WheelMode.Tools, Service.CanExecute).All(x => x.Enabled), "Dead tool advertised");
                wheel.Track(new Point(130, 45));
                wheel.Measure(new Size(260, 306)); wheel.Arrange(new Rect(0, 0, 260, 306));
                var bitmap = new RenderTargetBitmap(260, 306, 96, 96, PixelFormats.Pbgra32); bitmap.Render(wheel);
                var pixels = new byte[260 * 306 * 4]; bitmap.CopyPixels(pixels, 1040, 0);
                Check(pixels[3] == 0, "Mode introduced a background rectangle");
                // PDF's expanded contextual actions retain an opaque, connected annulus.
                if (i == 2) Check(pixels[(130 * 260 + 30) * 4 + 3] > 240, "PDF tool ring is missing");
                dc.DrawImage(bitmap, new Rect(i * 264, 10, 260, 306));
            }
        }
        var rendered = new RenderTargetBitmap(792, 326, 96, 96, PixelFormats.Pbgra32); rendered.Render(sheet);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(rendered));
        using var output = File.Create(Path.Combine(folder, "native-tools-modes.png")); encoder.Save(output);
    }
    private static string CodecOutput(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "media-engine", executable))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync(); var output = process.StandardOutput.ReadToEndAsync();
        process.WaitForExit(); Check(process.ExitCode == 0, "Codec verification failed: " + error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }
    private static bool HasAudio(string path)
    {
        using var json = JsonDocument.Parse(CodecOutput("ffprobe.exe", "-v", "error", "-show_entries", "stream=codec_type", "-of", "json", path));
        return json.RootElement.GetProperty("streams").EnumerateArray().Any(x => x.GetProperty("codec_type").GetString() == "audio");
    }
    private static string PixelHash(string path) => CodecOutput("ffmpeg.exe", "-v", "error", "-nostdin", "-i", path, "-map", "0:v:0", "-f", "hash", "-hash", "sha256", "-").Trim();
    private static void MediaTools()
    {
        var video = Path.Combine(folder, "أدوات فيديو.mp4");
        Engine("-v", "error", "-nostdin", "-n", "-i", MediaSource(), "-vf", "scale=384:256", "-c:v", "libx264", "-crf", "0", "-preset", "ultrafast", "-c:a", "aac", "-b:a", "256k", "-metadata", "title=private-test-title", video);
        var original = SHA256.HashData(File.ReadAllBytes(video));
        var compressed = ConvertFile(video, "COMPRESS");
        Check(new FileInfo(compressed.OutputPath).Length < new FileInfo(video).Length && compressed.Width == 384 && HasAudio(compressed.OutputPath), "Video compression");
        var rotated = ConvertFile(video, "ROTATE"); Check(rotated.Width == 256 && rotated.Height == 384 && HasAudio(rotated.OutputPath), "90-degree video rotation");
        var muted = ConvertFile(video, "MUTE"); Check(!HasAudio(muted.OutputPath), "Mute retained audio");
        Check(PixelHash(muted.OutputPath) == PixelHash(video), "Mute re-encoded/damaged video pixels");
        var clean = ConvertFile(video, "STRIP");
        var tags = CodecOutput("ffprobe.exe", "-v", "error", "-show_entries", "format_tags=title:stream_tags=title", "-of", "json", clean.OutputPath);
        Check(!tags.Contains("private-test-title") && PixelHash(clean.OutputPath) == PixelHash(video) && HasAudio(clean.OutputPath), "Video metadata cleaning changed pixels/tracks or retained title");
        Check(original.SequenceEqual(SHA256.HashData(File.ReadAllBytes(video))), "Video tools modified source");
        var audio = Path.Combine(folder, "أدوات صوت.mp3");
        Engine("-v", "error", "-nostdin", "-n", "-i", video, "-vn", "-c:a", "libmp3lame", "-b:a", "320k", "-metadata", "title=private-audio-title", audio);
        var audioHash = SHA256.HashData(File.ReadAllBytes(audio));
        var compact = ConvertFile(audio, "COMPRESS"); Check(new FileInfo(compact.OutputPath).Length < new FileInfo(audio).Length && HasAudio(compact.OutputPath), "Audio compression");
        var cleanAudio = ConvertFile(audio, "STRIP");
        var audioTags = CodecOutput("ffprobe.exe", "-v", "error", "-show_entries", "format_tags=title:stream_tags=title", "-of", "json", cleanAudio.OutputPath);
        Check(!audioTags.Contains("private-audio-title") && HasAudio(cleanAudio.OutputPath), "Audio title was retained or audio lost");
        Check(audioHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(audio))), "Audio tools modified source");
        Check(!Directory.EnumerateFileSystemEntries(folder, ".naqla-*").Any(), "Media tool temporary output leaked");
    }
}
