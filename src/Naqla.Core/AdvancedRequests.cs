namespace Naqla.Core;

public sealed record ModelPlan(string SourceSha256, OcrResult? Ocr = null, ImagePreview? Background = null, SubtitleDocument? Subtitles = null, bool WebVtt = false);
public sealed record PrivacyPlan(IReadOnlyList<PrivacyAnalysis> Sources, PrivacySettings Settings);
