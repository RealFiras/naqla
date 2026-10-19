using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Naqla.App;
using Naqla.Conversion;
using Naqla.Core;
using Naqla.Wheel;

internal static partial class Program
{
    private static string BatchFolder(string name) { var directory = Path.Combine(folder, name); Directory.CreateDirectory(directory); return directory; }
    private static ConversionRequest BatchRequest(string target, params string[] paths) => new(paths.Select(FileClassifier.Inspect).ToArray(), target);
    private static Dictionary<string, byte[]> HashSources(IEnumerable<string> paths) => paths.ToDictionary(x => x, x => SHA256.HashData(File.ReadAllBytes(x)));
    private static void OriginalsUnchanged(Dictionary<string, byte[]> hashes)
    { foreach (var pair in hashes) Check(pair.Value.SequenceEqual(SHA256.HashData(File.ReadAllBytes(pair.Key))), "Batch modified an original"); }
    private static void NoBatchTemps(string directory) => Check(!Directory.EnumerateFileSystemEntries(directory, ".naqla-*", SearchOption.AllDirectories).Any(), "Batch leaked owned temporary output");
    private static BatchResult RunBatch(ConversionRequest request, IProgress<BatchProgress>? progress = null, CancellationToken token = default) => new BatchProcessor(Service).RunAsync(request, progress, token).GetAwaiter().GetResult();

