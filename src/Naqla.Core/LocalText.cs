using System.Globalization;
using System.Text;

namespace Naqla.Core;

public sealed record OcrResult(string Text, float Confidence, int Pages, string Language)
{
    public bool HasText => !string.IsNullOrWhiteSpace(Text);
}
public sealed record SubtitleCue(double Start, double End, string Text);
public sealed record SubtitleDocument(IReadOnlyList<SubtitleCue> Cues, double Duration, string Language, IReadOnlyList<string>? Notes = null)
{
    public void Validate()
    {
        if (!double.IsFinite(Duration) || Duration <= 0 || Cues.Count is < 1 or > 100000) throw new ArgumentException("No usable subtitle segments were produced.");
        var previous = 0d;
        foreach (var cue in Cues)
        {
            if (!double.IsFinite(cue.Start) || !double.IsFinite(cue.End) || cue.Start < previous - .00001 || cue.End <= cue.Start || cue.End > Duration + .05 || string.IsNullOrWhiteSpace(cue.Text) || cue.Text.Contains("-->") || cue.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Any(string.IsNullOrWhiteSpace) || cue.Text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
                throw new ArgumentException("Subtitle timestamps/text are invalid or overlap.");
            previous = cue.End;
        }
    }
    private static string Timestamp(double value, char separator)
    {
        var milliseconds = (long)Math.Round(value * 1000, MidpointRounding.AwayFromZero);
        return $"{milliseconds / 3600000:00}:{milliseconds / 60000 % 60:00}:{milliseconds / 1000 % 60:00}{separator}{milliseconds % 1000:000}";
    }
    public string Encode(bool webVtt)
    {
        Validate(); var text = new StringBuilder(webVtt ? "WEBVTT\n\n" : "");
        for (var i = 0; i < Cues.Count; i++)
        {
            var cue = Cues[i];
            if ((long)Math.Round(cue.End * 1000) <= (long)Math.Round(cue.Start * 1000)) throw new ArgumentException("Subtitle segment is too short for millisecond timing.");
            text.Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append('\n').Append(Timestamp(cue.Start, webVtt ? '.' : ',')).Append(" --> ").Append(Timestamp(cue.End, webVtt ? '.' : ',')).Append('\n');
            var caption = cue.Text.Replace("\r", "").Trim();
            if (webVtt) caption = caption.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            text.Append(caption).Append("\n\n");
        }
        return text.ToString();
    }
}
