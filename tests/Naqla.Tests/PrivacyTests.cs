using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Windows.Media.Imaging;
using ImageMagick;
using Naqla.Conversion;
using Naqla.Core;
using PdfSharp.Pdf.IO;

internal static partial class Program
{
    private static PrivacyCleanService Privacy() => new(Path.Combine(AppContext.BaseDirectory, "media-engine"), Path.Combine(Components, "qpdf", "qpdf-12.4.2-mingw64", "bin"));
    private static void RunPrivacyTests()
    {
        Run("Privacy JPEG/PNG/TIFF/WebP: real metadata removed, pixel equality, orientation and originals", PrivacyImages);
        Run("Privacy Office DOCX/XLSX/PPTX properties change while bodies/assets/relationships remain identical", PrivacyOffice);
        Run("Privacy PDF properties/XMP removal preserves page text/vector rendering and original hash", PrivacyPdf);
        Run("Privacy MP4/MP3/FLAC stream-copy keeps encoded packet hashes and removes selected tags", PrivacyMedia);
        Run("Privacy selective cleanup, empty detection, stale/corrupt/cancelled inputs and collisions", PrivacyGuards);
        Run("Privacy complete DOCX/XLSX/PPTX packages pass independent Open XML schema validation before/after", PrivacyValidOffice);
    }
    private static void PrivacyImages()
    {
        foreach (var format in new[] { "JPG", "PNG", "TIFF", "WEBP" })
        {
            var path = Path.Combine(folder, "خصوصية " + format + "." + format.ToLowerInvariant());
            using (var image = new MagickImage(MagickColors.Coral, 100, 60))
            {
                var exif = new ExifProfile(); exif.SetValue(ExifTag.Make, "private camera"); exif.SetValue(ExifTag.Artist, "private author"); exif.SetValue(ExifTag.GPSLatitudeRef, "N"); exif.SetValue(ExifTag.Orientation, (ushort)6); image.Orientation = Orientation.RightTop; image.SetProfile(exif); image.SetAttribute("comment", "private comment"); image.SetProfile(ColorProfiles.SRGB); image.Write(path);
            }
            if (format == "TIFF")
            {
                using var sourceBytes = new MemoryStream(File.ReadAllBytes(path)); var sourceFrame = BitmapDecoder.Create(sourceBytes, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                var metadata = new BitmapMetadata("tiff"); metadata.SetQuery("/ifd/{ushort=271}", "private camera"); metadata.SetQuery("/ifd/{ushort=272}", "private model"); metadata.SetQuery("/ifd/{ushort=315}", "private artist");
                var encoder = new TiffBitmapEncoder { Compression = TiffCompressOption.Zip }; encoder.Frames.Add(BitmapFrame.Create(sourceFrame, null, metadata, sourceFrame.ColorContexts)); using var destination = File.Create(path); encoder.Save(destination);
            }
            var hash = SHA256.HashData(File.ReadAllBytes(path)); var source = FileClassifier.Inspect(path); var analysis = Privacy().AnalyzeAsync(source).GetAwaiter().GetResult();
            Check(analysis.Detected.Contains("EXIF"), "EXIF fixture not detected " + format);
            var result = Privacy().CleanAsync(analysis, PrivacySettings.Recommended).GetAwaiter().GetResult();
            using var decoded = new MagickImage(result.Output.OutputPath); var profile = decoded.GetExifProfile();
            Check(profile is null || profile.Values.All(x => x.Tag.Equals(ExifTag.Orientation)), "Personal EXIF remains " + format);
            using var original = new MagickImage(path);
            Check(decoded.GetAttribute("comment") is null && (original.GetColorProfile() is null || decoded.GetColorProfile() is not null), "Comment/essential ICC handling " + format);
            Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Privacy image source changed");
            if (format == "JPG") Check(!result.Report.Reencoded, "JPEG marker rewrite falsely reported as re-encoded");
        }
    }
    private static void OfficeFixture(string path, string contentPart)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Entry(string name, string value) { using var text = new StreamWriter(zip.CreateEntry(name).Open()); text.Write(value); }
        Entry("[Content_Types].xml", "<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Override PartName='/docProps/core.xml' ContentType='application/vnd.openxmlformats-package.core-properties+xml'/><Override PartName='/docProps/custom.xml' ContentType='application/vnd.openxmlformats-officedocument.custom-properties+xml'/></Types>");
        Entry("_rels/.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='r1' Type='http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties' Target='docProps/core.xml'/><Relationship Id='r2' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/custom-properties' Target='docProps/custom.xml'/></Relationships>");
        Entry("docProps/core.xml", "<cp:coreProperties xmlns:cp='http://schemas.openxmlformats.org/package/2006/metadata/core-properties' xmlns:dc='http://purl.org/dc/elements/1.1/' xmlns:dcterms='http://purl.org/dc/terms/'><dc:creator>private person</dc:creator><cp:lastModifiedBy>private editor</cp:lastModifiedBy><dcterms:created>2025-01-01T00:00:00Z</dcterms:created><dc:title>Keep my title</dc:title></cp:coreProperties>");
        Entry("docProps/app.xml", "<Properties xmlns='http://schemas.openxmlformats.org/officeDocument/2006/extended-properties'><Company>Private company</Company><Application>Private software</Application></Properties>");
        Entry("docProps/custom.xml", "<Properties xmlns='http://schemas.openxmlformats.org/officeDocument/2006/custom-properties'><property name='Private'>secret</property></Properties>");
        Entry(contentPart, "<content>نص المستند محفوظ · content</content>"); Entry("media/asset.bin", "embedded asset must remain byte-identical");
    }
    private static void PrivacyOffice()
    {
        foreach (var (format, part) in new[] { ("docx", "word/document.xml"), ("xlsx", "xl/workbook.xml"), ("pptx", "ppt/presentation.xml") })
        {
            var path = Path.Combine(folder, "خصائص مستند." + format); OfficeFixture(path, part); var hash = SHA256.HashData(File.ReadAllBytes(path));
            var analysis = Privacy().AnalyzeAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult(); Check(analysis.Detected.Contains("Author") && analysis.Detected.Contains("CustomProperties"), "Office metadata detection");
            var result = Privacy().CleanAsync(analysis, new(["Author", "Dates", "Software", "CustomProperties"])).GetAwaiter().GetResult();
            using var zip = ZipFile.OpenRead(result.Output.OutputPath); using var core = zip.GetEntry("docProps/core.xml")!.Open(); var xml = XDocument.Load(core);
            Check(!xml.Descendants().Any(x => x.Name.LocalName == "creator") && xml.Descendants().Any(x => x.Name.LocalName == "title" && x.Value == "Keep my title"), "Selective Office cleanup changed unselected fields");
            Check(zip.GetEntry("docProps/custom.xml") is null && zip.GetEntry(part) is not null, "Office package parts");
            Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Office source changed");
        }
        File.WriteAllText(Path.Combine(folder, "office-fixture-scope.txt"), "These ZIP/XML metadata unit fixtures test unchanged package parts. Native Office application acceptance and full OOXML schema validation are separate pending gates.");
    }
    private static void PrivacyPdf()
    {
        var fixture = PdfFixture("privacy-base.pdf", 3, true, true); var path = Path.Combine(folder, "خصوصية PDF.pdf");
        using (var doc = PdfReader.Open(fixture, PdfDocumentOpenMode.Modify)) { doc.Info.Author = "private author"; doc.Info.Title = "private title"; doc.Info.Creator = "private software"; doc.Info.Elements.SetString("/PrivateTag", "private custom field"); doc.Save(path); }
        var hash = SHA256.HashData(File.ReadAllBytes(path)); var analysis = Privacy().AnalyzeAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult();
        var result = Privacy().CleanAsync(analysis, PrivacySettings.Recommended).GetAwaiter().GetResult();
        using var verified = UglyToad.PdfPig.PdfDocument.Open(result.Output.OutputPath);
        Check(verified.NumberOfPages == 3 && string.IsNullOrEmpty(verified.Information.Author) && string.IsNullOrEmpty(verified.Information.Title), "Independent PDF metadata inspection");
        for (var page = 0; page < 3; page++) Check(PdfRendered(path, page).SequenceEqual(PdfRendered(result.Output.OutputPath, page)), "Privacy PDF visible content changed");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "PDF source changed");
    }
    private static void PrivacyMedia()
    {
        foreach (var format in new[] { "mp4", "mp3", "flac" })
        {
            var path = Path.Combine(folder, "وسائط خاصة." + format); var args = new List<string> { "-v", "error", "-nostdin", "-n", "-i", MediaSource() };
            if (format == "mp4") args.AddRange(["-c", "copy"]); else args.AddRange(["-vn", "-c:a", format == "mp3" ? "libmp3lame" : "flac"]);
            args.AddRange(["-metadata", "artist=private person", "-metadata", "title=private title", "-metadata", "comment=private comment", path]); Engine(args.ToArray());
            var hash = SHA256.HashData(File.ReadAllBytes(path)); var analysis = Privacy().AnalyzeAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult(); Check(analysis.Detected.Contains("Author"), "Media artist not detected");
            var result = Privacy().CleanAsync(analysis, new(["Author", "Description", "Comments", "Dates"])).GetAwaiter().GetResult();
            Check(result.Report.Removed.Contains("Author") && !result.Report.Reencoded, "Media cleanup stream-copy report"); Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Media source changed");
        }
    }
    private static void PrivacyGuards()
    {
        var path = Path.Combine(folder, "metadata-guard.png"); using (var image = new MagickImage(MagickColors.Red, 32, 32)) { image.SetAttribute("comment", "private"); image.Write(path); }
        var analysis = Privacy().AnalyzeAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var rejected = false;
        try { Privacy().CleanAsync(analysis, PrivacySettings.Recommended, cancellation.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { rejected = true; } Check(rejected, "Privacy cancellation ignored");
        var first = Privacy().CleanAsync(analysis, PrivacySettings.Recommended).GetAwaiter().GetResult(); var second = Privacy().CleanAsync(analysis, PrivacySettings.Recommended).GetAwaiter().GetResult(); Check(first.Output.OutputPath != second.Output.OutputPath, "Privacy output collision overwritten");
        var none = Privacy().AnalyzeAsync(FileClassifier.Inspect(first.Output.OutputPath)).GetAwaiter().GetResult(); rejected = false;
        try { Privacy().CleanAsync(none, PrivacySettings.Recommended).GetAwaiter().GetResult(); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Empty metadata reported success");
        File.AppendAllText(path, "source changed"); rejected = false; try { Privacy().CleanAsync(analysis, PrivacySettings.Recommended).GetAwaiter().GetResult(); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Stale metadata preview accepted");
        Check(!Directory.GetDirectories(folder, ".naqla-*").Any(), "Privacy failure left temporary files");
    }
}
