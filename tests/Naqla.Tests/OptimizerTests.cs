using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ImageMagick;
using ImageMagick.Formats;
using Naqla.App;
using Naqla.Conversion;
using Naqla.Core;
using Naqla.Wheel;
using Naqla.Windows;
using Orientation = ImageMagick.Orientation;

internal static partial class Program
{
    private static string OptimizerNoise(string group, string name, int width = 384, int height = 256)
    {
        var path = Path.Combine(BatchFolder(group), name); var pixels = new byte[width * height * 4]; var random = new Random(6721);
        random.NextBytes(pixels);
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) pixels[(y * width + x) * 4 + 3] = (byte)(x < 8 ? 0 : 255);
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var output = File.Create(path); encoder.Save(output); return path;
    }
    private static ConversionResult Optimize(string path, TargetSizeSettings settings) => Service.ConvertAsync(new(new[] { FileClassifier.Inspect(path) }, "TARGETSIZE", Optimization: settings)).GetAwaiter().GetResult();
    private static ImageOptimizationPreview OptimizationPreview(string path, TargetSizeSettings settings, IProgress<ImageOptimizationProgress>? progress = null, CancellationToken token = default) =>
        Service.PreviewOptimizationAsync(FileClassifier.Inspect(path), settings, progress, token).GetAwaiter().GetResult();
    private static void OptimizationUnits()
    {
        Check(TargetSizeSettings.Presets.SequenceEqual(new long[] { 102400, 204800, 512000, 1048576 }), "Wrong preset units");
        Check(TargetSizeSettings.ParseBytes("0.5", ImageSizeUnit.MB) == 524288 && TargetSizeSettings.ParseBytes("١٠٠", ImageSizeUnit.KB) == 102400 && TargetSizeSettings.ParseBytes("۱٫۵", ImageSizeUnit.KB) == 1536, "Custom decimal/Arabic size parsing");
        foreach (var value in new[] { "0", "-1", "NaN", "", "1,200", "999999999999999999999999999999" })
        { var denied = false; try { TargetSizeSettings.ParseBytes(value, ImageSizeUnit.MB); } catch (Exception e) when (e is ArgumentException or OverflowException) { denied = true; } Check(denied, "Invalid target accepted"); }
        foreach (var settings in new TargetSizeSettings[] { new(0, "JPG"), new(long.MaxValue, "JPG"), new(100, "FAKE"), new(100, "PNG", new(0, 10)), new(100, "PNG", new(30001, 20)) })
        { var denied = false; try { settings.Validate(); } catch (Exception e) when (e is ArgumentException or NotSupportedException) { denied = true; } Check(denied, "Invalid optimizer settings accepted"); }
    }
    private static void OptimizationJpegQuality()
    {
        var path = OptimizerNoise("optimizer-quality", "ضجيج اختبار.png"); var hash = SHA256.HashData(File.ReadAllBytes(path));
        var measurements = new List<ImageOptimizationReport>();
        foreach (var target in TargetSizeSettings.Presets)
        {
            var output = Optimize(path, new(target, "JPG")); var report = output.Optimization!; measurements.Add(report);
            Check(report.Achieved && report.ResultBytes == new FileInfo(output.OutputPath).Length && report.ResultBytes <= target && output.Width == 384 && output.Height == 256, "Unmeasured or oversized JPEG publication/dimension loss");
            Check(report.Quality is >= 1 and <= 100 && report.Attempts == 101 - report.Quality, "Not an exhaustive descending integer-quality search");
        }
        var chosen = measurements[0]; Check(chosen.Attempts > 1 && chosen.Quality < 100, "Fixture did not require iterative encoding");
        // Independent same-codec encodes check every quality above the chosen value; no monotonic assumption.
        using (var reference = new MagickImage(path))
        {
            reference.AutoOrient(); reference.Strip(); reference.ColorSpace = ColorSpace.sRGB; reference.BackgroundColor = MagickColors.White;
            reference.Alpha(AlphaAction.Remove); reference.ColorType = ColorType.TrueColor; reference.Density = new Density(96);
            for (var quality = chosen.Quality!.Value + 1; quality <= 100; quality++)
            { reference.Quality = (uint)quality; Check(reference.ToByteArray(MagickFormat.Jpeg).LongLength > chosen.TargetBytes, "A higher JPEG quality met the target but was missed"); }
        }
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Optimizer changed the original bytes");
        File.WriteAllText(Path.Combine(folder, "optimizer-measurements.json"), JsonSerializer.Serialize(measurements, new JsonSerializerOptions { WriteIndented = true }));
        NoBatchTemps(Path.GetDirectoryName(path)!);
    }
    private static void OptimizationOutputFormats()
    {
        var path = GridPng("optimizer-alpha.png", 80, 60); var formats = Service.GetOptimizationSourceAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult().Formats;
        Check(formats.SequenceEqual(new[] { "PNG", "JPG", "WEBP", "AVIF", "TIFF", "HEIC" }), "Unexpected encoder availability");
        foreach (var format in formats)
        {
            var output = Optimize(path, new(1048576, format)); var report = output.Optimization!;
            using var decoded = new MagickImage(output.OutputPath);
            Check(report.Achieved && decoded.Width == 80 && decoded.Height == 60 && report.ResultBytes == new FileInfo(output.OutputPath).Length, "Output decode/bytes failed for " + format);
            var alpha = decoded.GetPixels().GetPixel(2, 30).ToColor()!;
            var opaque = decoded.GetPixels().GetPixel(65, 30).ToColor()!;
            if (format == "JPG") Check(alpha.R > 240 && alpha.G > 240 && alpha.B > 240 && report.TransparencyFlattened, "JPG did not use white transparency matte");
            else Check(alpha.A <= 5 && opaque.A >= 250 && !report.TransparencyFlattened, "Transparency was lost for " + format);
        }
        var missing = new LocalConversionService(heifDirectory: Path.Combine(folder, "absent-heif"));
        var offered = missing.GetOptimizationSourceAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult().Formats;
        Check(!offered.Contains("HEIC") && offered.Contains("JPG"), "Unavailable HEIC encoder was offered");
        NoBatchTemps(folder);
    }
    private static void OptimizationInputFormats()
    {
        var path = GridPng("optimizer-input.png", 48, 32); var inputs = new List<string> { path };
        foreach (var format in new[] { "JPG", "WEBP", "AVIF", "TIFF", "HEIC" }) inputs.Add(ConvertFile(path, format).OutputPath);
        var bmp = Path.Combine(folder, "optimizer-input.bmp"); using (var image = new MagickImage(path)) image.Write(bmp); inputs.Add(bmp);
        var hashes = HashSources(inputs);
        foreach (var input in inputs) Check(Optimize(input, new(512000, "PNG")).Optimization!.Achieved, "Input not optimized: " + Path.GetExtension(input));
        OriginalsUnchanged(hashes);
    }
    private static void OptimizationLossless()
    {
        var path = GridPng("optimizer-lossless.png", 120, 80);
        using var original = new MagickImage(path); var expected = original.GetPixels().ToByteArray("RGBA")!;
        foreach (var format in new[] { "PNG", "TIFF" })
        {
            var output = Optimize(path, new(102400, format)); using var decoded = new MagickImage(output.OutputPath);
            Check(expected.SequenceEqual(decoded.GetPixels().ToByteArray("RGBA")!) && output.Optimization!.Quality is null && output.Optimization.Attempts == (format == "PNG" ? 3 : 2), "Lossless optimizer changed pixels or invented quality");
            var failed = OptimizationPreview(path, new(1, format));
            Check(!failed.Report.Achieved && failed.Report.ResultBytes > 1 && failed.Image.Png.Length > 0, "Unattainable lossless preview falsely succeeded");
        }
        NoBatchTemps(folder);
    }
    private static void OptimizationResizeOrientation()
    {
        var path = Path.Combine(folder, "optimizer-exif.jpg");
        using (var image = new MagickImage(MagickColors.Red, 80, 40))
        {
            var exif = new ExifProfile(); exif.SetValue(ExifTag.Orientation, (ushort)6); image.SetProfile(exif); image.Orientation = Orientation.RightTop; image.SetProfile(ColorProfiles.SRGB); image.Write(path);
        }
        var plain = Optimize(path, new(102400, "PNG")); Check(plain.Width == 40 && plain.Height == 80, "Default dimensions ignored EXIF orientation");
        using (var decoded = new MagickImage(plain.OutputPath)) Check(decoded.Orientation is Orientation.TopLeft or Orientation.Undefined, "Stale orientation retained");
        var resized = Optimize(path, new(102400, "PNG", new(30, 30))); Check(resized.Width == 15 && resized.Height == 30, "Resize failed aspect ratio or oriented dimensions");
        var noUpscale = Optimize(path, new(102400, "PNG", new(1000, 1000))); Check(noUpscale.Width == 40 && noUpscale.Height == 80, "Resize silently enlarged source");
        var cmyk = Path.Combine(folder, "optimizer-cmyk.tiff"); using (var image = new MagickImage(MagickColors.Red, 32, 24)) { image.ColorSpace = ColorSpace.CMYK; image.Write(cmyk); }
        using var rgb = new MagickImage(Optimize(cmyk, new(102400, "PNG")).OutputPath);
        var pixel = rgb.GetPixels().GetPixel(10, 10).ToColor()!; Check(pixel.R > 230 && pixel.G < 25 && pixel.B < 25, "CMYK conversion changed red rendering");
    }
    private static void OptimizationImpossibleAndInvalid()
    {
        var directory = BatchFolder("optimizer-impossible"); var path = OptimizerNoise("optimizer-impossible", "impossible.png", 64, 48); var before = Directory.GetFiles(directory).Length;
        var preview = OptimizationPreview(path, new(1, "JPG")); Check(!preview.Report.Achieved && preview.Report.Attempts == 100, "Lossy failure did not search the quality domain");
        ImageTargetNotMetException? failure = null; try { Optimize(path, new(1, "JPG")); } catch (ImageTargetNotMetException e) { failure = e; }
        Check(failure is not null && !failure.Report.Achieved && JobFailure.FromException(failure).Kind == JobFailureKind.ImageTargetNotMet && Directory.GetFiles(directory).Length == before, "Unattainable target published or reported success");
        var corrupt = Path.Combine(directory, "corrupt.png"); File.WriteAllText(corrupt, "not an image");
        var failed = false; try { Optimize(corrupt, new(102400, "PNG")); } catch (MagickException) { failed = true; } Check(failed, "Corrupt image optimized");
        var multi = Path.Combine(directory, "multipage.tiff"); using (var frames = new MagickImageCollection { new MagickImage(MagickColors.Red, 10, 10), new MagickImage(MagickColors.Blue, 10, 10) }) frames.Write(multi);
        failed = false; try { Optimize(multi, new(102400, "PNG")); } catch (NotSupportedException) { failed = true; } Check(failed, "Multi-frame image silently lost pages");
        failed = false; try { Service.ConvertAsync(new ConversionRequest(FileClassifier.Inspect(path), "TARGETSIZE")).GetAwaiter().GetResult(); } catch (ArgumentException) { failed = true; } Check(failed, "Missing optimizer settings accepted");
        NoBatchTemps(directory);
    }
    private static void OptimizationNames()
    {
        var directory = BatchFolder("optimizer-أسماء"); var path = SolidImage(directory, "صورة أصل.png", MagickColors.Blue, 64, 48); var hashes = HashSources(new[] { path });
        var occupied = Path.Combine(directory, "صورة أصل optimized.png"); File.WriteAllText(occupied, "unrelated");
        Directory.CreateDirectory(Path.Combine(directory, "صورة أصل optimized (1).png"));
        var output = Optimize(path, new(102400, "PNG"));
        Check(Path.GetDirectoryName(output.OutputPath) == directory && Path.GetFileName(output.OutputPath) == "صورة أصل optimized (2).png" && File.ReadAllText(occupied) == "unrelated", "Unicode/collision naming replaced another object");
        OriginalsUnchanged(hashes); NoBatchTemps(directory);
    }
    private static void OptimizationCancellation()
    {
        var path = OptimizerNoise("optimizer-cancel", "source.png"); var hash = SHA256.HashData(File.ReadAllBytes(path));
        using var cancellation = new CancellationTokenSource(); var reported = 0; var writerBlocked = false;
        var observer = new InlineProgress<ImageOptimizationProgress>(_ =>
        {
            reported++; try { using var writer = File.OpenWrite(path); } catch (IOException) { writerBlocked = true; } cancellation.Cancel();
        });
        var cancelled = false; try { OptimizationPreview(path, new(1, "JPG"), observer, cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && reported == 1 && writerBlocked && hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "In-flight preview did not cancel/lock/preserve source");
        using var before = new CancellationTokenSource(); before.Cancel(); cancelled = false;
        try { Service.ConvertAsync(new(new[] { FileClassifier.Inspect(path) }, "TARGETSIZE", Optimization: new(100000, "JPG")), before.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && Directory.GetFiles(Path.GetDirectoryName(path)!).Length == 1, "Cancelled operation published output"); NoBatchTemps(Path.GetDirectoryName(path)!);
    }
    private static void OptimizationBatch()
    {
        var directory = BatchFolder("optimizer-batch"); var a = SolidImage(directory, "صورة.png", MagickColors.Red, 48, 32); var b = SolidImage(BatchFolder("optimizer-other"), "صورة.png", MagickColors.Blue, 24, 48);
        var noise = OptimizerNoise("optimizer-batch", "too-large.png", 192, 128); var corrupt = Path.Combine(directory, "bad.png"); File.WriteAllText(corrupt, "corrupt");
        var paths = new[] { a, noise, corrupt, b }; var hashes = HashSources(paths);
        var fallback = Service.GetOptimizationSourceAsync(new[] { FileClassifier.Inspect(corrupt), FileClassifier.Inspect(a), FileClassifier.Inspect(b) }).GetAwaiter().GetResult();
        Check(fallback.PreviewIndex == 1, "Corrupt first input blocked a readable batch preview or was silently discarded");
        var result = RunBatch(BatchRequest("TARGETSIZE", paths) with { Optimization = new(1000, "PNG") });
        Check(result.Succeeded == 2 && result.Failed == 2 && result.Jobs[1].Failure!.Kind == JobFailureKind.ImageTargetNotMet && result.Jobs[0].Output!.Optimization!.Achieved && result.Jobs[3].Output!.Optimization!.Achieved, "Batch did not preserve independent target checks/partial success");
        Check(result.OutputDirectories.Count == 2, "Batch output directories missing"); OriginalsUnchanged(hashes); NoBatchTemps(directory);
        var progressWindow = new BatchProgressWindow(); progressWindow.Finish(result);
        var content = (FrameworkElement)progressWindow.Content; content.Measure(new Size(350, 600)); content.Arrange(new Rect(0, 0, 350, content.DesiredSize.Height)); content.UpdateLayout(); SaveRender(content, 350, (int)Math.Ceiling(content.DesiredSize.Height), "native-optimizer-results.png"); progressWindow.CloseForExit();
    }
    private static void OptimizationBatchCancellation()
    {
        var a = OptimizerNoise("optimizer-batch-cancel", "a.png", 64, 48); var b = OptimizerNoise("optimizer-batch-cancel", "b.png", 64, 48); var hashes = HashSources(new[] { a, b });
        using var cancellation = new CancellationTokenSource(); var processor = new BatchProcessor(Service);
        var observer = new InlineProgress<BatchProgress>(state => { if (state.Succeeded == 1) cancellation.Cancel(); });
        var result = processor.RunAsync(BatchRequest("TARGETSIZE", a, b) with { Optimization = new(102400, "WEBP") }, observer, cancellation.Token).GetAwaiter().GetResult();
        Check(result.Succeeded == 1 && result.Cancelled == 1 && result.Completed == 1 && File.Exists(result.Jobs[0].Output!.OutputPath), "Batch cancellation lost successes or invented completion"); OriginalsUnchanged(hashes); NoBatchTemps(Path.GetDirectoryName(a)!);
    }
    private static void Pump(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame(); _ = task.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
        var settle = new DispatcherFrame(); _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => settle.Continue = false); Dispatcher.PushFrame(settle);
    }
    private static IEnumerable<FrameworkElement> Elements(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        { if (child is FrameworkElement element) yield return element; foreach (var nested in Elements(child)) yield return nested; }
    }
    private static void OptimizationNativeUi()
    {
        var application = Application.Current ?? new Application(); application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var path = GridPng("optimizer-ui.png", 160, 100); var converter = new LocalConversionService(renamer: RenameService()); var source = converter.GetOptimizationSourceAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult();
        var queue = new BatchProcessor(converter);
        var window = new TargetSizeWindow(source, 1, (settings, progress, ct) => queue.RunExclusiveAsync(token => converter.PreviewOptimizationAsync(FileClassifier.Inspect(path), settings, progress, token), ct));
        var content = (FrameworkElement)window.Content; content.Measure(new Size(528, 630)); content.Arrange(new Rect(0, 0, 528, 630)); content.UpdateLayout();
        Check(!window.CanSave && window.MeasuredSettings is null, "Save enabled before real measurement"); SaveRender(content, 528, 630, "native-optimizer-settings.png");
        Pump(window.StartMeasurementAsync()); Check(window.CanSave && window.Measurement!.Report.Achieved && window.MeasuredSettings!.Resize is null, "Real native preview didn't gate Save/default dimensions");
        content.UpdateLayout(); SaveRender(content, 528, 630, "native-optimizer-preview.png");
        Elements(content).OfType<TextBox>().Single(x => x.Name == "TargetAmount").Text = "0.0009765625"; // Exactly 1 byte.
        Check(!window.CanSave && window.Measurement is null, "Edited settings retained stale measured success"); Pump(window.StartMeasurementAsync());
        Check(!window.CanSave && window.Measurement is { Report.Achieved: false } && window.ResultText.Contains("غير قابل"), "Impossible native preview falsely enabled single Save");
        content.UpdateLayout(); SaveRender(content, 528, 630, "native-optimizer-unattainable.png"); window.Close();
        var cancelledWindow = new TargetSizeWindow(source, 1, (settings, progress, ct) => queue.RunExclusiveAsync(token => converter.PreviewOptimizationAsync(FileClassifier.Inspect(path), settings, progress, token), ct));
        var cancelledTask = cancelledWindow.StartMeasurementAsync(); cancelledWindow.Close(); Pump(cancelledTask);
        Check(!cancelledWindow.CanSave, "Closing the optimizer retained an accepted measurement");
        foreach (var count in new[] { 1, 2 })
        {
            var files = count == 1 ? new[] { path } : new[] { path, GridPng("optimizer-ui-two.png") }; var payload = FileDragPayload.Inspect(files);
            var options = WheelOptions.For(payload, WheelMode.Tools, converter.CanExecute); Check(options.Any(x => x.Id == "TARGETSIZE") && options.Count == count + 5, "Contextual image tool disappeared/geometry changed");
            var wheel = new PixelWheel(converter.CanExecute); wheel.SetSelection(payload); wheel.SetMode(true, false);
            var index = options.ToList().FindIndex(x => x.Id == "TARGETSIZE"); var angle = (-90 + index * 360d / options.Count) * Math.PI / 180;
            wheel.Track(new Point(130 + 85 * Math.Cos(angle), 130 + 85 * Math.Sin(angle))); Check(wheel.Selection?.Id == "TARGETSIZE", "New action hit target failed");
            wheel.Measure(new Size(260, 306)); wheel.Arrange(new Rect(0, 0, 260, 306)); wheel.UpdateLayout(); SaveRender(wheel, 260, 306, $"native-optimizer-wheel-{count}.png");
        }
        SynchronizationContext.SetSynchronizationContext(null); NoBatchTemps(folder);
    }
    private static void OptimizationDiagnostics()
    {
        var path = Path.Combine(folder, "private-optimizer-source.png"); string logPath;
        using (var log = new DiagnosticLog(Path.Combine(folder, "optimizer-diagnostics")))
        {
            logPath = log.CurrentFilePath!;
            log.Write(DiagnosticEvent.OperationInputAccepted, new() { Operation = DiagnosticOperation.TargetSize, Target = DiagnosticFormat.JPG, TargetBytes = 102400 });
            log.Write(DiagnosticEvent.ConversionSucceeded, new() { Operation = DiagnosticOperation.TargetSize, ResultBytes = 99800, TargetBytes = 102400 });
            log.Write(DiagnosticEvent.ConversionFailed, DiagnosticData.FromException(new IOException(path)));
        }
        var text = File.ReadAllText(logPath); Check(text.Contains("TargetSize") && text.Contains("102400") && text.Contains("99800") && !text.Contains(folder) && !text.Contains("private-optimizer"), "Optimizer diagnostic privacy/measurements failed");
    }
    private static void OptimizationManualHelpers()
    {
        var directory = Path.Combine(folder, "manual-target-size-صور");
        int Script(string script, params string[] arguments)
        {
            var start = new System.Diagnostics.ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.GetFullPath(Path.Combine("scripts", script)) }.Concat(arguments)) start.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            Check(process.WaitForExit(30000), "Target-size helper timed out"); Task.WaitAll(output, error); return process.ExitCode;
        }
        Check(Script("prepare-target-size-desktop-test.ps1", "-Destination", directory) == 0, "Target fixture preparation failed");
        Check(Directory.GetFiles(Path.Combine(directory, "images")).Any(x => Path.GetFileName(x) == "صورة-01.png"), "PowerShell mangled Unicode fixture names; retain script BOM");
        Check(Script("verify-target-size-desktop-test.ps1", "-Directory", directory) != 0, "Missing target outputs falsely passed file verification");
        foreach (var group in new[] { "images", "partial-failure" })
        {
            var paths = Directory.GetFiles(Path.Combine(directory, group), "*.png");
            var result = RunBatch(BatchRequest("TARGETSIZE", paths) with { Optimization = new(102400, "JPG") });
            Check(result.Succeeded == (group == "images" ? 3 : 1) && result.Failed == (group == "images" ? 0 : 1), "Manual fixtures didn't produce independently checked real results");
            Check(Script("verify-target-size-desktop-test.ps1", "-Directory", directory, "-Group", group) == 0, "Independent System.Drawing output/hash/cap verification failed");
            using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, group + "-verification.json")));
            Check(report.RootElement.GetProperty("DiskEvidencePassed").GetBoolean() && !report.RootElement.GetProperty("ActualExplorerDragVerified").GetBoolean(), "Disk verifier falsely claimed Explorer acceptance");
        }
        Check(Script("verify-target-size-desktop-test.ps1", "-Directory", directory, "-Group", "unattainable", "-TargetBytes", "1", "-ExpectNoOutput") == 0, "No-output scenario didn't preserve source");
    }
}
