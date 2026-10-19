using System.IO;
using ImageMagick;
using Naqla.Core;

namespace Naqla.Conversion;

/// <summary>Offline capability/parameter validation and dispatch, independent of wheel rendering.</summary>
public sealed class LocalConversionService : IConversionService
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Path, long Bytes, DateTime? Stamp), MediaDetails> mediaCache = new();
    private readonly SemaphoreSlim probeQueue = new(1);
    public PdfToolkitService PdfToolkit { get; } = new();
    public AdvancedMediaService AdvancedMedia { get; }
    public DocumentService Documents { get; }
    public LocalModelService Models { get; }
    public PrivacyCleanService Privacy { get; }
    public PluginService Plugins { get; }
    public ArchiveService Archives { get; }
    private IReadOnlyList<(string Id, string Label, string[] Extensions)> pluginActions = [];
    private bool ocrAvailable, backgroundAvailable, speechAvailable, archive7zAvailable, pdfCleanAvailable;
    private readonly MediaConverter media;
    private readonly HeicEncoder heic;
    private readonly ImageConversions images;
    private readonly TargetSizeOptimizer optimizer;
    private readonly bool mediaAvailable, heicAvailable;
    private bool documentsAvailable;
    private readonly string componentsRoot;
    private readonly ISmartRenameService? renamer;
    public LocalConversionService(string? engineDirectory = null, string? heifDirectory = null, ISmartRenameService? renamer = null, string? officeDirectory = null, string? componentDirectory = null, string? pluginDirectory = null)
    {
        media = new(engineDirectory ?? Path.Combine(AppContext.BaseDirectory, "media-engine"));
        AdvancedMedia = new(engineDirectory ?? Path.Combine(AppContext.BaseDirectory, "media-engine"));
        var components = componentDirectory ?? ComponentStore.DefaultRoot;
        Documents = new(officeDirectory ?? Path.Combine(components, "office"));
        componentsRoot = components;
        Models = new(components, engineDirectory ?? Path.Combine(AppContext.BaseDirectory, "media-engine"));
        Privacy = new(engineDirectory ?? Path.Combine(AppContext.BaseDirectory, "media-engine"), Path.Combine(components, "qpdf-engine"));
        Archives = new(Path.Combine(components, "sevenzip", "x64"));
        Plugins = new(pluginDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Naqla", "Plugins"));
        pdfCleanAvailable = File.Exists(Path.Combine(components, "qpdf-engine", "qpdf.exe"));
        heic = new(heifDirectory ?? Path.Combine(AppContext.BaseDirectory, "heif-engine"));
        images = new(heic);
        optimizer = new(heic);
        mediaAvailable = media.Available; heicAvailable = heic.Available;
        this.renamer = renamer;
        RefreshExtensions();
    }
    public void RefreshExtensions()
    {
        ocrAvailable = Models.HasOcr; backgroundAvailable = Models.HasBackgroundModel; speechAvailable = Models.HasSpeech; archive7zAvailable = Archives.SevenZipAvailable;
        documentsAvailable = Documents.Available;
        pdfCleanAvailable = File.Exists(Path.Combine(componentsRoot, "qpdf-engine", "qpdf.exe"));
        pluginActions = Plugins.List().Where(x => x.Enabled).SelectMany(x => x.Manifest.Actions.Select(a => ("PLUGIN:" + x.Manifest.Id + ":" + a.Id, a.Label, a.Extensions))).ToArray();
    }
    public bool CanExecute(DraggedFile file, string target) => target.StartsWith("PLUGIN:", StringComparison.Ordinal) ? pluginActions.Any(x => x.Id == target && x.Extensions.Contains(file.Extension)) : target switch
    {
        "ARCHIVE" => file.Category != FileCategory.Unsupported,
        "EXTRACT" => file.Category == FileCategory.Archive,
        "BACKGROUND" => backgroundAvailable && file.Category == FileCategory.Image,
        "OCR" => ocrAvailable && (file.Category == FileCategory.Image || file.Extension == "PDF"),
        "SUBTITLE" => speechAvailable && mediaAvailable && file.Category is FileCategory.Video or FileCategory.Audio,
        "PRIVACY" => file.Category == FileCategory.Image || file.Extension is "DOCX" or "XLSX" or "PPTX" || file.Extension == "PDF" && pdfCleanAvailable || mediaAvailable && file.Category is FileCategory.Video or FileCategory.Audio,
        "CHANNELS" => mediaAvailable && file.Category == FileCategory.Audio,
        _ => CanExecuteBasic(file, target)
    };
    private bool CanExecuteBasic(DraggedFile file, string target) => target == "RENAME" ? renamer is not null && file.Category is not (FileCategory.Unsupported or FileCategory.Folder) : PdfToolIds.TryParse(target, out _) ? file.Extension == "PDF" && file.Category == FileCategory.Document : file.Category switch
    {
        FileCategory.Image => target is "PNG" or "JPG" or "WEBP" or "AVIF" or "TIFF" or "PDF" or "MERGE" or "TARGETSIZE" ||
            (target == "HEIC" && heicAvailable) ||
            (target is "CROP" or "ROTATE" or "STRIP" or "COMPRESS" && (file.Extension != "HEIC" || heicAvailable)),
        FileCategory.Video => mediaAvailable && target is "MP4" or "MOV" or "M4A" or "MP3" or "GIF" or "WEBM" or "COMPRESS" or "ROTATE" or "STRIP" or "MUTE" or "TRIM" or "FRAMES",
        FileCategory.Audio => mediaAvailable && target is "MP3" or "M4A" or "WAV" or "FLAC" or "AIFF" or "OPUS" or "COMPRESS" or "STRIP" or "TRIM" or "NORMALIZE" or "SILENCE" or "MONO" or "STEREO",
        FileCategory.Document => file.Extension == "PDF" && target is "PNG" or "JPG" or "TXT" or "DOCX" or "RTF" || documentsAvailable && file.Extension is "DOCX" or "TXT" or "RTF" or "HTML" or "ODT" && target is "DOCX" or "PDF" or "TXT" or "RTF" or "HTML" or "ODT" && target != file.Extension,
        _ => false
    };
    public IReadOnlyList<WheelOption> ContextOptions(FileDragPayload payload, WheelMode mode)
    {
        var options = WheelOptions.For(payload, mode, CanOffer).ToList();
        if (!payload.IsValid || mode != WheelMode.Tools) return options;
        (string Id, string Label, bool Single)[] extra = [("TRIM", "قص زمني", false), ("FRAMES", "إطارات", false), ("NORMALIZE", "تطبيع الصوت", false), ("SILENCE", "قص الصمت", false), ("CHANNELS", "القنوات", false), ("BACKGROUND", "إزالة الخلفية", true), ("OCR", "استخراج نص", true), ("SUBTITLE", "ترجمة نصية", true), ("PRIVACY", "خصوصية", false), ("ARCHIVE", "أرشفة", false), ("EXTRACT", "فك الأرشيف", false)];
        foreach (var action in extra.Where(x => !x.Single || payload.Files.Count == 1)) if (payload.Files.All(x => CanOffer(x, action.Id))) options.Add(new(action.Id, action.Label, true));
        foreach (var action in pluginActions) if (payload.Files.All(x => CanOffer(x, action.Id))) options.Add(new(action.Id, action.Label, true));
        return options.GroupBy(x => x.Id).Select(x => x.First()).ToArray();
    }
    public bool CanExecute(FileDragPayload payload, string target) => payload.IsValid &&
        (target is "BACKGROUND" or "OCR" or "SUBTITLE" ? payload.Files.Count == 1 && payload.Files.All(x => CanExecute(x, target)) :
        (target == "MERGE" ? payload.Files.Count > 1 && payload.Files.All(x => x.Category == FileCategory.Image) : PdfToolIds.TryParse(target, out var pdfTool) ?
            payload.Files.Count <= 20 && (pdfTool == PdfTool.Merge ? payload.Files.Count > 1 : payload.Files.Count == 1) && payload.Files.All(x => CanExecute(x, target)) : payload.Files.All(x => CanExecute(x, target))));

    // Synchronous UI queries never start a decoder or touch the filesystem.
    public bool CanOffer(DraggedFile file, string target)
    {
        if (!CanExecute(file, target)) return false;
        if (file.Category == FileCategory.Video && target is "MP3" or "M4A" or "MUTE" or "SUBTITLE")
            return mediaCache.TryGetValue((file.FullPath, file.Size, file.LastWriteUtc), out var info) && info.HasAudio;
        return true;
    }
    public async Task ProbeCapabilitiesAsync(IReadOnlyList<DraggedFile> files, CancellationToken token = default)
    {
        await probeQueue.WaitAsync(token).ConfigureAwait(false);
        try
        {
        foreach (var file in files.Where(x => x.Category is FileCategory.Video or FileCategory.Audio))
        {
            token.ThrowIfCancellationRequested();
            var key = (file.FullPath, file.Size, file.LastWriteUtc);
            if (mediaCache.ContainsKey(key)) continue;
            try
            {
                var info = await AdvancedMedia.InspectAsync(file, token).ConfigureAwait(false);
                if (mediaCache.Count >= 256) mediaCache.Clear();
                mediaCache[key] = info;
            }
            catch (Exception error) when (error is IOException or NotSupportedException or TimeoutException) { /* Unknown audio actions remain unavailable. */ }
        }
        }
        finally { probeQueue.Release(); }
    }

    public Task<ImagePreview> GetImagePreviewAsync(DraggedFile file, CancellationToken token = default) =>
        Task.Run(() => RasterImages.Preview(FileClassifier.Inspect(file.FullPath), token), token);

    public Task<ImageOptimizationSource> GetOptimizationSourceAsync(DraggedFile file, CancellationToken token = default) =>
        Task.Run(() => optimizer.Source(FileClassifier.Inspect(file.FullPath), token), token);
    public Task<ImageOptimizationSource> GetOptimizationSourceAsync(IReadOnlyList<DraggedFile> files, CancellationToken token = default)
    {
        var snapshot = files.ToArray();
        return Task.Run(() =>
        {
            if (snapshot.Length is < 1 or > FileDragPayload.MaximumFiles || snapshot.Any(x => x.Category != FileCategory.Image)) throw new ArgumentException("اختر صورًا محلية فقط");
            for (var index = 0; index < snapshot.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                try { return optimizer.Source(FileClassifier.Inspect(snapshot[index].FullPath), token) with { PreviewIndex = index }; }
                catch (Exception error) when (snapshot.Length > 1 && error is IOException or UnauthorizedAccessException or NotSupportedException or MagickException) { /* Preview fallback only; all sources remain in the actual batch. */ }
            }
            throw new InvalidDataException("تعذر قراءة أي صورة للمعاينة؛ لم تُعالج الدفعة");
        }, token);
    }
    public Task<ImageOptimizationPreview> PreviewOptimizationAsync(DraggedFile file, TargetSizeSettings settings, IProgress<ImageOptimizationProgress>? progress = null, CancellationToken token = default) =>
        Task.Run(async () => (await optimizer.RunAsync(FileClassifier.Inspect(file.FullPath), settings, true, progress, token).ConfigureAwait(false)).Preview!, token);

    public Task<ConversionResult> ConvertAsync(DraggedFile file, string target, CancellationToken cancellationToken = default) =>
        ConvertAsync(new ConversionRequest(file, target), cancellationToken);

    public Task<ConversionResult> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Target == "RENAME")
        {
            if (renamer is null || request.Rename is null) throw new ArgumentException("معاينة الأسماء مطلوبة");
            if (!request.Files.Select(x => x.FullPath).SequenceEqual(request.Rename.Entries.Select(x => x.Source.FullPath), StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("اختلفت الملفات عن المعاينة المؤكدة");
            return renamer.ExecuteAsync(request.Rename, request.RenameConfirmed, cancellationToken);
        }
        if (request.Files.Count is < 1 or > FileDragPayload.MaximumFiles) throw new ArgumentException("Choose between 1 and 200 local files.");
        // Snapshot and refresh metadata at execution time. Never serialize source paths to diagnostics.
        var payload = FileDragPayload.Inspect(request.Files.Select(x => x.FullPath).ToArray());
        if (!CanExecute(payload, request.Target)) throw new NotSupportedException("This operation is not available for this selection.");
        if (request.Target == "ARCHIVE")
        {
            var format = request.ArchiveFormat ?? throw new ArgumentException("Choose an archive format.");
            if (format == "7Z" && !archive7zAvailable) throw new NotSupportedException("Install the local 7-Zip component first.");
            return Archives.CreateAsync(payload.Files.Select(x => x.FullPath).ToArray(), format, cancellationToken);
        }
        if (PdfToolIds.TryParse(request.Target, out var tool))
        {
            var plan = request.Pdf ?? throw new ArgumentException("افتح محرر PDF وعاين النتيجة قبل الحفظ");
            if (plan.Tool != tool || !payload.Files.Select(x => x.FullPath).SequenceEqual(plan.Session.Sources.Select(x => x.File.FullPath), StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("اختلفت المستندات أو العملية عن المعاينة");
            return PdfToolkit.ExecuteAsync(plan, token: cancellationToken);
        }
        if (request.Files.Count > 1 && request.Target != "MERGE") throw new ArgumentException("Submit individual batch operations through BatchProcessor.");
        if (request.Target == "CROP" && request.Crop is null) throw new ArgumentException("Choose a crop rectangle before saving.");
        request = request with { Files = payload.Files };
        var file = payload.Files[0];
        if (request.Target.StartsWith("PLUGIN:", StringComparison.Ordinal)) return Plugins.ExecuteAsync(file, request.Target, cancellationToken);
        if (request.Target == "EXTRACT") return Archives.ExtractAsync(file, token: cancellationToken);
        if (request.Target == "PRIVACY")
        {
            var plan = request.Privacy ?? throw new ArgumentException("Review and select metadata categories first.");
            var source = plan.Sources.SingleOrDefault(x => x.Source.FullPath.Equals(file.FullPath, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Source is not in the approved metadata preview.");
            return CleanPrivacyAsync(source, plan.Settings, cancellationToken);
        }
        if (request.Target is "BACKGROUND" or "OCR" or "SUBTITLE") return ExecuteModelAsync(file, request.Target, request.Model ?? throw new ArgumentException("A reviewed preview is required."), cancellationToken);
        if (request.Target == "CHANNELS")
        {
            var settings = request.MediaOptions ?? throw new ArgumentException("Choose an audio channel mode.");
            if (settings.Channels == AudioChannels.Preserve) throw new ArgumentException("Choose mono or dual-channel stereo.");
            return AdvancedMedia.ExecuteAsync(file, settings.Channels == AudioChannels.Mono ? "MONO" : "STEREO", settings, cancellationToken);
        }
        if (request.Target is "TRIM" or "FRAMES" or "NORMALIZE" or "SILENCE" or "MONO" or "STEREO")
            return AdvancedMedia.ExecuteAsync(file, request.Target, request.MediaOptions ?? throw new ArgumentException("Media settings are required."), cancellationToken);
        if (request.Target == "TARGETSIZE")
        {
            var settings = request.Optimization ?? throw new ArgumentException("حدد الحجم المستهدف قبل الحفظ");
            settings.Validate();
            return Task.Run(async () => (await optimizer.RunAsync(file, settings, false, null, cancellationToken).ConfigureAwait(false)).Output!, cancellationToken);
        }
        return file.Category switch
        {
            FileCategory.Video or FileCategory.Audio => media.ConvertAsync(file, request.Target, cancellationToken, request.MediaOptions),
            FileCategory.Document => file.Extension == "PDF" ? Task.Run(() => PdfConversions.Convert(file, request.Target, cancellationToken, request.PdfDpi), cancellationToken) : Documents.ConvertAsync(file, request.Target, cancellationToken),
            _ => Task.Run(() => images.ConvertAsync(request, cancellationToken), cancellationToken)
        };
    }
    private async Task<ConversionResult> CleanPrivacyAsync(PrivacyAnalysis source, PrivacySettings settings, CancellationToken token)
    {
        var result = await Privacy.CleanAsync(source, settings, token).ConfigureAwait(false);
        return result.Output with { Privacy = result.Report };
    }
    private async Task<ConversionResult> ExecuteModelAsync(DraggedFile file, string target, ModelPlan plan, CancellationToken token)
    {
        using var guard = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(guard, token).ConfigureAwait(false));
        if (!hash.Equals(plan.SourceSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The source changed after the reviewed model preview.");
        return target switch
        {
            "BACKGROUND" => await Models.SaveBackgroundPreviewAsync(file, plan.Background ?? throw new ArgumentException("Missing background preview."), token).ConfigureAwait(false),
            "OCR" => await Models.SaveOcrAsync(file, plan.Ocr ?? throw new ArgumentException("Missing OCR preview."), token).ConfigureAwait(false),
            "SUBTITLE" => await Models.SaveSubtitlesAsync(file, plan.Subtitles ?? throw new ArgumentException("Missing subtitle preview."), plan.WebVtt, token).ConfigureAwait(false),
            _ => throw new NotSupportedException()
        };
    }
    internal static MagickFormat ImageFormat(string extension) => extension switch
    {
        "PNG" => MagickFormat.Png, "JPG" or "JPEG" => MagickFormat.Jpeg, "WEBP" => MagickFormat.WebP,
        "AVIF" => MagickFormat.Avif, "TIFF" => MagickFormat.Tiff, "BMP" => MagickFormat.Bmp,
        "HEIC" => MagickFormat.Heic, _ => throw new NotSupportedException("Unsupported image codec.")
    };
}
