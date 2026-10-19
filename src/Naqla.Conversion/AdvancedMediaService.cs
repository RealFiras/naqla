using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using ImageMagick;
using Naqla.Core;

namespace Naqla.Conversion;

/// <summary>Offline accurate trimming, actual frame decoding and measured two-pass normalization.</summary>
public sealed class AdvancedMediaService(string engineDirectory)
{
    private string Ffmpeg => Path.Combine(engineDirectory, "ffmpeg.exe");
    private string Ffprobe => Path.Combine(engineDirectory, "ffprobe.exe");
    public bool Available => File.Exists(Ffmpeg) && File.Exists(Ffprobe);
    public Task<MediaDetails> InspectAsync(DraggedFile file, CancellationToken token = default) => MediaProbe.ReadAsync(Ffprobe, file.FullPath, token);
    private static string N(double number) => number.ToString("0.########", CultureInfo.InvariantCulture);
    private static List<string> Input(string path, string level = "error") => ["-hide_banner", "-loglevel", level, "-nostdin", "-n", "-xerror", "-protocol_whitelist", "file,pipe", "-threads", "2", "-filter_threads", "2", "-i", path];

    public async Task<byte[]> PreviewFrameAsync(DraggedFile source, double seconds, CancellationToken token = default)
    {
        var info = await InspectAsync(source, token).ConfigureAwait(false);
        if (!info.HasVideo || !double.IsFinite(seconds) || seconds < 0 || seconds >= info.Duration) throw new ArgumentException("Choose a timestamp within the video.");
        using var output = new OutputFile(source.FullPath, "png");
        var args = Input(source.FullPath); args.AddRange(["-ss", N(seconds), "-map", "0:v:0", "-frames:v", "1", "-vf", "scale=w='min(800,iw)':h=-1", output.Temporary]);
        await LocalProcess.RunAsync(Ffmpeg, args, token).ConfigureAwait(false);
        using var decoded = new MagickImage(output.Temporary); if (decoded.Width == 0 || decoded.Height == 0) throw new InvalidDataException("Frame preview could not be decoded.");
        return await File.ReadAllBytesAsync(output.Temporary, token).ConfigureAwait(false);
    }

