using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImageMagick;
using Naqla.Conversion;
using Naqla.Core;

internal static partial class Program
{
    private static void BatchStress()
    {
        var directory = Path.Combine(folder, "stress-صور"); Directory.CreateDirectory(directory); var files = new List<DraggedFile>(); var hashes = new List<string>();
        for (var i = 0; i < 200; i++)
        {
            var path = Path.Combine(directory, $"صورة-{i:000}.png"); using (var image = new MagickImage(i % 2 == 0 ? MagickColors.Coral : MagickColors.CornflowerBlue, 64, 48)) image.Write(path);
            files.Add(FileClassifier.Inspect(path)); hashes.Add(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        }
        var queue = new BatchProcessor(new LocalConversionService()); var results = new List<object>();
        foreach (var count in new[] { 10, 50, 50, 200 })
        {
            var watch = System.Diagnostics.Stopwatch.StartNew(); var result = queue.RunAsync(new(files.Take(count).ToArray(), "JPG")).GetAwaiter().GetResult();
            Check(result.Succeeded == count && result.Jobs.Count == count && result.Cancelled == 0 && result.Failed == 0, "Stress batch skipped or invented jobs");
            foreach (var job in result.Jobs) { using var decoded = new MagickImage(job.Output!.OutputPath); Check(decoded.Format == MagickFormat.Jpeg && decoded.Width == 64 && decoded.Height == 48, "Stress output codec/dimensions"); }
            results.Add(new { Files = count, Milliseconds = watch.ElapsedMilliseconds, Succeeded = result.Succeeded });
        }
        for (var i = 0; i < files.Count; i++) Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(files[i].FullPath))) == hashes[i], "Stress modified original");
        Check(!Directory.GetFiles(directory, ".naqla-*").Any(), "Stress left temporary image files");
        File.WriteAllText(Path.Combine(folder, "stress-results.json"), JsonSerializer.Serialize(new { Status = "PASSED", ActualConversions = 310, Results = results, Scope = "Small generated image I/O and repeated batch jobs. Long 4K media/hours-long session testing remains manual." }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static void PrivacyMetadataCoverage()
    {
        var deep = Path.Combine(folder, "عمق 16.tiff"); var pixels = new ushort[] { 1234, 2345, 3456, 4567, 5678, 6789, 7890, 8901 };
        var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(4, 2, 96, 96, System.Windows.Media.PixelFormats.Gray16, null, pixels, 8);
        var metadata = new System.Windows.Media.Imaging.BitmapMetadata("tiff"); metadata.SetQuery("/ifd/{ushort=315}", "Synthetic private author");
        var tiff = new System.Windows.Media.Imaging.TiffBitmapEncoder(); tiff.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap, null, metadata, null)); using (var stream = File.Create(deep)) tiff.Save(stream);
        var deepAnalysis = Privacy().AnalyzeAsync(FileClassifier.Inspect(deep)).GetAwaiter().GetResult(); var blocked = false;
        try { Privacy().CleanAsync(deepAnalysis, PrivacySettings.Recommended).GetAwaiter().GetResult(); } catch (NotSupportedException) { blocked = true; }
        Check(blocked && !File.Exists(Path.Combine(folder, "عمق 16-clean.tiff")), "High-bit-depth privacy re-encoding silently reduced precision");
        foreach (var format in new[] { "JPG", "AVIF" })
        {
            var path = Path.Combine(folder, "بيانات GPS." + format.ToLowerInvariant());
            using (var image = new MagickImage(MagickColors.Coral, 100, 60))
            {
                var exif = new ExifProfile(); exif.SetValue(ExifTag.GPSLatitudeRef, "N"); exif.SetValue(ExifTag.GPSLatitude, [new Rational(31), new Rational(57), new Rational(0)]); exif.SetValue(ExifTag.GPSLongitudeRef, "E"); exif.SetValue(ExifTag.GPSLongitude, [new Rational(35), new Rational(56), new Rational(0)]); image.SetProfile(exif);
                image.SetProfile(new XmpProfile(Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description xmlns:dc='http://purl.org/dc/elements/1.1/' dc:creator='Synthetic private fixture author'/></rdf:RDF></x:xmpmeta>")));
                var iptc = new IptcProfile(); iptc.SetValue(IptcTag.Caption, "Synthetic private caption"); image.SetProfile(iptc); image.Write(path);
            }
            var source = FileClassifier.Inspect(path); using var original = new MagickImage(path);
            Check(original.GetExifProfile()?.GetValue(ExifTag.GPSLatitude) is not null && original.GetXmpProfile() is not null, "Actual encoded GPS/XMP fixture missing: " + format);
            var service = Privacy(); var analysis = service.AnalyzeAsync(source).GetAwaiter().GetResult(); Check(analysis.Detected.Contains("EXIF") && analysis.Detected.Contains("XMP"), "Metadata categories not detected");
            if (format == "JPG") Check(analysis.Detected.Contains("IPTC"), "Actual JPEG IPTC fixture missing");
            var hash = SHA256.HashData(File.ReadAllBytes(path));
            if (format == "AVIF")
            {
                using var frames = RasterImages.Open(source); var image = frames[0]; var space = image.ColorSpace; var gamma = image.Gamma; var profiles = image.ProfileNames.ToArray();
                var before = image.ToByteArray(MagickFormat.Rgba); var pixelSpace = image.ColorSpace;
                using var copy = image.Clone(); foreach (var name in copy.ProfileNames.ToArray()) copy.RemoveProfile(name);
                var rawPath = Path.Combine(folder, "avif-debug.png"); copy.Settings.Compression = CompressionMethod.Zip; copy.Write(rawPath, MagickFormat.Png);
                using var after = new MagickImage(rawPath); var decoded = after.ToByteArray(MagickFormat.Rgba);
                File.WriteAllText(Path.Combine(folder, "avif-debug.json"), JsonSerializer.Serialize(new { Space = space.ToString(), PixelSpace = pixelSpace.ToString(), Gamma = gamma, Profiles = profiles, AfterSpace = after.ColorSpace.ToString(), Before = before.Take(16).ToArray(), After = decoded.Take(16).ToArray(), Equal = before.SequenceEqual(decoded) }, new JsonSerializerOptions { WriteIndented = true }));
            }
            if (format == "AVIF") { var refused = false; try { service.CleanAsync(analysis, PrivacySettings.Recommended).GetAwaiter().GetResult(); } catch (NotSupportedException) { refused = true; } Check(refused, "AVIF fallback happened without explicit approval"); }
            var output = service.CleanAsync(analysis, new(PrivacySettings.KnownCategories, AllowLosslessPngFallback: format == "AVIF")).GetAwaiter().GetResult();
            using var cleaned = new MagickImage(output.Output.OutputPath); Check(cleaned.GetExifProfile()?.GetValue(ExifTag.GPSLatitude) is null && cleaned.GetXmpProfile() is null && cleaned.GetIptcProfile() is null, "GPS/XMP/IPTC metadata remained");
            Check(!cleaned.AttributeNames.Any(x => x.StartsWith("exif:", StringComparison.OrdinalIgnoreCase) && !x.Equals("exif:Orientation", StringComparison.OrdinalIgnoreCase) || x.StartsWith("xmp:", StringComparison.OrdinalIgnoreCase) || x.StartsWith("iptc:", StringComparison.OrdinalIgnoreCase)), "Derived private metadata was reserialized as PNG text");
            if (format == "AVIF") Check(cleaned.Format == MagickFormat.Png && Path.GetExtension(output.Output.OutputPath) == ".png", "Explicit fallback did not produce a genuine lossless PNG");
            Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Metadata coverage changed source");
        }
    }
}
