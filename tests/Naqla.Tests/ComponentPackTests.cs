using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ImageMagick;
using ImageMagick.Drawing;
using Naqla.Conversion;
using Naqla.Core;
using PdfSharp.Pdf.IO;

internal static partial class Program
{
    private static void ComponentPackAcceptance()
    {
        var repository = Path.GetFullPath(Path.Combine(Components, "../..")); var packs = Path.Combine(repository, "artifacts", "component-packs");
        var root = Path.Combine(folder, "installed-packs-full"); var store = new ComponentStore(root); var timings = new List<object>();
        foreach (var feature in new[] { "ocr", "background", "speech", "archives", "pdf-metadata", "office" })
        {
            var watch = Stopwatch.StartNew(); var zip = Path.Combine(packs, "Naqla-component-" + feature + ".zip");
            store.InstallAsync(zip).GetAwaiter().GetResult(); Check(store.Status().Single(x => x.Feature == feature).Installed, "Actual imported pack status: " + feature);
            timings.Add(new { Feature = feature, InstallMilliseconds = watch.ElapsedMilliseconds, CompressedBytes = new FileInfo(zip).Length }); Console.WriteLine("PACK imported: " + feature);
        }
        var service = new LocalConversionService(componentDirectory: root, pluginDirectory: Path.Combine(folder, "plugins"));
        var ocrPath = Path.Combine(folder, "فحص OCR.png"); OcrFixture(ocrPath, "Naqla offline component 123", System.Windows.FlowDirection.LeftToRight); var file = FileClassifier.Inspect(ocrPath);
        var recognized = service.Models.RecognizeAsync(file, "eng").GetAwaiter().GetResult(); Check(recognized.Text.Contains("offline", StringComparison.OrdinalIgnoreCase), "Imported OCR data/worker failed");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ocrPath)));
        var saved = service.ConvertAsync(new([file], "OCR", Model: new(hash, Ocr: recognized))).GetAwaiter().GetResult(); Check(File.ReadAllText(saved.OutputPath) == recognized.Text, "Imported OCR dispatch/save");
        var photo = Path.Combine(folder, "قناع الحزمة.png"); using (var image = new MagickImage(MagickColors.White, 400, 300)) { new Drawables().FillColor(MagickColors.Coral).StrokeColor(MagickColors.DarkRed).Circle(200, 150, 200, 65).Draw(image); image.Write(photo); }
        var background = service.Models.SaveBackgroundAsync(FileClassifier.Inspect(photo)).GetAwaiter().GetResult(); using (var result = new MagickImage(background.OutputPath)) Check(result.HasAlpha && result.Width == 400, "Imported segmentation model output");
        var speech = Path.Combine(folder, "خطاب الحزمة.wav"); File.Copy(Path.Combine(Components, "fixtures", "jfk.wav"), speech);
        var transcript = service.Models.TranscribeAsync(FileClassifier.Inspect(speech), "en").GetAwaiter().GetResult(); Check(string.Join(' ', transcript.Cues.Select(x => x.Text)).Contains("country", StringComparison.OrdinalIgnoreCase), "Imported speech native/CRT/model closure");
        service.Models.SaveSubtitlesAsync(FileClassifier.Inspect(speech), transcript, true).GetAwaiter().GetResult();
        var archive = service.ConvertAsync(new([file], "ARCHIVE", ArchiveFormat: "7Z")).GetAwaiter().GetResult();
        var extracted = service.ConvertAsync(FileClassifier.Inspect(archive.OutputPath), "EXTRACT").GetAwaiter().GetResult(); Check(File.Exists(Path.Combine(extracted.OutputPath, Path.GetFileName(ocrPath))), "Imported 7za execution/independent extraction");
        var pdf = PdfFixture("pack-source.pdf", 2); var privatePdf = Path.Combine(folder, "خصائص الحزمة.pdf"); using (var document = PdfReader.Open(pdf, PdfDocumentOpenMode.Modify)) { document.Info.Author = "Disposable fixture author"; document.Save(privatePdf); }
        var analysis = service.Privacy.AnalyzeAsync(FileClassifier.Inspect(privatePdf)).GetAwaiter().GetResult();
        var clean = service.ConvertAsync(new([analysis.Source], "PRIVACY", Privacy: new([analysis], new(["Author"])))).GetAwaiter().GetResult(); Check(clean.Privacy is { Removed.Count: > 0 }, "Imported qpdf dispatch/report missing");
        var text = Path.Combine(folder, "مستند الحزمة.txt"); File.WriteAllText(text, "Naqla imported office component\nمرحبا بالعالم");
        var docx = service.ConvertAsync(FileClassifier.Inspect(text), "DOCX").GetAwaiter().GetResult(); using (var zip = ZipFile.OpenRead(docx.OutputPath)) { using var content = zip.GetEntry("word/document.xml")!.Open(); Check(System.Xml.Linq.XDocument.Load(content).Root!.Value.Contains("مرحبا"), "Imported office conversion"); }
        Check(store.Status().All(x => x.Installed), "Pack processing modified static owned component files");
        File.WriteAllText(Path.Combine(folder, "component-packs.json"), JsonSerializer.Serialize(new { CatalogVersion = ComponentStore.CatalogVersion, Status = "PASSED", AllSixImported = true, RealOcrSegmentationSpeechArchivePrivacyOffice = true, Packs = timings, PhysicalOfflineNetworkDisabled = false, CleanPc = "MANUAL PENDING" }, new JsonSerializerOptions { WriteIndented = true }));
        ComponentStore.ReleaseVerificationHandles();
    }
}