    public async Task<ConversionResult> ExecuteAsync(DraggedFile original, string operation, MediaToolOptions settings, CancellationToken token = default)
    {
        settings.Validate();
        var file = FileClassifier.Inspect(original.FullPath);
        // A read lock protects against modification while probes, two passes, and verification run.
        using var sourceLock = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var info = await InspectAsync(file, token).ConfigureAwait(false);
        if (operation == "FRAMES") return await FramesAsync(file, info, settings.Frames ?? throw new ArgumentException("Frame settings are required."), token).ConfigureAwait(false);
        if (operation is not ("TRIM" or "NORMALIZE" or "SILENCE" or "MONO" or "STEREO")) throw new NotSupportedException("Unknown media operation.");
        if (operation != "TRIM" && !info.HasAudio) throw new InvalidDataException("This file has no audio track.");
        if (operation == "STEREO" && info.Channels != 1) throw new ArgumentException("Dual-channel stereo requires a mono source.");
        var video = file.Category == FileCategory.Video;
        var format = video ? (file.Extension == "WEBM" ? "WEBM" : file.Extension == "MOV" ? "MOV" : "MP4") : file.Extension;
        if (format is not ("MP4" or "MOV" or "WEBM" or "MP3" or "M4A" or "WAV" or "FLAC" or "AIFF" or "OPUS")) throw new NotSupportedException("Unsupported output container.");
        using var output = new OutputFile(file.FullPath, format, " " + operation.ToLowerInvariant());
        var filters = new List<string>();
        var silence = settings.Silence;
        if (operation == "SILENCE") silence ??= new();
        if (silence is not null)
        {
            silence.Validate();
            var trim = $"silenceremove=start_periods=1:start_duration={N(silence.MinimumSeconds)}:start_silence={N(silence.MinimumSeconds)}:start_threshold={N(silence.ThresholdDb)}dB";
            if (silence.Leading) filters.Add(trim);
            if (silence.Trailing) { filters.Add("areverse"); filters.Add(trim); filters.Add("areverse"); }
        }
        var channels = operation == "MONO" ? AudioChannels.Mono : operation == "STEREO" ? AudioChannels.DualMonoStereo : settings.Channels;
        if (channels == AudioChannels.Mono) filters.Add("pan=mono|c0=" + (info.Channels == 1 ? "c0" : ".5*c0+.5*c1"));
        if (channels == AudioChannels.DualMonoStereo)
        {
            if (info.Channels != 1) throw new ArgumentException("Dual-channel stereo requires a mono source.");
            filters.Add("pan=stereo|c0=c0|c1=c0");
        }
        LoudnessSettings? loudness = operation == "NORMALIZE" ? settings.Loudness ?? new() : null;
        if (loudness is not null)
        {
            loudness.Validate();
            var baseFilter = $"loudnorm=I={N(loudness.Integrated)}:TP={N(loudness.TruePeak)}:LRA={N(loudness.Range)}";
            var first = Input(file.FullPath, "info"); first.AddRange(["-map", "0:a:0", "-vn", "-af", string.Join(',', filters.Append(baseFilter + ":print_format=json")), "-f", "null", "-"]);
            var measured = await LocalProcess.RunCapturedAsync(Ffmpeg, first, token, 0, 131072).ConfigureAwait(false);
            var start = measured.Error.LastIndexOf('{'); var end = measured.Error.LastIndexOf('}');
            if (start < 0 || end < start) throw new InvalidDataException("The first loudness pass did not produce measurements.");
            using var json = JsonDocument.Parse(measured.Error[start..(end + 1)]);
            double Value(string name) { var result = double.Parse(json.RootElement.GetProperty(name).GetString()!, CultureInfo.InvariantCulture); if (!double.IsFinite(result)) throw new InvalidDataException("No measurable speech/audio loudness; a silent input cannot be normalized."); return result; }
            filters.Add(baseFilter + $":measured_I={N(Value("input_i"))}:measured_TP={N(Value("input_tp"))}:measured_LRA={N(Value("input_lra"))}:measured_thresh={N(Value("input_thresh"))}:offset={N(Value("target_offset"))}:linear=true:print_format=summary");
        }
        var args = Input(file.FullPath);
        double? expectedDuration = null;
        if (operation == "TRIM")
        {
            var range = settings.Trim ?? throw new ArgumentException("Trim range is required."); range.Validate(info.Duration);
            // Accurate post-input seeking and re-encoding. A preference is never misreported as verified keyframe alignment.
            args.AddRange(["-ss", N(range.Start), "-t", N(range.End - range.Start)]); expectedDuration = range.End - range.Start;
        }
        if (video) args.AddRange(["-map", "0:v:0", "-map", "0:a:0?", "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2", "-pix_fmt", "yuv420p", "-c:v", format == "WEBM" ? "libvpx-vp9" : "libx264", "-crf", format == "WEBM" ? "32" : "22"]);
        else args.AddRange(["-map", "0:a:0", "-vn"]);
        if (filters.Count > 0) args.AddRange(["-af", string.Join(',', filters)]);
        args.AddRange(AudioArguments(format));
        if (info.HasAudio) args.AddRange(["-ar", Math.Clamp(info.SampleRate, 8000, 48000).ToString(CultureInfo.InvariantCulture)]);
        args.AddRange(["-map_metadata", "-1", "-map_chapters", "-1", "-threads", "2", output.Temporary]);
        await LocalProcess.RunAsync(Ffmpeg, args, token).ConfigureAwait(false);
        var result = await MediaProbe.ReadAsync(Ffprobe, output.Temporary, token).ConfigureAwait(false);
        MediaConverter.ValidateFormat(result, format, !video);
        if (expectedDuration is double expected && Math.Abs(result.Duration - expected) > Math.Max(.15, 1 / Math.Max(1, info.FrameRate) + .08)) throw new InvalidDataException("Accurate trim duration verification failed.");
        if (operation == "SILENCE" && result.Duration >= info.Duration - .01) throw new InvalidDataException("No leading or trailing silence was removed with these settings.");
        if ((channels == AudioChannels.Mono && result.Channels != 1) || (channels == AudioChannels.DualMonoStereo && result.Channels != 2)) throw new InvalidDataException("Output channel count verification failed.");
        double? actualI = null, actualPeak = null;
        if (loudness is not null)
        {
            // A different FFmpeg filter measures the decoded final encoding, including lossy overshoot.
            var measureArgs = Input(output.Temporary, "info"); measureArgs.AddRange(["-map", "0:a:0", "-af", "ebur128=peak=true", "-f", "null", "-"]);
            var measure = await LocalProcess.RunCapturedAsync(Ffmpeg, measureArgs, token, 0, 1_048_576).ConfigureAwait(false);
            var summary = measure.Error.LastIndexOf("Summary:", StringComparison.Ordinal);
            if (summary < 0) throw new InvalidDataException("Independent loudness verification is missing.");
            var text = measure.Error[summary..];
            actualI = Measure(text, @"I:\s*(-?[\d.]+)\s*LUFS"); actualPeak = Measure(text, @"Peak:\s*(-?[\d.]+)\s*dBFS");
            if (Math.Abs(actualI.Value - loudness.Integrated) > 1.2 || actualPeak > loudness.TruePeak + .3) throw new InvalidDataException("The decoded result did not meet the configured loudness / true-peak limits. No output was published.");
        }
        await DecodeAsync(output.Temporary, token).ConfigureAwait(false);
        var bytes = new FileInfo(output.Temporary).Length;
        return new(output.Publish(token), result.Width, result.Height, Report: new(file.Size, bytes, operation == "TRIM" ? "Accurate re-encoded trim" : operation, true, actualI, actualPeak));
    }

