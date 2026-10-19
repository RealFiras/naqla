namespace Naqla.Core;

public sealed record PrivacySettings(string[] Categories, bool AllowLosslessPngFallback = false)
{
    public static readonly string[] KnownCategories = ["EXIF", "XMP", "IPTC", "Comments", "Author", "Dates", "Description", "Software", "CustomProperties"];
    public static PrivacySettings Recommended => new([.. KnownCategories]);
    public void Validate() { if (Categories is null || Categories.Length == 0 || Categories.Distinct().Count() != Categories.Length || Categories.Any(x => !KnownCategories.Contains(x))) throw new ArgumentException("Select supported metadata categories."); }
    public bool Removes(string category) => Categories.Contains(category);
}
public sealed record PrivacyAnalysis(DraggedFile Source, string Sha256, IReadOnlyList<string> Detected, IReadOnlyList<string> Limits);
public sealed record PrivacyReport(IReadOnlyList<string> Removed, IReadOnlyList<string> Retained, IReadOnlyList<string> Limitations, long OutputBytes, bool Reencoded, string SourceSha256, string OutputSha256);
public sealed record PrivacyResult(ConversionResult Output, PrivacyReport Report);
