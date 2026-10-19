namespace Naqla.Core;

public sealed record ImageToolOptions(int Quality = 78, int Rotation = 90)
{
    public void Validate()
    {
        if (Quality is < 1 or > 100 || Rotation is not (90 or -90 or 180 or 270)) throw new ArgumentException("تحقق من جودة الصورة وزاوية الدوران");
    }
}
public enum MediaQuality { High, Balanced, Small }
public enum AudioChannels { Preserve, Mono, DualMonoStereo }
public sealed record TrimRange(double Start, double End, bool PreferStreamCopy = false)
{
    public void Validate(double duration)
    {
        if (!double.IsFinite(Start) || !double.IsFinite(End) || Start < 0 || End <= Start || End > duration + .01 || End - Start < .05)
            throw new ArgumentException("حدد بداية ونهاية صحيحتين داخل مدة الملف");
    }
}
public sealed record LoudnessSettings(double Integrated = -16, double TruePeak = -1.5, double Range = 11)
{
    public void Validate()
    {
        if (!double.IsFinite(Integrated) || Integrated is < -40 or > -5 || !double.IsFinite(TruePeak) || TruePeak is < -9 or > 0 || !double.IsFinite(Range) || Range is < 1 or > 20)
            throw new ArgumentException("تحقق من إعدادات شدة الصوت وذروة الإشارة");
    }
}
public sealed record SilenceSettings(double ThresholdDb = -40, double MinimumSeconds = .15, bool Leading = true, bool Trailing = true)
{
    public void Validate()
    {
        if (!double.IsFinite(ThresholdDb) || ThresholdDb is < -80 or > -10 || !double.IsFinite(MinimumSeconds) || MinimumSeconds is < .02 or > 5 || (!Leading && !Trailing))
            throw new ArgumentException("تحقق من عتبة الصمت وأطراف القص");
    }
}
public enum FrameSelection { Single, Every, Count }
public sealed record FrameSettings(FrameSelection Selection = FrameSelection.Single, double Start = 0, double? End = null, double EverySeconds = 5, int Count = 6, string Format = "PNG")
{
    public double[] Timestamps(double duration)
    {
        var end = End ?? duration;
        if (!Enum.IsDefined(Selection) || !double.IsFinite(Start) || !double.IsFinite(end) || Start < 0 || Start >= duration || end <= Start || end > duration + .01 || Format is not ("PNG" or "JPG")) throw new ArgumentException("تحقق من زمن استخراج الإطارات وصيغتها");
        if (Selection == FrameSelection.Single) return [Start];
        if (Selection == FrameSelection.Count)
        {
            if (Count is < 1 or > 300) throw new ArgumentException("اختر من 1 إلى 300 إطار");
            return Enumerable.Range(0, Count).Select(i => Start + i * (end - Start) / Count).ToArray();
        }
        if (!double.IsFinite(EverySeconds) || EverySeconds < .1 || Math.Ceiling((end - Start) / EverySeconds) > 300) throw new ArgumentException("الفاصل 0.1 ثانية على الأقل والحد 300 إطار");
        return Enumerable.Range(0, (int)Math.Ceiling((end - Start) / EverySeconds)).Select(i => Start + i * EverySeconds).Where(x => x < end).ToArray();
    }
}
public sealed record MediaToolOptions(MediaQuality Quality = MediaQuality.Balanced, int Rotation = 90, TrimRange? Trim = null, FrameSettings? Frames = null, LoudnessSettings? Loudness = null, SilenceSettings? Silence = null, AudioChannels Channels = AudioChannels.Preserve)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Quality) || !Enum.IsDefined(Channels) || Rotation is not (90 or -90 or 180 or 270)) throw new ArgumentException("إعدادات الوسائط غير صحيحة");
        Loudness?.Validate(); Silence?.Validate();
    }
}
public sealed record MediaDetails(bool HasAudio, bool HasVideo, string Container, string VideoCodec, string AudioCodec, double Duration, int Width, int Height, double FrameRate, int Channels, int SampleRate, int Rotation)
{
    public int DisplayWidth => Math.Abs(Rotation) % 180 == 90 ? Height : Width;
    public int DisplayHeight => Math.Abs(Rotation) % 180 == 90 ? Width : Height;
}
public sealed record ProcessingReport(long SourceBytes, long ResultBytes, string Description, bool Reencoded = false, double? MeasuredLoudness = null, double? MeasuredTruePeak = null);