    private static double Measure(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !double.IsFinite(result)) throw new InvalidDataException("The decoded measurement is not finite.");
        return result;
    }
    internal static string[] AudioArguments(string format) => format switch
    {
        "MP3" => ["-c:a", "libmp3lame", "-b:a", "192k"], "WAV" => ["-c:a", "pcm_s16le"], "FLAC" => ["-c:a", "flac"], "AIFF" => ["-c:a", "pcm_s16be"], "OPUS" or "WEBM" => ["-c:a", "libopus", "-b:a", "128k"], _ => ["-c:a", "aac", "-b:a", "192k"]
    };
    internal Task DecodeAsync(string path, CancellationToken token) => LocalProcess.RunAsync(Ffmpeg, ["-hide_banner", "-loglevel", "error", "-nostdin", "-xerror", "-protocol_whitelist", "file,pipe", "-threads", "2", "-i", path, "-map", "0:v?", "-map", "0:a?", "-f", "null", "-"], token);

    private async Task<ConversionResult> FramesAsync(DraggedFile file, MediaDetails info, FrameSettings settings, CancellationToken token)
    {
        if (!info.HasVideo) throw new InvalidDataException("The input has no video track.");
        var times = settings.Timestamps(info.Duration);
        using var folder = new OutputDirectory(file.FullPath, " frames"); var frames = new List<object>(); long bytes = 0;
        for (var i = 0; i < times.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            var name = $"frame-{i + 1:0000}.{settings.Format.ToLowerInvariant()}"; var path = Path.Combine(folder.Temporary, name);
            // Selection keeps the original PTS and reports only selected frames, so long seeks cannot truncate the timing evidence.
            var args = Input(file.FullPath, "info"); args.AddRange(["-map", "0:v:0", "-frames:v", "1", "-vf", $"select='gte(t,{N(times[i])})',showinfo", "-fps_mode", "vfr", "-q:v", "2", path]);
            var result = await LocalProcess.RunCapturedAsync(Ffmpeg, args, token, 0, 1_048_576).ConfigureAwait(false);
            using var decoded = new MagickImage(path);
            if ((int)decoded.Width != info.DisplayWidth || (int)decoded.Height != info.DisplayHeight) throw new InvalidDataException("Extracted frame orientation/dimensions did not match the video.");
            // showinfo runs before output seeking and reports the original presentation timestamps.
            var matches = Regex.Matches(result.Error, @"pts_time:([\d.eE+\-]+)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            var pts = matches.Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Where(x => x + .0001 >= times[i]).ToArray();
            if (pts.Length == 0) throw new InvalidDataException("No independently reported frame presentation timestamp was available.");
            var actual = pts[0]; if (!double.IsFinite(actual) || actual < times[i] - .0001 || actual > info.Duration + .1) throw new InvalidDataException("Frame timing verification failed.");
            bytes += new FileInfo(path).Length; frames.Add(new { File = name, RequestedSeconds = times[i], ActualSeconds = actual });
        }
        await File.WriteAllTextAsync(Path.Combine(folder.Temporary, "frames.json"), JsonSerializer.Serialize(new { Count = times.Length, Frames = frames }, new JsonSerializerOptions { WriteIndented = true }), token).ConfigureAwait(false);
        return new(folder.Publish(token), info.DisplayWidth, info.DisplayHeight, times.Length, Report: new(file.Size, bytes, $"{times.Length} decoded frames"));
    }
}
