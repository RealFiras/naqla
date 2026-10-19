using System.Globalization;
using System.Text.Json;
using Naqla.Core;

namespace Naqla.Conversion;

internal static class MediaProbe
{
    public static async Task<MediaDetails> ReadAsync(string executable, string path, CancellationToken token, string? demuxer = null)
    {
        var args = new List<string> { "-v", "error", "-protocol_whitelist", "file,pipe", "-show_entries", "stream=codec_type,codec_name,width,height,avg_frame_rate,sample_rate,channels:stream_side_data=rotation:format=format_name,duration", "-of", "json" };
        if (demuxer is not null) args.AddRange(["-f", demuxer]); args.Add(path);
        var response = await LocalProcess.RunCapturedAsync(executable, args, token, timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        using var json = JsonDocument.Parse(response.Output);
        var audio = false; var video = false; var width = 0; var height = 0; var rotation = 0; var channels = 0; var sampleRate = 0; var duration = 0d; var fps = 0d; var videoCodec = ""; var audioCodec = ""; var container = "";
        foreach (var stream in json.RootElement.GetProperty("streams").EnumerateArray())
        {
            var type = stream.GetProperty("codec_type").GetString();
            if (type == "audio" && !audio)
            {
                audio = true; audioCodec = Text(stream, "codec_name"); channels = Number(stream, "channels"); int.TryParse(Text(stream, "sample_rate"), CultureInfo.InvariantCulture, out sampleRate);
            }
            if (type == "video" && !video && Number(stream, "width") > 0)
            {
                video = true; videoCodec = Text(stream, "codec_name"); width = Number(stream, "width"); height = Number(stream, "height"); fps = Fraction(Text(stream, "avg_frame_rate"));
                if (stream.TryGetProperty("side_data_list", out var sides)) foreach (var side in sides.EnumerateArray()) if (side.TryGetProperty("rotation", out var r) && r.TryGetDouble(out var degrees)) rotation = (int)Math.Round(degrees);
            }
        }
        if (json.RootElement.TryGetProperty("format", out var format)) { container = Text(format, "format_name"); double.TryParse(Text(format, "duration"), NumberStyles.Float, CultureInfo.InvariantCulture, out duration); }
        if (!double.IsFinite(duration) || duration < 0 || width > 30000 || height > 30000 || (long)width * height > 40_000_000 || duration > 24 * 3600)
            throw new NotSupportedException("الوسائط تتجاوز حدود المعالجة المحلية: 40 MP أو 24 ساعة");
        return new(audio, video, container, videoCodec, audioCodec, duration, width, height, fps, channels, sampleRate, rotation);
    }
    private static string Text(JsonElement element, string key) => element.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
    private static int Number(JsonElement element, string key) => element.TryGetProperty(key, out var value) && value.TryGetInt32(out var n) ? n : 0;
    private static double Fraction(string value) { var parts = value.Split('/'); return parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && b > 0 ? a / b : 0; }
}
