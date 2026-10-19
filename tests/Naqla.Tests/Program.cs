using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Naqla.Conversion;
using Naqla.Core;
using Naqla.Windows;

// These are real filesystem/codec tests, NOT Explorer drag tests.
internal static partial class Program
{
    private static readonly List<object> Results = [];
    private static string folder = "";
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--internal-ocr") return OcrWorker.Run(args);
        if (args.FirstOrDefault() == "--test-job-child") { Thread.Sleep(30000); return 0; }
        if (args.FirstOrDefault() == "--test-job-parent")
        {
            Thread.Sleep(200);
            using var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, "--test-job-child") { UseShellExecute = false, CreateNoWindow = true })!;
            File.WriteAllText(args[1], child.Id.ToString()); Thread.Sleep(200); return 0;
        }
        // Renderer tests share the real tray app's explicit-shutdown lifecycle; closing
        // an offscreen test editor must not shut down WPF before the next test.
        _ = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        folder = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("artifacts", "test-results"));
        Directory.CreateDirectory(folder);
        try
        {
            if (args.Contains("--installer-only")) RunInstallerDownloadTests();
            else if (args.Contains("--public-packs-only")) Run("Five public packs without proprietary CRT install and execute actual OCR/background/speech/archive/privacy engines", PublicComponentPackAcceptance);
            else if (args.Contains("--stress-only")) { Run("Real 10/50/200-image repeated batch stress preserves hashes, output decodes and all job counts", BatchStress); Run("Actual GPS/XMP/IPTC privacy fixtures and explicitly approved AVIF lossless-PNG fallback", PrivacyMetadataCoverage); }
            else if (args.Contains("--component-packs-only")) { Run("All six actual offline packs install by pinned catalog and execute their real engines", ComponentPackAcceptance); }
            else if (args.Contains("--ocr-only")) { Run("Actual English/Arabic/mixed OCR performance workload", LocalOcr); }
            else if (args.Contains("--background-only")) { Run("Actual local segmentation performance workload", LocalSegmentation); }
            else if (args.Contains("--speech-only")) { Run("Actual English/Arabic transcription performance workload", LocalSpeech); }
            else if (args.Contains("--integration-only")) { RunIntegrationTests(); }
            else if (args.Contains("--documents-only")) { RunDocumentTests(); }
            else if (args.Contains("--matrix-only")) { Run("Fresh actual-handler image/video/audio/PDF source-destination matrix", FreshConversionMatrix); }
            else if (args.Contains("--privacy-only")) { RunPrivacyTests(); }
            else if (args.Contains("--plugins-only")) { RunPluginTests(); }
            else if (args.Contains("--archives-only")) { RunArchiveTests(); }
            else if (args.Contains("--models-only")) { RunModelTests(); }
            else if (args.Contains("--advanced-only")) { RunAdvancedTests(); }
            else
            {
            if (!args.Contains("--pdf-only"))
            {
            if (!args.Contains("--optimizer-only"))
            {
            if (!args.Contains("--rename-only"))
            {
            Run("Requested extension classification and source aliases", Classifications);
            Run("Real Arabic file metadata, size, and location", Metadata);
            Run("Radial sector boundaries and hub exclusion", Geometry);
            Run("Source omission and engine capabilities gate every visible action", Options);
            Run("PNG→JPG decode, white alpha matte, collision safety, original hash", Conversion);
            Run("Corrupt and disguised PNG failure leaves no output", InvalidImages);
            Run("Cancellation before conversion leaves no output", Cancellation);
            Run("Diagnostic schema excludes private paths and exception text; enable/disable and flush", DiagnosticPrivacy);
            Run("Diagnostic size cap and bounded retention preserve unrelated files", DiagnosticRetention);
            Run("Diagnostic storage failure does not throw into the app", DiagnosticStorageFailure);
            Run("Real image codecs: JPG, PNG, WebP, AVIF, TIFF, HEIC, PDF; source hash and collisions", ImageFormats);
            Run("Image tools rotate pixels, strip metadata, and reduce a JPEG", ImageTools);
            Run("All five MP4 destinations and reverse video formats decode fully", MediaFormats);
            Run("All audio destinations and source audio formats decode fully", AudioFormats);
            Run("Media cancellation, corrupt inputs, and missing audio preserve originals", MediaFailures);
            Run("PDF all-page PNG/JPG and text TXT/DOCX/RTF; scanned text fails honestly", PdfFormats);
            Run("Native pixel wheel renders five palettes with transparent exterior", NativeWheel);
            Run("Ctrl press toggles persist through release; repeat and per-drag reset", CtrlModeTransitions);
            Run("Tools before OLE file confirmation, MP4/audio/PDF modes and stale selection", ToolsWheelModes);
            Run("Real video/audio tools: compression, rotation, mute, metadata and source hashes", MediaTools);
            Run("Crop marquee coordinates, reverse drags, bounds and invalid input", CropGeometry);
            Run("Crop real pixels/alpha/orientation; missing parameters and collision safety", ImageCrop);
            Run("Real multi-image payload order, total size and actionable wheel capabilities", ImageBatch);
            Run("Merge PDF all pages/colors/order, first-source folder, hashes and collisions", MergeImages);
            Run("Merge corruption/cancellation leaves no partial output or temporary files", MergeFailures);
            Run("HEIC genuine HEVC brands, alpha, tiny/odd sizes, Arabic folders and tools", HeicFeatures);
            Run("Lossless PNG/TIFF/BMP compression and metadata removal across image formats", LosslessImageTools);
            Run("Every image input converts to every requested non-source image/PDF destination", ImageConversionMatrix);
            Run("Native crop editor layout and pixel fields render without displaying a window", CropEditorRender);
            Run("In-flight HEIC encoder cancellation removes child/temp output and preserves source", HeicCancellation);
            Run("Batch real selections intersect capabilities, mixed categories, validation and absent backends", BatchCapabilities);
            Run("Batch single/multiple PNGs, Arabic duplicate names, source folders, collisions and real progress", BatchImageConversion);
            Run("Batch mixed image formats and individual PDF outputs preserve originals", BatchMixedImages);
            Run("Batch multiple real videos convert and decode fully", BatchVideoConversion);
            Run("Mixed image/audio shared tools process every actual source", BatchMixedTools);
            Run("Corrupt mid-batch source reports failure and preserves unrelated successes", BatchPartialFailure);
            Run("Actual denied destination reports partial failure without losing writable results", BatchReadOnlyDestination);
            Run("Mid-batch/pre-cancellation preserves successes and never invents completion", BatchCancellation);
            Run("Batch crop reports incompatible pixel bounds without clamping or losing success", BatchCropPartialFailure);
            Run("Queue scheduling bounds concurrency across submissions (explicit test double)", BatchConcurrency);
            Run("Batch hub/progress native render preserves geometry and honest job counts", BatchNativeRender);
            Run("200 real files capability timing versus baseline SetFile (not Shift latency)", BatchCapabilityTiming);
            Run("Manual fixture/verifier scripts validate actual outputs and never fake Explorer acceptance", BatchManualFixtureScripts);
            }
            Run("Smart Rename all transforms/date/regex/padding preserve extension and preview is read-only", RenameTransforms);
            Run("Smart Rename validates reserved/invalid names, collisions, duplicate sources and output paths", RenameValidation);
            Run("Real confirmed rename/undo retain file identity, bytes and MOTW without copying", RenameRealAndUndo);
            Run("Batch swaps, case-only renames and source directories retain every file", RenameBatchAndSwap);
            Run("Stale preview, occupied undo target and modified undo file are refused", RenameStaleAndUndoGuards);
            Run("Locked source and in-flight cancellation do not lose original names/content", RenameLockAndCancellation);
            Run("Injected commit failure rolls back real Windows rename operations", RenameRollback);
            Run("Blocked rollback retains both original data and unrelated file with recovery record", RenameRecoveryCollision);
            Run("Advanced regex is bounded and invalid patterns do not change files", RenameRegexBound);
            Run("Rename uses shared queue and remains in contextual image tools", RenameQueueAndCapabilities);
            Run("Native pixel buttons retain automation names; rename preview needs explicit confirmation", PixelButtonsAndRenameEditor);
            }
            if (!args.Contains("--rename-only"))
            {
                Run("Target-size presets/custom Arabic units, resize and format validation", OptimizationUnits);
                Run("Actual iterative JPEG optimization meets all presets at maximal integer quality", OptimizationJpegQuality);
                Run("All six optimizer encoders decode with alpha or explicit white JPEG matte", OptimizationOutputFormats);
                Run("Every supported image source optimizes and retains original hashes", OptimizationInputFormats);
                Run("Lossless PNG/TIFF recipes retain pixels and report impossible measured previews", OptimizationLossless);
                Run("Optimizer preserves oriented dimensions, bounds aspect ratio, prevents upscale and handles color", OptimizationResizeOrientation);
                Run("Unattainable targets, corrupt/multiframe files and absent settings never publish success", OptimizationImpossibleAndInvalid);
                Run("Unicode optimizer outputs stay beside originals and never replace files or directories", OptimizationNames);
                Run("In-flight/pre-cancelled optimization cleans candidates, locks and preserves source", OptimizationCancellation);
                Run("Target-size batch propagates settings, independent outcomes and actual result reports", OptimizationBatch);
                Run("Target-size batch cancellation retains published successes and honest counts", OptimizationBatchCancellation);
                Run("Native optimizer preview/settings, stale/failed Save gates and 6/7-sector tool renders", OptimizationNativeUi);
                Run("Optimizer diagnostics retain only typed operations and numeric measurements", OptimizationDiagnostics);
                Run("Manual optimizer helpers independently decode outputs/check bytes and never invent Explorer acceptance", OptimizationManualHelpers);
            }
            }
            if (!args.Contains("--optimizer-only") && !args.Contains("--rename-only")) RunPdfToolkitTests();
            RunAdvancedTests();
            if (args.Contains("--all") || args.Contains("--release-acceptance")) { RunIntegrationTests(); RunArchiveTests(); RunPluginTests(); RunPrivacyTests(); RunModelTests(); RunDocumentTests(); Run("Fresh actual-handler image/video/audio/PDF source-destination matrix", FreshConversionMatrix); }
            if (args.Contains("--release-acceptance")) { Run("Real 10/50/200-image repeated batch stress preserves hashes, output decodes and all job counts", BatchStress); Run("Actual GPS/XMP/IPTC privacy fixtures and explicitly approved AVIF lossless-PNG fallback", PrivacyMetadataCoverage); Run("All six actual offline packs install by pinned catalog and execute their real engines", ComponentPackAcceptance); }
            }
        }
        finally
        {
            File.WriteAllText(Path.Combine(folder, "results.json"), JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
        }
        return Results.Any(x => JsonSerializer.Serialize(x).Contains("\"Passed\":false")) ? 1 : 0;
    }
    private static void Run(string name, Action test)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try { test(); Results.Add(new { Test = name, Passed = true, Milliseconds = clock.ElapsedMilliseconds }); Console.WriteLine($"PASS {name}"); }
        catch (Exception e) { Results.Add(new { Test = name, Passed = false, Milliseconds = clock.ElapsedMilliseconds, Error = e.ToString() }); Console.WriteLine($"FAIL {name}: {e.Message}"); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Classifications()
    {
        foreach (var (list, category) in new[] {
            ("PNG JPG JPEG WEBP AVIF HEIC TIFF BMP", FileCategory.Image),
            ("MP4 MOV MKV WebM AVI", FileCategory.Video),
            ("MP3 WAV FLAC M4A AIFF OPUS", FileCategory.Audio),
            ("PDF DOCX TXT RTF", FileCategory.Document) })
            foreach (var extension in list.Split(' ')) Check(FileClassifier.Category("." + extension) == category, extension);
        Check(FileClassifier.Category("exe") == FileCategory.Unsupported, "Unsupported extension");
    }
    private static void Metadata()
    {
        var path = Path.Combine(folder, "ملف عربي للاختبار.txt");
        File.WriteAllText(path, "نقلة — UTF-8");
        var file = FileClassifier.Inspect(path);
        Check(file.FullPath == path && file.Size == new FileInfo(path).Length && file.Extension == "TXT" && file.Category == FileCategory.Document, "Metadata mismatch");
        var large = Path.Combine(folder, "large-metadata.mp4");
        using (var stream = File.Create(large)) stream.SetLength(32 * 1024 * 1024);
        Check(FileClassifier.Inspect(large).Size == 33554432, "Large file metadata");
    }
    private static void Geometry()
    {
        Check(RadialHitTest.Sector(0, -100, 66, 150, 5) == 0, "Top sector");
        Check(RadialHitTest.Sector(0, 0, 66, 150, 5) == -1, "Hub");
        Check(RadialHitTest.Sector(200, 0, 66, 150, 5) == -1, "Outside");
        for (var n = 1; n <= 6; n++)
            for (var i = 0; i < n; i++)
            {
                var angle = (-90 + i * 360d / n) * Math.PI / 180;
                Check(RadialHitTest.Sector(100 * Math.Cos(angle), 100 * Math.Sin(angle), 66, 150, n) == i, "Sector center");
            }
    }
    private static void Options()
    {
        foreach (var extension in new[] { "PNG", "JPG", "JPEG", "PDF", "MP4", "MP3", "DOCX" })
        {
            var file = new DraggedFile("", extension, 0, FileClassifier.Category(extension));
            var service = new LocalConversionService();
            var options = WheelOptions.For(file, WheelMode.Convert, service.CanExecute);
            Check(options.All(x => x.Id != (extension == "JPEG" ? "JPG" : extension)), "Source shown");
            Check(options.All(x => x.Enabled && service.CanExecute(file, x.Id)), "Unimplemented converter enabled");
            if (extension == "PNG") Check(options.Any(x => x.Id == "HEIC"), "Bundled HEIC encoder missing from image destinations");
        }
        var png = new DraggedFile("", "PNG", 0, FileCategory.Image);
        var tools = WheelOptions.For(png, WheelMode.Tools, new LocalConversionService().CanExecute);
        Check(tools.Select(x => x.Id).SequenceEqual(new[] { "COMPRESS", "TARGETSIZE", "CROP", "ROTATE", "STRIP", "MERGE" }) && tools.All(x => x.Enabled), "All image tool capabilities must be implemented");
    }
    private static string CreatePng(string name)
    {
        var path = Path.Combine(folder, name);
        var pixels = new byte[32 * 32 * 4];
        // Fully transparent colored pixels must become white, not black.
        for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = 255; pixels[i + 3] = 0; }
        // An opaque red center also verifies that actual source pixels survive encoding.
        for (var y = 8; y < 24; y++)
            for (var x = 8; x < 24; x++)
            {
                var i = (y * 32 + x) * 4;
                pixels[i] = 0; pixels[i + 2] = 255; pixels[i + 3] = 255;
            }
        var bitmap = BitmapSource.Create(32, 32, 96, 96, PixelFormats.Bgra32, null, pixels, 128);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
        return path;
    }
    private static void Conversion()
    {
        var path = CreatePng("صورة شفافة.png");
        var hash = SHA256.HashData(File.ReadAllBytes(path));
        var existing = Path.ChangeExtension(path, ".jpg");
        File.WriteAllText(existing, "existing file must survive");
        var converter = new PngToJpegConverter();
        var first = converter.ConvertAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult();
        var second = converter.ConvertAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult();
        Check(first.OutputPath != second.OutputPath && first.OutputPath != existing, "Output overwrite");
        Check(Path.GetDirectoryName(first.OutputPath) == folder, "Output directory");
        Check(File.ReadAllText(existing) == "existing file must survive", "Existing output changed");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Original modified");
        using var input = File.OpenRead(first.OutputPath);
        var image = new JpegBitmapDecoder(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        var rgb = new FormatConvertedBitmap(image, PixelFormats.Bgr24, null, 0);
        var pixels = new byte[32 * 32 * 3]; rgb.CopyPixels(pixels, 96, 0);
        Check(pixels.Take(3).All(x => x >= 245), "Transparency is not white");
        var center = (16 * 32 + 16) * 3;
        Check(pixels[center] < 20 && pixels[center + 1] < 20 && pixels[center + 2] > 235, "Opaque source pixels were not preserved");
        Check(first.Width == 32 && first.Height == 32, "Dimensions changed");
    }
    private static void InvalidImages()
    {
        foreach (var (name, bytes) in new[] { ("broken.png", new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0 }), ("disguised.png", new byte[16]) })
        {
            var path = Path.Combine(folder, name); File.WriteAllBytes(path, bytes);
            var failed = false;
            try { new PngToJpegConverter().ConvertAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult(); }
            catch (Exception) { failed = true; }
            Check(failed && !File.Exists(Path.ChangeExtension(path, ".jpg")), "Invalid source produced output");
        }
        Check(!Directory.EnumerateFiles(folder, ".naqla-*.tmp").Any(), "Temporary output leaked");
    }
    private static void Cancellation()
    {
        var path = CreatePng("cancel.png");
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var cancelled = false;
        try { new PngToJpegConverter().ConvertAsync(FileClassifier.Inspect(path), cts.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && !File.Exists(Path.ChangeExtension(path, ".jpg")), "Cancellation produced output");
    }
    private static void DiagnosticPrivacy()
    {
        // Logger/schema unit test only. These records are not evidence of native input or OLE.
        var directory = Path.Combine(folder, "diagnostic-privacy");
        var secret = @"C:\Users\PRIVATE_PERSON\SecretFolder\private-image.png";
        string logPath;
        using (var log = new DiagnosticLog(directory))
        {
            Check(log.Available, "Log storage unavailable");
            logPath = log.CurrentFilePath!;
            log.BeginAttempt();
            log.Write(DiagnosticEvent.TestMarked, new() { Count = 1 });
            log.Write(DiagnosticEvent.ConversionStarted, DiagnosticData.FromFile(FileClassifier.Inspect(Path.Combine(folder, "ملف عربي للاختبار.txt"))));
            log.Write(DiagnosticEvent.ConversionFailed, DiagnosticData.FromException(new IOException("Denied " + secret)));
            log.Write(DiagnosticEvent.OperationInputAccepted, new() { Operation = DiagnosticOperation.Rename, Count = 2 });
            log.Write(DiagnosticEvent.ConversionFailed, DiagnosticData.FromException(new RenameRecoveryException(secret)) with { Operation = DiagnosticOperation.UndoRename });
            log.Write(DiagnosticEvent.SelectionAnalyzed, new() { Count = 12, Enabled = true, ConversionOptionsCount = 6, ToolOptionsCount = 5 });
            log.Write(DiagnosticEvent.BatchProgressChanged, new() { Count = 12, CompletedCount = 7, FailedCount = 1 });
            log.Write(DiagnosticEvent.BatchFinished, new() { Count = 12, CompletedCount = 8, FailedCount = 1, CancelledCount = 4 });
            log.Enabled = false;
            log.Write(DiagnosticEvent.ShiftDown);
            log.Enabled = true;
            log.Write(DiagnosticEvent.AppStopped);
            Check(log.Count(DiagnosticEvent.ShiftDown) == 0, "Disabled logging still captured input");
        }
        var text = File.ReadAllText(logPath);
        foreach (var forbidden in new[] { secret, "PRIVATE_PERSON", "SecretFolder", "private-image", folder, "Denied" })
            Check(!text.Contains(forbidden, StringComparison.OrdinalIgnoreCase), "Private data leaked into diagnostic log");
        var lines = File.ReadAllLines(logPath);
        Check(lines.Length >= 7 && text.Contains("DiagnosticsDisabled") && text.Contains("DiagnosticsEnabled") && text.Contains("AppStopped"), "Log did not drain cleanly");
        long previous = 0;
        foreach (var line in lines.Skip(1))
        {
            using var json = JsonDocument.Parse(line);
            var sequence = json.RootElement.GetProperty("Sequence").GetInt64();
            Check(sequence > previous, "Record ordering"); previous = sequence;
        }
    }
    private static void DiagnosticRetention()
    {
        var directory = Path.Combine(folder, "diagnostic-retention"); Directory.CreateDirectory(directory);
        var unrelated = Path.Combine(directory, "user-note.txt"); File.WriteAllText(unrelated, "preserve");
        for (var session = 0; session < 4; session++)
        {
            using var log = new DiagnosticLog(directory, maximumBytes: 900, keepSessions: 2);
            for (var i = 0; i < 100; i++) log.Write(DiagnosticEvent.TestMarked, new() { Count = i });
        }
        var files = Directory.GetFiles(directory, "session-*.jsonl");
        Check(files.Length == 2, "Session retention did not cap at two");
        Check(files.All(x => new FileInfo(x).Length < 2000 && File.ReadAllText(x).Contains("LogLimitReached")), "Log size cap missing");
        Check(File.ReadAllText(unrelated) == "preserve", "Unrelated file changed");
    }
    private static void DiagnosticStorageFailure()
    {
        var blockingFile = Path.Combine(folder, "not-a-log-directory"); File.WriteAllText(blockingFile, "file");
        using var log = new DiagnosticLog(blockingFile);
        Check(!log.Available, "Unwritable log path should be reported unavailable");
        log.Write(DiagnosticEvent.AppReady);
        Check(log.StatusText().Contains("unavailable"), "Storage failure not exposed in status");
    }
}
