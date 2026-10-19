namespace Naqla.Core;

public sealed record CropRectangle(int X, int Y, int Width, int Height)
{
    public void Validate(int imageWidth, int imageHeight)
    {
        if (X < 0 || Y < 0 || Width < 1 || Height < 1 || (long)X + Width > imageWidth || (long)Y + Height > imageHeight)
            throw new ArgumentOutOfRangeException(nameof(CropRectangle), "The crop rectangle must stay inside the oriented source image.");
    }
}

public sealed record ConversionRequest(IReadOnlyList<DraggedFile> Files, string Target, CropRectangle? Crop = null, RenamePlan? Rename = null, bool RenameConfirmed = false, TargetSizeSettings? Optimization = null, PdfEditPlan? Pdf = null, ImageToolOptions? ImageOptions = null, MediaToolOptions? MediaOptions = null, int PdfDpi = 144, ModelPlan? Model = null, PrivacyPlan? Privacy = null, string? ArchiveFormat = null)
{
    public ConversionRequest(DraggedFile file, string target, CropRectangle? crop = null) : this(new[] { file }, target, crop) { }
}

public sealed record ImagePreview(byte[] Png, int Width, int Height);
public enum SourceValidation { Valid, UnsupportedExtension, Unreadable }
public sealed record FileInspection(DraggedFile File, SourceValidation Validation);

/// <summary>Immutable metadata from a real OLE file list, preserving the source-provided order.</summary>
public sealed class FileDragPayload
{
    public const int MaximumFiles = 200;
    public IReadOnlyList<DraggedFile> Files { get; }
    public IReadOnlyList<FileInspection> Inspections { get; }
    public IReadOnlyList<FileCategory> Categories { get; }
    public bool IsValid => Inspections.All(x => x.Validation == SourceValidation.Valid);
    public DraggedFile PreviewFile { get; }
    private FileDragPayload(FileInspection[] inspections)
    {
        Inspections = Array.AsReadOnly(inspections);
        var files = inspections.Select(x => x.File).ToArray();
        Files = Array.AsReadOnly(files);
        Categories = Array.AsReadOnly(files.Select(x => x.Category).Distinct().ToArray());
        PreviewFile = files[0] with { Size = files.Aggregate(0L, (total, file) => checked(total + file.Size)), SizeUnknown = files.Any(x => x.SizeUnknown) };
    }
    public static FileDragPayload Inspect(IReadOnlyList<string> paths)
    {
        if (paths.Count is < 1 or > MaximumFiles) throw new NotSupportedException("Drag between 1 and 200 local files.");
        var normalized = paths.Select(Path.GetFullPath).ToArray();
        if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
            throw new NotSupportedException("The same source file cannot appear twice in one drop.");
        var inspections = normalized.Select(path =>
        {
            try
            {
                var file = FileClassifier.Inspect(path);
                return new FileInspection(file, file.Category == FileCategory.Unsupported ? SourceValidation.UnsupportedExtension : SourceValidation.Valid);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                var extension = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
                return new FileInspection(new(path, extension, 0, FileClassifier.Category(extension)), SourceValidation.Unreadable);
            }
        }).ToArray();
        return new(inspections);
    }
    public bool Matches(IReadOnlyList<string> paths) => paths.Count == Files.Count &&
        paths.Select(Path.GetFullPath).SequenceEqual(Files.Select(x => x.FullPath), StringComparer.OrdinalIgnoreCase);
}
