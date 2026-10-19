using System.IO;
using System.Globalization;
using System.Text.Json;
using Naqla.Core;

namespace Naqla.Conversion;

internal sealed class MediaConverter(string engineDirectory)
{
    private string Ffmpeg => Path.Combine(engineDirectory, "ffmpeg.exe");
    private string Ffprobe => Path.Combine(engineDirectory, "ffprobe.exe");
    public bool Available => File.Exists(Ffmpeg) && File.Exists(Ffprobe);
    public async Task<ConversionResult> ConvertAsync(DraggedFile file, string target, CancellationToken token, MediaToolOptions? options = null)
    {
        options ??= new(); options.Validate();
        var source = await Probe(file.FullPath, token, Demuxer(file.Extension)).ConfigureAwait(false);
        var tool = target is "COMPRESS" or "ROTATE" or "STRIP" or "MUTE";
        var destination = tool ? ToolFormat(file, target) : target;
        var audioOnly = destination is "MP3" or "M4A" or "WAV" or "FLAC" or "AIFF" or "OPUS";
        if (audioOnly && !source.Audio) throw new InvalidDataException("This file has no audio track to convert.");
        if (!audioOnly && source.Width == 0) throw new InvalidDataException("This file has no video track to convert.");
        var suffix = target switch { "COMPRESS" => " compressed", "ROTATE" => " rotated", "STRIP" => " clean", "MUTE" => " muted", _ => "" };
        using var output = new OutputFile(file.FullPath, destination, suffix);
        using var palette = target == "GIF" ? new OutputFile(file.FullPath, "png") : null;
        const string gifFilter = "fps=12,scale=w='min(640,iw)':h=-1:flags=lanczos";
        if (palette is not null)
        {
            // Two passes avoid retaining an entire video in RAM while waiting for palettegen.
            await LocalProcess.RunAsync(Ffmpeg, ["-hide_banner", "-loglevel", "error", "-nostdin", "-n", "-xerror", "-protocol_whitelist", "file,pipe", "-threads", "2", "-filter_threads", "2", "-f", Demuxer(file.Extension), "-i", file.FullPath, "-vf", gifFilter + ",palettegen", "-frames:v", "1", palette.Temporary], token).ConfigureAwait(false);
        }
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin", "-n", "-xerror", "-protocol_whitelist", "file,pipe", "-threads", "2", "-filter_threads", "2", "-f", Demuxer(file.Extension), "-i", file.FullPath };
        if (target is "STRIP" or "MUTE")
        {
            // Copy encoded packets without degrading quality. Mute keeps only the primary video;
            // metadata cleaning preserves the existing container and its streams.
            args.AddRange(target == "MUTE" ? ["-map", "0:v:0", "-an"] : ["-map", "0"]);
            args.AddRange(["-c", "copy"]);
        }
        else if (audioOnly)
        {
            args.AddRange(["-map", "0:a:0", "-vn"]);
            args.AddRange(destination switch
            {
                "MP3" => ["-c:a", "libmp3lame", "-b:a", target == "COMPRESS" ? AudioBitrate(options.Quality) : "192k"],
                "M4A" => ["-c:a", "aac", "-b:a", target == "COMPRESS" ? AudioBitrate(options.Quality) : "192k"],
                "WAV" => ["-c:a", "pcm_s16le"],
                "FLAC" => ["-c:a", "flac", "-compression_level", "8"],
                "AIFF" => ["-c:a", "pcm_s16be"],
                "OPUS" => ["-c:a", "libopus", "-b:a", target == "COMPRESS" ? AudioBitrate(options.Quality) : "128k"],
                _ => throw new NotSupportedException()
            });
        }
        else if (target == "GIF")
        {
            args.AddRange(["-i", palette!.Temporary, "-filter_complex_threads", "2", "-filter_complex", $"[0:v:0]{gifFilter}[v];[v][1:v]paletteuse", "-an", "-loop", "0"]);
        }
        else
        {
            var rotationFilter = options.Rotation switch { -90 or 270 => "transpose=cclock,", 180 => "hflip,vflip,", _ => "transpose=clock," };
            var filter = (target == "ROTATE" ? rotationFilter : "") + "pad=ceil(iw/2)*2:ceil(ih/2)*2";
            args.AddRange(["-map", "0:v:0", "-map", "0:a:0?", "-vf", filter, "-pix_fmt", "yuv420p"]);
            args.AddRange(destination == "WEBM"
                ? ["-c:v", "libvpx-vp9", "-crf", target == "COMPRESS" ? (options.Quality switch { MediaQuality.High => "34", MediaQuality.Small => "45", _ => "40" }) : "32", "-b:v", "0", "-cpu-used", "4", "-c:a", "libopus", "-b:a", target == "COMPRESS" ? AudioBitrate(options.Quality) : "128k"]
                : ["-c:v", "libx264", "-preset", "fast", "-crf", target == "COMPRESS" ? (options.Quality switch { MediaQuality.High => "25", MediaQuality.Small => "35", _ => "30" }) : "22", "-c:a", "aac", "-b:a", target == "COMPRESS" ? AudioBitrate(options.Quality) : "192k", "-movflags", "+faststart"]);
        }
        args.AddRange(["-map_metadata", "-1", "-map_metadata:s", "-1", "-map_chapters", "-1", "-threads", "2", output.Temporary]);
        await LocalProcess.RunAsync(Ffmpeg, args, token).ConfigureAwait(false);
        var result = await Probe(output.Temporary, token).ConfigureAwait(false);
        if (new FileInfo(output.Temporary).Length == 0 || (audioOnly ? !result.Audio : result.Width == 0)) throw new InvalidDataException("Output stream verification failed.");
        if (target == "MUTE" && result.Audio) throw new InvalidDataException("Audio removal verification failed.");
        if (target == "COMPRESS" && new FileInfo(output.Temporary).Length >= file.Size)
            throw new InvalidDataException("This file is already compact; recompression did not reduce its size. The original was preserved.");
        if (source.Duration > 0 && result.Duration > 0 && Math.Abs(source.Duration - result.Duration) > Math.Max(1.5, source.Duration * .02)) throw new InvalidDataException("The output duration did not match the source.");
        // Decode every output frame/sample before publication, not just its header.
        await LocalProcess.RunAsync(Ffmpeg, ["-hide_banner", "-loglevel", "error", "-nostdin", "-xerror", "-protocol_whitelist", "file,pipe", "-threads", "2", "-i", output.Temporary, "-map", "0:v?", "-map", "0:a?", "-f", "null", "-"], token).ConfigureAwait(false);
        var details = await MediaProbe.ReadAsync(Ffprobe, output.Temporary, token).ConfigureAwait(false);
        ValidateFormat(details, destination, audioOnly);
        var bytes = new FileInfo(output.Temporary).Length;
        return new(output.Publish(token), result.Width, result.Height, Report: tool ? new(file.Size, bytes, $"{file.Size:N0} B → {bytes:N0} B", target is "COMPRESS" or "ROTATE") : null);
    }
    internal static string AudioBitrate(MediaQuality quality) => quality switch { MediaQuality.High => "160k", MediaQuality.Small => "64k", _ => "96k" };
    internal static void ValidateFormat(MediaDetails info, string destination, bool audioOnly)
    {
        var valid = destination switch
        {
            "MP4" or "MOV" or "M4A" => info.Container.Split(',').Contains("mov"), "WEBM" or "MKV" => info.Container.Contains("matroska"),
            "GIF" => info.Container == "gif", "MP3" => info.Container == "mp3", "WAV" => info.Container == "wav", "FLAC" => info.Container == "flac", "AIFF" => info.Container == "aiff", "OPUS" => info.Container == "ogg", "AVI" => info.Container == "avi", _ => false
        };
        var expectedAudio = destination switch { "MP3" => "mp3", "M4A" => "aac", "WAV" => "pcm_s16le", "FLAC" => "flac", "AIFF" => "pcm_s16be", "OPUS" => "opus", _ => "" };
        if (!valid || audioOnly && (!info.HasAudio || info.HasVideo || info.AudioCodec != expectedAudio || info.Channels < 1 || info.SampleRate < 8000)) throw new InvalidDataException("The encoded container/codec does not match the requested format.");
    }
    private static string ToolFormat(DraggedFile file, string operation)
    {
        if (operation is "STRIP" or "MUTE") return file.Extension;
        if (file.Category == FileCategory.Video) return file.Extension is "MP4" or "MOV" or "WEBM" ? file.Extension : "MP4";
        return file.Extension is "MP3" or "M4A" or "OPUS" ? file.Extension : "FLAC";
    }
    private static string Demuxer(string extension) => extension switch
    {
        "MP4" or "MOV" or "M4A" => "mov", "MKV" or "WEBM" => "matroska", "AVI" => "avi", "MP3" => "mp3", "WAV" => "wav", "FLAC" => "flac", "AIFF" => "aiff", "OPUS" => "ogg", _ => throw new NotSupportedException()
    };
    private async Task<MediaInfo> Probe(string path, CancellationToken token, string? demuxer = null)
    {
        var args = new List<string> { "-v", "error", "-protocol_whitelist", "file,pipe", "-show_entries", "stream=codec_type,width,height:format=duration", "-of", "json" };
        if (demuxer is not null) args.AddRange(["-f", demuxer]); args.Add(path);
        using var json = JsonDocument.Parse(await LocalProcess.RunAsync(Ffprobe, args, token, true).ConfigureAwait(false));
        var audio = false; var width = 0; var height = 0; var duration = 0d;
        foreach (var stream in json.RootElement.GetProperty("streams").EnumerateArray())
        {
            if (stream.GetProperty("codec_type").GetString() == "audio") audio = true;
            if (stream.GetProperty("codec_type").GetString() == "video" && width == 0)
            { width = stream.GetProperty("width").GetInt32(); height = stream.GetProperty("height").GetInt32(); }
        }
        if (json.RootElement.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var value))
            double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
        return new(audio, width, height, duration);
    }
    private sealed record MediaInfo(bool Audio, int Width, int Height, double Duration);
}