    private static void BatchCapabilities()
    {
        var directory = BatchFolder("batch-capabilities");
        var png = SolidImage(directory, "صورة.png", MagickColors.Red, 80, 60);
        var jpg = SolidImage(directory, "صورة.jpg", MagickColors.Blue, 80, 60);
        var images = FileDragPayload.Inspect(new[] { png, jpg });
        var conversions = WheelOptions.For(images, WheelMode.Convert, Service.CanExecute);
        Check(conversions.Any(x => x.Id == "WEBP") && conversions.Any(x => x.Id == "HEIC") && !conversions.Any(x => x.Id is "PNG" or "JPG"), "Image common actions or source omission wrong");
        Check(WheelOptions.For(images, WheelMode.Tools, Service.CanExecute).Select(x => x.Id).SequenceEqual(new[] { "COMPRESS", "TARGETSIZE", "CROP", "ROTATE", "STRIP", "MERGE" }), "Batch image tools lost");
        var mixed = FileDragPayload.Inspect(new[] { png, Path.Combine(folder, "فيديو وصوت حقيقي.mp3") });
        Check(mixed.IsValid && mixed.Categories.Count == 2 && WheelOptions.For(mixed, WheelMode.Convert, Service.CanExecute).Count == 0, "Mixed selection guessed conversion");
        Check(WheelOptions.For(mixed, WheelMode.Tools, Service.CanExecute).Select(x => x.Id).SequenceEqual(new[] { "COMPRESS", "STRIP" }), "Mixed image/audio common tools incorrect");
        var text = Path.Combine(directory, "نص.txt"); File.WriteAllText(text, "actual local text");
        var noCommon = FileDragPayload.Inspect(new[] { png, text });
        Check(WheelOptions.For(noCommon, WheelMode.Convert, Service.CanExecute).Count == 0 && WheelOptions.For(noCommon, WheelMode.Tools, Service.CanExecute).Count == 0, "No-common case offered a fake operation");
        var unsupported = Path.Combine(directory, "unknown.xyz"); File.WriteAllText(unsupported, "unsupported");
        var invalid = FileDragPayload.Inspect(new[] { png, unsupported, Path.Combine(directory, "missing.png") });
        Check(invalid.Inspections.Count == 3 && !invalid.IsValid && invalid.Inspections[1].Validation == SourceValidation.UnsupportedExtension && invalid.Inspections[2].Validation == SourceValidation.Unreadable, "Per-source validation lost/skipped an input");
        Check(WheelOptions.For(invalid, WheelMode.Tools, Service.CanExecute).Count == 0 && !Service.CanExecute(invalid, "STRIP"), "Invalid member silently skipped");
        var missing = new LocalConversionService(Path.Combine(directory, "no-media"), Path.Combine(directory, "no-heif"));
        Check(!WheelOptions.For(images, WheelMode.Convert, missing.CanExecute).Any(x => x.Id == "HEIC"), "Unavailable backend advertised");
    }
    private static void BatchImageConversion()
    {
        var directory = BatchFolder("batch-صور"); var other = BatchFolder("batch-مجلد ثان");
        var inputs = new[] { SolidImage(directory, "متشابه.png", MagickColors.Red, 80, 60), SolidImage(other, "متشابه.png", MagickColors.Blue, 80, 60), SolidImage(directory, "ثالث.png", MagickColors.Lime, 80, 60) };
        var hashes = HashSources(inputs);
        var occupied = Path.ChangeExtension(inputs[0], ".jpg"); File.WriteAllText(occupied, "existing output sentinel");
        var snapshots = new List<BatchProgress>();
        var result = RunBatch(BatchRequest("JPG", inputs), new InlineProgress<BatchProgress>(snapshots.Add));
        Check(result.Succeeded == 3 && result.Failed == 0 && result.Cancelled == 0 && result.OutputDirectories.Count == 2, "Batch result counts/directories");
        Check(File.ReadAllText(occupied) == "existing output sentinel", "Batch overwrote existing name");
        for (var i = 0; i < inputs.Length; i++)
        {
            var output = result.Jobs[i].Output!; Check(Path.GetDirectoryName(output.OutputPath) == Path.GetDirectoryName(inputs[i]), "Output not beside corresponding source");
            using var decoded = new MagickImage(output.OutputPath); var color = decoded.GetPixels().GetPixel(40, 30).ToColor()!;
            Check(i switch { 0 => color.R > 220, 1 => color.B > 220, _ => color.G > 220 }, "Batch swapped source pixels");
        }
        Check(snapshots.Last().Completed == 3 && snapshots.Last().Percent == 100 && snapshots.Where(x => x.Current > 0).Select(x => x.Current).SequenceEqual(new[] { 1, 2, 3 }), "Invented/skipped job progress");
        var single = RunBatch(BatchRequest("WEBP", inputs[0])); Check(single.Succeeded == 1 && single.Jobs.Count == 1, "Single-file queue regression");
        OriginalsUnchanged(hashes); NoBatchTemps(directory); NoBatchTemps(other);
    }
    private static void BatchMixedImages()
    {
        var directory = BatchFolder("batch-mixed-images");
        var inputs = new[] { SolidImage(directory, "first.png", MagickColors.Red, 50, 40), SolidImage(directory, "ثاني.jpg", MagickColors.Blue, 50, 40), SolidImage(directory, "third.tiff", MagickColors.Lime, 50, 40) };
        var hashes = HashSources(inputs); var result = RunBatch(BatchRequest("WEBP", inputs));
        Check(result.Succeeded == 3, "Mixed image batch failed");
        foreach (var job in result.Jobs) { using var image = new MagickImage(job.Output!.OutputPath); Check(image.Format == MagickFormat.WebP && image.Width == 50, "Invalid batch codec result"); }
        var pdf = RunBatch(BatchRequest("PDF", inputs));
        Check(pdf.Succeeded == 3 && pdf.Jobs.All(x => x.Output!.Items == 1), "Convert PDF silently merged sources");
        OriginalsUnchanged(hashes); NoBatchTemps(directory);
    }
    private static void BatchVideoConversion()
    {
        var directory = BatchFolder("batch-videos"); var video = MediaSource();
        var first = Path.Combine(directory, "أول.mp4"); var second = Path.Combine(directory, "ثاني.mov"); File.Copy(video, first);
        Engine("-v", "error", "-nostdin", "-n", "-i", video, "-c", "copy", second);
        var inputs = new[] { first, second }; var hashes = HashSources(inputs);
        var payload = FileDragPayload.Inspect(inputs); Check(payload.IsValid && WheelOptions.For(payload, WheelMode.Convert, Service.CanExecute).Any(x => x.Id == "WEBM"), "Multi-video drag actions missing");
        var result = RunBatch(BatchRequest("WEBM", inputs)); Check(result.Succeeded == 2 && result.OutputDirectories.Count == 1, "Multi-video outputs failed");
        foreach (var job in result.Jobs) Engine("-v", "error", "-nostdin", "-i", job.Output!.OutputPath, "-f", "null", "-");
        OriginalsUnchanged(hashes); NoBatchTemps(directory);
    }
    private static void BatchMixedTools()
    {
        var directory = BatchFolder("batch-mixed-tools");
        var image = SolidImage(directory, "صورة.png", MagickColors.Red, 80, 60);
        var audio = Path.Combine(directory, "صوت.mp3"); File.Copy(Path.Combine(folder, "فيديو وصوت حقيقي.mp3"), audio);
        var inputs = new[] { image, audio }; var hashes = HashSources(inputs);
        var result = RunBatch(BatchRequest("STRIP", inputs));
        Check(result.Succeeded == 2 && Path.GetExtension(result.Jobs[0].Output!.OutputPath) == ".png" && Path.GetExtension(result.Jobs[1].Output!.OutputPath) == ".mp3", "Mixed tools skipped a file or changed category");
        Engine("-v", "error", "-nostdin", "-i", result.Jobs[1].Output!.OutputPath, "-f", "null", "-"); OriginalsUnchanged(hashes); NoBatchTemps(directory);
    }
    private static void BatchPartialFailure()
    {
        var directory = BatchFolder("batch-partial");
        var first = SolidImage(directory, "good-before.png", MagickColors.Red, 80, 60);
        var broken = Path.Combine(directory, "تالف.png"); File.WriteAllText(broken, "not PNG bytes");
        var last = SolidImage(directory, "good-after.png", MagickColors.Blue, 80, 60);
        var inputs = new[] { first, broken, last }; var hashes = HashSources(inputs);
        var result = RunBatch(BatchRequest("JPG", inputs));
        Check(result.Succeeded == 2 && result.Failed == 1 && result.Jobs[1].Failure is not null && result.Jobs[2].Output is not null, "One corrupt source discarded unrelated successes");
        Check(!File.Exists(Path.ChangeExtension(broken, ".jpg")), "Corrupt source published output");
        foreach (var output in result.Jobs.Where(x => x.Output is not null)) { using var decoded = new MagickImage(output.Output!.OutputPath); Check(decoded.Width == 80, "Partial success not decodable"); }
        OriginalsUnchanged(hashes); NoBatchTemps(directory);
    }
    private static void BatchReadOnlyDestination()
    {
        var directory = BatchFolder("batch-permissions"); var denied = Path.Combine(directory, "read-only"); var writable = Path.Combine(directory, "writable");
        var first = SolidImage(denied, "source.png", MagickColors.Red, 64, 48); var second = SolidImage(writable, "source.png", MagickColors.Blue, 64, 48);
        var inputs = new[] { first, second }; var hashes = HashSources(inputs);
        var info = new DirectoryInfo(denied); var originalAcl = info.GetAccessControl();
        var restricted = info.GetAccessControl(); restricted.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.WriteData | FileSystemRights.AppendData, AccessControlType.Deny));
        BatchResult result;
        try { info.SetAccessControl(restricted); result = RunBatch(BatchRequest("JPG", inputs)); }
        finally { info.SetAccessControl(originalAcl); }
        Check(result.Failed == 1 && result.Succeeded == 1 && result.Jobs[0].Failure!.Kind == JobFailureKind.PermissionDenied, "Actual denied output was not reported per-file");
        Check(result.OutputDirectories.SequenceEqual(new[] { writable }), "Failed folder misreported as an output directory");
        OriginalsUnchanged(hashes); NoBatchTemps(directory);
    }
    private static void BatchCancellation()
    {
        var directory = BatchFolder("batch-cancel");
        var inputs = Enumerable.Range(0, 12).Select(i => SolidImage(directory, $"صورة-{i}.png", MagickColors.Red, 500, 400)).ToArray(); var hashes = HashSources(inputs);
        using var cancellation = new CancellationTokenSource(); var snapshots = new List<BatchProgress>();
        var result = RunBatch(BatchRequest("JPG", inputs), new InlineProgress<BatchProgress>(state => { snapshots.Add(state); if (state.Succeeded == 1) cancellation.Cancel(); }), cancellation.Token);
        Check(result.Succeeded == 1 && result.Cancelled == 11 && result.Failed == 0 && result.Completed == 1, "Mid-batch cancellation lost success or started remaining jobs");
        Check(Directory.GetFiles(directory, "*.jpg").Length == 1 && snapshots.Last().Percent < 100, "Cancellation invented completion or leaked output");
        using var alreadyCancelled = new CancellationTokenSource(); alreadyCancelled.Cancel();
        var stopped = RunBatch(BatchRequest("WEBP", inputs), token: alreadyCancelled.Token);
        Check(stopped.Cancelled == 12 && !Directory.GetFiles(directory, "*.webp").Any(), "Pre-cancelled queue ran jobs");
        OriginalsUnchanged(hashes); NoBatchTemps(directory);
    }
    private static void BatchCropPartialFailure()
    {
        var directory = BatchFolder("batch-crop");
        var large = SolidImage(directory, "large.png", MagickColors.Red, 80, 60); var small = SolidImage(directory, "small.png", MagickColors.Blue, 20, 20);
        var inputs = new[] { large, small }; var hashes = HashSources(inputs);
        var request = BatchRequest("CROP", inputs) with { Crop = new(25, 20, 30, 25) };
        var result = RunBatch(request);
        Check(result.Succeeded == 1 && result.Failed == 1 && result.Jobs[0].Output!.Width == 30 && result.Jobs[0].Output!.Height == 25, "Crop silently clamped/ignored incompatible input");
        OriginalsUnchanged(hashes); NoBatchTemps(directory);
    }
    private static void BatchConcurrency()
    {
        var fake = new CountingConverter(); var processor = new BatchProcessor(fake);
        var files = Enumerable.Range(0, 5).Select(i => new DraggedFile($"controlled-{i}.png", "PNG", 1, FileCategory.Image)).ToArray();
        // This is a queue scheduling test double, not file/codec/Explorer acceptance evidence.
        var first = processor.RunAsync(new(files, "JPG")); var second = processor.RunAsync(new(files, "JPG"));
        Task.WaitAll(first, second);
        Check(BatchProcessor.MaximumConcurrency == 1 && fake.MaximumActive == 1 && fake.Calls == 10 && first.Result.Succeeded == 5 && second.Result.Succeeded == 5, "Queue exceeded global engine concurrency");
        var failed = false; try { processor.RunAsync(new(Array.Empty<DraggedFile>(), "JPG")).GetAwaiter().GetResult(); } catch (ArgumentException) { failed = true; }
        Check(failed, "Empty batch accepted");
    }
    private static void BatchNativeRender()
    {
        _ = Application.Current ?? new Application();
        var paths = Directory.GetFiles(Path.Combine(folder, "batch-صور"), "*.png"); var payload = FileDragPayload.Inspect(paths);
        var wheel = new PixelWheel(Service.CanExecute); wheel.SetSelection(payload);
        Check(wheel.HubLabel == $"{paths.Length} ملف" && wheel.Width == 260 && wheel.Height == 306, "Batch hub altered wheel geometry");
        wheel.Measure(new Size(260, 306)); wheel.Arrange(new Rect(0, 0, 260, 306)); wheel.UpdateLayout(); SaveRender(wheel, 260, 306, "native-batch-wheel.png");
        var window = new BatchProgressWindow(); var content = (FrameworkElement)window.Content;
        window.Update(new(12, 7, 6, 1, 8)); Check(window.CompletedPercent == 58 && window.StatusText.Contains("8 / 12"), "Progress not based on completed real job counts");
        content.Measure(new Size(350, double.PositiveInfinity)); content.Arrange(new Rect(0, 0, 350, content.DesiredSize.Height)); content.UpdateLayout();
        SaveRender(content, 350, (int)Math.Ceiling(content.DesiredSize.Height), "native-batch-progress.png");
        window.Finish(new(new[] { new BatchItemResult(0, payload.Files, JobOutcome.Cancelled), new BatchItemResult(1, payload.Files, JobOutcome.Failed, Failure: new(JobFailureKind.IO, 0)) }));
        Check(window.CompletedPercent == 50 && window.StatusText.Contains("أُلغي"), "Cancelled jobs shown as completed"); window.CloseForExit();
    }
    private static void BatchCapabilityTiming()
    {
        var input = Path.Combine(folder, "batch-capabilities", "صورة.png");
        var directory = BatchFolder("batch-timing"); var paths = Enumerable.Range(0, 200).Select(i => { var path = Path.Combine(directory, $"image-{i}.png"); File.Copy(input, path); return path; }).ToArray();
        var payload = FileDragPayload.Inspect(paths); var wheel = new PixelWheel(Service.CanExecute); var single = payload.Files[0];
        double[] Measure(Action action) { action(); return Enumerable.Range(0, 25).Select(_ => { var watch = Stopwatch.StartNew(); action(); return watch.Elapsed.TotalMilliseconds; }).Order().ToArray(); }
        var baseline = Measure(() => wheel.SetFile(single)); var batch = Measure(() => wheel.SetSelection(payload));
        var report = new { SourceCount = 200, BaselineSetFileMedianMs = baseline[12], BatchSetSelectionMedianMs = batch[12], BatchSetSelectionP95Ms = batch[23], ActualShiftActivationMeasured = false };
        File.WriteAllText(Path.Combine(folder, "batch-capability-timing.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Check(wheel.HubLabel == "200 ملف" && payload.PreviewFile.Size == 200 * single.Size, "Maximum selection metadata changed");
    }
    private static void BatchManualFixtureScripts()
    {
        var directory = Path.Combine(folder, "manual-script-صور");
        int Script(string name, params string[] arguments)
        {
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.GetFullPath(Path.Combine("scripts", name)) }.Concat(arguments)) start.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            Check(process.WaitForExit(30000), "Manual fixture helper timed out"); Task.WaitAll(output, error); return process.ExitCode;
        }
        Check(Script("prepare-batch-desktop-test.ps1", "-Destination", directory) == 0, "Real fixture preparation failed");
        Check(Script("verify-batch-desktop-test.ps1", "-Directory", directory) != 0, "Missing outputs falsely passed desktop file verification");
        foreach (var group in new[] { "10-images", "partial-failure" })
        {
            var inputs = Directory.GetFiles(Path.Combine(directory, group), "*.png"); var result = RunBatch(BatchRequest("JPG", inputs));
            Check(inputs.Any(path => Path.GetFileName(path).StartsWith("صورة-", StringComparison.Ordinal)), "PowerShell 5.1 mangled Arabic fixture names; preserve the script UTF-8 BOM");
            Check(result.Succeeded == (group == "10-images" ? 10 : 3) && result.Failed == (group == "10-images" ? 0 : 1), "Manual fixture expected counts");
            Check(Script("verify-batch-desktop-test.ps1", "-Directory", directory, "-Group", group) == 0, "Real desktop file verifier failed");
            using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, group + "-verification.json")));
            Check(report.RootElement.GetProperty("DiskEvidencePassed").GetBoolean() && !report.RootElement.GetProperty("ActualExplorerDragVerified").GetBoolean(), "File evidence was confused with real Explorer acceptance");
        }
    }
    private static void SaveRender(FrameworkElement element, int width, int height, string name)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output = File.Create(Path.Combine(folder, name)); encoder.Save(output);
    }
    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T> { public void Report(T value) => callback(value); }
    private sealed class CountingConverter : IConversionService
    {
        private int active; public int MaximumActive; public int Calls;
        public bool CanExecute(DraggedFile file, string target) => target == "JPG";
        public bool CanExecute(FileDragPayload payload, string target) => target == "JPG";
        public Task<ConversionResult> ConvertAsync(DraggedFile file, string target, CancellationToken cancellationToken = default) => ConvertAsync(new ConversionRequest(file, target), cancellationToken);
        public async Task<ConversionResult> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken = default)
        {
            var count = Interlocked.Increment(ref active); MaximumActive = Math.Max(MaximumActive, count); Interlocked.Increment(ref Calls);
            try { await Task.Delay(20, cancellationToken); return new("controlled-test-result.jpg", 1, 1); }
            finally { Interlocked.Decrement(ref active); }
        }
    }
}
