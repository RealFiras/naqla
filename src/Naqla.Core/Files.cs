namespace Naqla.Core;

public enum FileCategory { Unsupported, Image, Video, Audio, Document, Archive, Folder }
public enum WheelMode { Convert, Tools }
public sealed record DraggedFile(string FullPath, string Extension, long Size, FileCategory Category, DateTime? LastWriteUtc = null, bool SizeUnknown = false)
{
    public string DisplaySize => SizeUnknown ? "غير محسوب" : Size < 1024 ? $"{Size} B" : Size < 1048576 ? $"{Size / 1024d:0.#} KB" : Size < 1073741824 ? $"{Size / 1048576d:0.#} MB" : $"{Size / 1073741824d:0.#} GB";
}

public static class FileClassifier
{
    public static FileCategory Category(string extension) => extension.TrimStart('.').ToLowerInvariant() switch
    {
        "png" or "jpg" or "jpeg" or "webp" or "avif" or "heic" or "tiff" or "bmp" => FileCategory.Image,
        "mp4" or "mov" or "mkv" or "webm" or "avi" => FileCategory.Video,
        "mp3" or "wav" or "flac" or "m4a" or "aiff" or "opus" => FileCategory.Audio,
        "pdf" or "docx" or "txt" or "rtf" or "html" or "odt" or "xlsx" or "pptx" => FileCategory.Document,
        "zip" or "7z" or "tar" => FileCategory.Archive,
        _ => FileCategory.Unsupported
    };

    public static DraggedFile Inspect(string path)
    {
        var file = new FileInfo(path);
        if (file.FullName.StartsWith(@"\\", StringComparison.Ordinal))
            throw new NotSupportedException("Network paths are outside this prototype's local-file scope.");
        var directory = new DirectoryInfo(file.FullName);
        if (directory.Exists)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new NotSupportedException("Linked folders and remote/cloud placeholders are not supported.");
            return new(directory.FullName, "FOLDER", 0, FileCategory.Folder, directory.LastWriteTimeUtc, true);
        }
        if (!file.Exists || (file.Attributes & FileAttributes.Directory) != 0)
            throw new IOException("A regular local file is required.");
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0) throw new NotSupportedException("Linked files and cloud placeholders must be available as regular local files first.");
        var extension = file.Extension.TrimStart('.').ToUpperInvariant();
        return new(file.FullName, extension, file.Length, Category(extension), file.LastWriteTimeUtc);
    }
}

public sealed record WheelOption(string Id, string Label, bool Enabled);
public static class WheelOptions
{
    /// <summary>Intersect actual backend capabilities and per-source destination rules; never skip a member.</summary>
    public static IReadOnlyList<WheelOption> For(FileDragPayload payload, WheelMode mode, Func<DraggedFile, string, bool> canExecute)
    {
        if (!payload.IsValid) return [];
        var perFile = payload.Files.Select(file => For(file, mode, canExecute)).ToArray();
        var common = perFile[0].Where(option => option.Id != "MERGE" && (payload.Files.Count == 1 || !PdfToolIds.TryParse(option.Id, out _)) && perFile.All(list => list.Any(x => x.Id == option.Id && x.Enabled))).ToList();
        if (mode == WheelMode.Tools && payload.Files.Count > 1 && payload.Files.All(x => x.Category == FileCategory.Image && canExecute(x, "MERGE")))
            common.Add(new("MERGE", "دمج PDF", true));
        if (mode == WheelMode.Tools && payload.Files.Count is > 1 and <= 20 && payload.Files.All(x => x.Extension == "PDF" && canExecute(x, "PDFMERGE")))
            common.Add(new("PDFMERGE", "دمج PDF", true));
        return common;
    }

    public static IReadOnlyList<WheelOption> For(DraggedFile file, WheelMode mode, Func<DraggedFile, string, bool>? canExecute = null)
    {
        if (mode == WheelMode.Tools)
        {
            (string Id, string Label)[] tools = file.Category switch
            {
                FileCategory.Image => [("COMPRESS", "ضغط"), ("TARGETSIZE", "حجم مستهدف"), ("CROP", "قص"), ("ROTATE", "تدوير"), ("STRIP", "بدون بيانات"), ("MERGE", "دمج PDF")],
                FileCategory.Video => [("COMPRESS", "ضغط"), ("ROTATE", "تدوير"), ("MUTE", "بدون صوت"), ("STRIP", "بدون بيانات")],
                FileCategory.Audio => [("COMPRESS", "ضغط"), ("STRIP", "بدون بيانات")],
                FileCategory.Document when file.Extension == "PDF" => [("TXT", "استخراج نص"), ("PDFREORDER", "ترتيب"), ("PDFEXTRACT", "استخراج"), ("PDFDELETE", "حذف"), ("PDFNUMBERS", "ترقيم"), ("PDFWATERMARK", "علامة مائية"), ("PDFROTATE", "تدوير")],
                _ => []
            };
            tools = [.. tools, ("RENAME", "تسمية")];
            return tools.Where(x => canExecute is null || canExecute(file, x.Id))
                .Select(x => new WheelOption(x.Id, x.Label, canExecute?.Invoke(file, x.Id) == true)).ToArray();
        }
        string[] formats = file.Category switch
        {
            FileCategory.Image => ["JPG", "WEBP", "AVIF", "TIFF", "HEIC", "PDF", "PNG"],
            FileCategory.Video => ["MP4", "MOV", "M4A", "MP3", "GIF", "WEBM"],
            FileCategory.Audio => ["MP3", "M4A", "WAV", "FLAC", "AIFF", "OPUS"],
            FileCategory.Document when file.Extension == "PDF" => ["PNG", "JPG", "TXT", "DOCX", "RTF"],
            FileCategory.Document when file.Extension is "DOCX" or "TXT" or "RTF" or "HTML" or "ODT" => ["DOCX", "PDF", "TXT", "RTF", "HTML", "ODT"],
            _ => []
        };
        var source = file.Extension == "JPEG" ? "JPG" : file.Extension;
        return formats.Where(x => x != source && (canExecute is null || canExecute(file, x)))
            .Select(x => new WheelOption(x, x == "WEBM" ? "WebM" : x, canExecute?.Invoke(file, x) ?? (file.Extension == "PNG" && x == "JPG"))).ToArray();
    }
}

public sealed record ConversionResult(string OutputPath, int Width, int Height, int Items = 1, IReadOnlyList<string>? OutputPaths = null, RenameReceipt? Rename = null, ImageOptimizationReport? Optimization = null, ProcessingReport? Report = null, PrivacyReport? Privacy = null);
public interface IConversionService
{
    bool CanExecute(DraggedFile file, string target);
    bool CanExecute(FileDragPayload payload, string target);
    Task<ConversionResult> ConvertAsync(DraggedFile file, string target, CancellationToken cancellationToken = default);
    Task<ConversionResult> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken = default);
}
public interface IFileConverter
{
    Task<ConversionResult> ConvertAsync(DraggedFile file, CancellationToken cancellationToken = default);
}

// UI-independent geometry: first sector centered at twelve o'clock.
public static class RadialHitTest
{
    public static int Sector(double x, double y, double innerRadius, double outerRadius, int count)
    {
        var radius = Math.Sqrt(x * x + y * y);
        if (count <= 0 || radius < innerRadius || radius > outerRadius) return -1;
        var degrees = (Math.Atan2(y, x) * 180 / Math.PI + 90 + 180d / count + 360) % 360;
        return (int)(degrees / (360d / count));
    }
}
