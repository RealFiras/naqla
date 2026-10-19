using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using ImageMagick.Formats;
using Naqla.App;
using Naqla.Core;
using Naqla.Conversion;
using Naqla.Wheel;
using UglyToad.PdfPig;

internal static partial class Program
{
    private static void CropGeometry()
    {
        Check(CropSelection.FromNormalized(.1, .2, .6, .7, 100, 100) == new CropRectangle(10, 20, 50, 50), "Normal crop coordinates");
        Check(CropSelection.FromNormalized(.6, .7, .1, .2, 100, 100) == new CropRectangle(10, 20, 50, 50), "Reverse crop coordinates");
        Check(CropSelection.FromNormalized(-5, -2, 3, 8, 80, 60) == new CropRectangle(0, 0, 80, 60), "Out-of-preview clamping");
        Check(CropSelection.FromNormalized(1, 1, 1, 1, 80, 60) == new CropRectangle(79, 59, 1, 1), "Last pixel crop");
        foreach (var invalid in new[] { new CropRectangle(-1, 0, 2, 2), new(0, 0, 0, 2), new(70, 0, 20, 2), new(int.MaxValue, 0, int.MaxValue, 2) })
        {
            var rejected = false; try { invalid.Validate(80, 60); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid crop accepted");
        }
        var failed = false; try { CropSelection.FromNormalized(double.NaN, 0, 1, 1, 80, 60); } catch (ArgumentOutOfRangeException) { failed = true; }
        Check(failed, "Non-finite crop accepted");
    }
    private static string GridPng(string name, int width = 80, int height = 60)
    {
        var path = Path.Combine(folder, name); var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        { var i = (y * width + x) * 4; pixels[i] = (byte)(x % 255); pixels[i + 1] = (byte)(y % 255); pixels[i + 2] = 200; pixels[i + 3] = (byte)(x < width / 2 ? 0 : 255); }
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(path); encoder.Save(file); return path;
    }
    private static void ImageCrop()
    {
        var path = GridPng("قص إحداثيات.png"); var original = SHA256.HashData(File.ReadAllBytes(path));
        var file = FileClassifier.Inspect(path); var request = new ConversionRequest(file, "CROP", new(30, 12, 25, 28));
        var result = Service.ConvertAsync(request).GetAwaiter().GetResult();
        using (var image = new MagickImage(result.OutputPath))
        {
            Check(image.Width == 25 && image.Height == 28, "Crop dimensions");
            var pixel = image.GetPixels().GetPixel(15, 5).ToColor()!;
            Check(pixel.R == 200 && pixel.G == 17 && pixel.B == 45 && pixel.A == 255, "Cropped pixels don't match selected source coordinates");
            Check(image.GetPixels().GetPixel(0, 0).ToColor()!.A == 0, "Crop alpha was flattened");
        }
        var second = Service.ConvertAsync(request).GetAwaiter().GetResult(); Check(second.OutputPath != result.OutputPath, "Crop overwrote output");
        foreach (var invalid in new ConversionRequest[] { new(file, "CROP"), new(file, "CROP", new(79, 0, 20, 10)) })
        {
            var failed = false; try { Service.ConvertAsync(invalid).GetAwaiter().GetResult(); } catch (ArgumentException) { failed = true; }
            Check(failed, "Invalid crop was saved");
        }
        var oriented = Path.Combine(folder, "اتجاه exif.jpg");
        using (var image = new MagickImage(MagickColors.Red, 80, 40))
        {
            var exif = new ExifProfile(); exif.SetValue(ExifTag.Orientation, (ushort)6); image.SetProfile(exif); image.Orientation = ImageMagick.Orientation.RightTop; image.Write(oriented);
        }
        var preview = Service.GetImagePreviewAsync(FileClassifier.Inspect(oriented)).GetAwaiter().GetResult();
        Check(preview.Width == 40 && preview.Height == 80, "Preview didn't apply EXIF orientation");
        var orientedCrop = Service.ConvertAsync(new ConversionRequest(FileClassifier.Inspect(oriented), "CROP", new(0, 40, 30, 40))).GetAwaiter().GetResult();
        Check(orientedCrop.Width == 30 && orientedCrop.Height == 40, "Crop doesn't use oriented preview coordinates");
        using (var check = new MagickImage(orientedCrop.OutputPath)) Check(check.Orientation is ImageMagick.Orientation.TopLeft or ImageMagick.Orientation.Undefined, "Crop retained a stale orientation tag");
        Check(original.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Crop modified original");
        Check(!Directory.EnumerateFileSystemEntries(folder, ".naqla-*").Any(), "Crop temporary leak");
    }
    private static string SolidImage(string directory, string name, MagickColor color, uint width, uint height)
    {
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, name);
        using var image = new MagickImage(color, width, height); image.Write(path); return path;
    }
    private static void ImageBatch()
    {
        var first = SolidImage(folder, "دفعة أول.png", MagickColors.Red, 64, 32);
        var second = SolidImage(folder, "دفعة ثان.jpg", MagickColors.Green, 32, 64);
        var payload = FileDragPayload.Inspect(new[] { second, first });
        Check(payload.Files[0].FullPath == second && payload.PreviewFile.Size == new FileInfo(first).Length + new FileInfo(second).Length, "Batch order/size");
        Check(payload.Matches(new[] { second, first }) && !payload.Matches(new[] { first, second }), "Drop payload order validation");
        var wheel = new PixelWheel(Service.CanExecute); wheel.SetSelection(payload);
        Check(wheel.OptionsCount > 1 && wheel.HubLabel == "2 ملف", "Multi-image conversions/count missing");
        Check(WheelOptions.For(payload, WheelMode.Convert, Service.CanExecute).Any(x => x.Id == "PDF"), "Per-image PDF action missing");
        wheel.SetMode(true, false); Check(WheelOptions.For(payload, WheelMode.Tools, Service.CanExecute).Any(x => x.Id == "MERGE"), "Explicit Merge PDF tool missing");
        Check(!Service.CanExecute(FileDragPayload.Inspect(new[] { first }), "MERGE"), "Single-file merge advertised as actionable");
        foreach (var paths in new[] { new[] { first, first }, Array.Empty<string>(), Enumerable.Repeat(first, 201).ToArray() })
        {
            var rejected = false; try { FileDragPayload.Inspect(paths); } catch (NotSupportedException) { rejected = true; }
            Check(rejected, "Invalid batch accepted");
        }
    }
    private static void MergeImages()
    {
        var subfolder = Path.Combine(folder, "مجلد الدمج");
        var green = SolidImage(subfolder, "صفحة أولى.jpg", MagickColors.Lime, 40, 80);
        var red = SolidImage(folder, "صفحة ثانية.png", MagickColors.Red, 80, 40);
        var blue = SolidImage(folder, "صفحة ثالثة.webp", MagickColors.Blue, 64, 48);
        var files = new[] { green, red, blue }.Select(FileClassifier.Inspect).ToArray();
        var hashes = files.Select(x => SHA256.HashData(File.ReadAllBytes(x.FullPath))).ToArray();
        var occupied = Path.Combine(subfolder, "صفحة أولى merged.pdf"); File.WriteAllText(occupied, "preserve this existing file");
        var merged = Service.ConvertAsync(new ConversionRequest(files, "MERGE")).GetAwaiter().GetResult();
        Check(merged.Items == 3 && Path.GetDirectoryName(merged.OutputPath) == subfolder && merged.OutputPath != occupied, "Merge location/name/count");
        Check(File.ReadAllText(occupied) == "preserve this existing file", "Merge overwrote collision");
        using (var document = PdfDocument.Open(merged.OutputPath)) Check(document.NumberOfPages == 3, "Merge lost pages");
        using (var input = File.OpenRead(merged.OutputPath))
        {
            var index = 0;
            foreach (var bitmap in PDFtoImage.Conversion.ToImages(input, options: new(Dpi: 72)))
            {
                using (bitmap)
                {
                    var pixel = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
                    Check(index switch { 0 => pixel.Green > 220 && pixel.Red < 30, 1 => pixel.Red > 220 && pixel.Blue < 30, _ => pixel.Blue > 220 && pixel.Red < 30 }, "PDF page color/order changed");
                }
                index++;
            }
            Check(index == 3, "PDF pages didn't all decode");
        }
        var viaPdf = new BatchProcessor(Service).RunAsync(new(files, "PDF")).GetAwaiter().GetResult();
        Check(viaPdf.Succeeded == 3 && viaPdf.Jobs.All(x => x.Output!.Items == 1), "Batch PDF must create a separate PDF per image");
        for (var i = 0; i < files.Length; i++) Check(hashes[i].SequenceEqual(SHA256.HashData(File.ReadAllBytes(files[i].FullPath))), "Merge modified source");
    }
    private static void MergeFailures()
    {
        var directory = Path.Combine(folder, "merge-cancellation"); Directory.CreateDirectory(directory);
        var good = SolidImage(directory, "good.png", MagickColors.Red, 200, 200);
        var corrupt = Path.Combine(directory, "broken.png"); File.WriteAllText(corrupt, "not an image");
        var failed = false;
        try { Service.ConvertAsync(new ConversionRequest(new[] { good, corrupt }.Select(FileClassifier.Inspect).ToArray(), "MERGE")).GetAwaiter().GetResult(); } catch (Exception) { failed = true; }
        Check(failed && !Directory.GetFiles(directory, "*.pdf").Any() && !Directory.EnumerateFileSystemEntries(directory, ".naqla-*").Any(), "Failed merge published/leaked a partial PDF");
        var pages = Enumerable.Range(0, 8).Select(i => SolidImage(directory, $"large-{i}.png", MagickColors.Blue, 1200, 800)).Select(FileClassifier.Inspect).ToArray();
        using var cancellation = new CancellationTokenSource();
        var operation = Service.ConvertAsync(new ConversionRequest(pages, "MERGE"), cancellation.Token);
        var observer = Task.Run(async () =>
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!Directory.EnumerateFiles(directory, ".naqla-*.pdf").Any() && !operation.IsCompleted && watch.ElapsedMilliseconds < 3000) await Task.Delay(2);
            cancellation.Cancel();
        });
        var cancelled = false; try { operation.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
        observer.GetAwaiter().GetResult();
        Check(cancelled && !Directory.GetFiles(directory, "*.pdf").Any() && !Directory.EnumerateFileSystemEntries(directory, ".naqla-*").Any(), "Cancelled merge published/leaked output");
    }
    private static void HeicFeatures()
    {
        var directory = Path.Combine(folder, "مجلد HEIC عربي"); Directory.CreateDirectory(directory);
        var source = CreatePng("شفافية HEIC.png");
        var heic = ConvertFile(source, "HEIC");
        var signature = Encoding.ASCII.GetString(File.ReadAllBytes(heic.OutputPath).Take(100).ToArray());
        Check(signature.Contains("ftyp") && (signature.Contains("heic") || signature.Contains("heix")) && !signature.Contains("avif"), "HEIC is not a genuine HEVC HEIF file");
        using (var decoded = new MagickImage(heic.OutputPath))
        {
            Check(decoded.HasAlpha && decoded.GetPixels().GetPixel(0, 0).ToColor()!.A < 10, "HEIC transparency was not preserved");
            var center = decoded.GetPixels().GetPixel(16, 16).ToColor()!; Check(center.R > 210 && center.G < 40 && center.A > 245, "HEIC real source pixels lost");
        }
        foreach (var size in new[] { (1u, 1u), (17u, 29u), (65u, 33u) })
        {
            var path = SolidImage(directory, $"صورة {size.Item1} {size.Item2}.png", MagickColors.Green, size.Item1, size.Item2);
            var encoded = ConvertFile(path, "HEIC"); Check(encoded.Width == size.Item1 && encoded.Height == size.Item2, "Odd/tiny HEIC dimensions");
        }
        var original = SHA256.HashData(File.ReadAllBytes(heic.OutputPath));
        var cropped = Service.ConvertAsync(new ConversionRequest(FileClassifier.Inspect(heic.OutputPath), "CROP", new(8, 8, 16, 16))).GetAwaiter().GetResult();
        Check(cropped.Width == 16 && Path.GetExtension(cropped.OutputPath) == ".heic", "HEIC crop is not genuine same-format output");
        Check(ConvertFile(heic.OutputPath, "ROTATE").Width == 32, "HEIC rotate");
        Check(ConvertFile(heic.OutputPath, "STRIP").Width == 32, "HEIC metadata tool");
        Check(original.SequenceEqual(SHA256.HashData(File.ReadAllBytes(heic.OutputPath))), "HEIC tools modified source");
        var missing = new LocalConversionService(heifDirectory: Path.Combine(folder, "missing-heif-engine"));
        Check(!missing.CanExecute(FileClassifier.Inspect(source), "HEIC"), "Missing encoder falsely advertised");
        Check(!Directory.EnumerateFileSystemEntries(directory, ".naqla-*").Any(), "Arabic-path HEIC temporary leak");
    }
    private static void LosslessImageTools()
    {
        var path = Path.Combine(folder, "uncompressed-source.png");
        using (var image = new MagickImage(MagickColors.CornflowerBlue, 512, 256))
        { using var output = File.Create(path); image.Write(output, new PngWriteDefines { CompressionLevel = 0 }); }
        var compressed = ConvertFile(path, "COMPRESS"); Check(new FileInfo(compressed.OutputPath).Length < new FileInfo(path).Length, "PNG compression");
        using (var before = new MagickImage(path)) using (var after = new MagickImage(compressed.OutputPath))
            Check(before.ToByteArray(MagickFormat.Rgba).SequenceEqual(after.ToByteArray(MagickFormat.Rgba)), "Lossless PNG pixels changed");
        var bmp = SolidImage(folder, "compress-bmp.bmp", MagickColors.CornflowerBlue, 512, 256);
        var bmpResult = ConvertFile(bmp, "COMPRESS"); Check(Path.GetExtension(bmpResult.OutputPath) == ".png" && new FileInfo(bmpResult.OutputPath).Length < new FileInfo(bmp).Length, "BMP lossless PNG compression");
        var tiff = Path.Combine(folder, "uncompressed-tiff.tiff");
        using (var image = new MagickImage(MagickColors.Red, 512, 256)) { image.Settings.Compression = CompressionMethod.NoCompression; image.Write(tiff); }
        var tiffResult = ConvertFile(tiff, "COMPRESS"); Check(new FileInfo(tiffResult.OutputPath).Length < new FileInfo(tiff).Length, "TIFF compression");
        foreach (var format in new[] { "PNG", "JPG", "WEBP", "AVIF", "TIFF", "HEIC" })
        {
            var tagged = Path.Combine(folder, "metadata-" + format + ".png");
            using (var image = new MagickImage(MagickColors.Red, 64, 48))
            {
                var exif = new ExifProfile(); exif.SetValue(ExifTag.Artist, "private-artist-marker"); image.SetProfile(exif);
                image.SetProfile(new XmpProfile(Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description xmlns:dc='http://purl.org/dc/elements/1.1/' dc:creator='private-xmp-marker'/></rdf:RDF></x:xmpmeta>")));
                image.Write(tagged);
            }
            var source = format == "PNG" ? tagged : ConvertFile(tagged, format).OutputPath;
            var result = ConvertFile(source, "STRIP");
            using var decoded = new MagickImage(result.OutputPath);
            Check(decoded.GetExifProfile() is null && decoded.GetXmpProfile() is null, "Metadata profile retained: " + format);
        }
    }
    private static void ImageConversionMatrix()
    {
        var source = GridPng("matrix-image.png", 64, 48);
        var inputs = new List<string> { source };
        foreach (var target in new[] { "JPG", "WEBP", "AVIF", "TIFF", "HEIC" }) inputs.Add(ConvertFile(source, target).OutputPath);
        inputs.Add(SolidImage(folder, "matrix.bmp", MagickColors.Red, 64, 48));
        var rows = new List<object>();
        foreach (var input in inputs)
        {
            var file = FileClassifier.Inspect(input); var hash = SHA256.HashData(File.ReadAllBytes(input));
            foreach (var target in new[] { "PNG", "JPG", "PDF", "AVIF", "TIFF", "HEIC" }.Where(x => x != file.Extension))
            {
                var output = ConvertFile(input, target);
                Check(File.Exists(output.OutputPath) && output.Width == 64 && output.Height == 48, file.Extension + " → " + target);
                if (target == "PDF") { using var pdf = PdfDocument.Open(output.OutputPath); Check(pdf.NumberOfPages == 1, "Matrix PDF page missing"); }
                else { using var decoded = new MagickImage(output.OutputPath); Check(decoded.Width == 64 && decoded.Height == 48, "Matrix decode"); }
                rows.Add(new { Source = file.Extension, Target = target, Passed = true });
            }
            Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(input))), "Matrix changed source");
        }
        File.WriteAllText(Path.Combine(folder, "image-format-matrix.json"), System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
    private static void HeicCancellation()
    {
        var directory = Path.Combine(folder, "إلغاء HEIC"); Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "noise.png");
        using (var image = new MagickImage(MagickColors.Red, 1536, 1024)) { image.AddNoise(NoiseType.Gaussian); image.Write(source); }
        var hash = SHA256.HashData(File.ReadAllBytes(source));
        using var cancellation = new CancellationTokenSource();
        var task = Service.ConvertAsync(FileClassifier.Inspect(source), "HEIC", cancellation.Token);
        var observedProcess = false;
        var observer = Task.Run(async () =>
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted && watch.ElapsedMilliseconds < 10000)
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("heif-enc");
                observedProcess = processes.Length > 0; foreach (var process in processes) process.Dispose();
                if (observedProcess) break;
                await Task.Delay(5);
            }
            cancellation.Cancel();
        });
        var cancelled = false; try { task.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
        observer.GetAwaiter().GetResult();
        Check(observedProcess && cancelled, "HEIC process cancellation was not exercised");
        Check(!File.Exists(Path.ChangeExtension(source, ".heic")) && !Directory.EnumerateFileSystemEntries(directory, ".naqla-*").Any(), "Cancelled HEIC leaked/published output");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "HEIC cancellation changed source");
    }
    private static void CropEditorRender()
    {
        _ = Application.Current ?? new Application();
        var preview = Service.GetImagePreviewAsync(FileClassifier.Inspect(Path.Combine(folder, "قص إحداثيات.png"))).GetAwaiter().GetResult();
        var editor = new CropWindow(preview); var content = (FrameworkElement)editor.Content;
        content.Measure(new Size(780, 590)); content.Arrange(new Rect(0, 0, 780, 590)); content.UpdateLayout();
        Check(editor.Selection == new CropRectangle(0, 0, 80, 60), "Editor source dimensions");
        var bitmap = new RenderTargetBitmap(780, 590, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var output = File.Create(Path.Combine(folder, "native-crop-editor.png"))) encoder.Save(output);
        Check(FindTextBoxes(content).Count() == 4, "Crop pixel parameter fields missing"); editor.Close();
    }
    private static IEnumerable<TextBox> FindTextBoxes(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is TextBox box) yield return box;
            foreach (var nested in FindTextBoxes(child)) yield return nested;
        }
    }
}
