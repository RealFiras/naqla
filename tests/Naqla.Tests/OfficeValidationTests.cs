using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Naqla.Core;
using W = DocumentFormat.OpenXml.Wordprocessing;
using S = DocumentFormat.OpenXml.Spreadsheet;
using P = DocumentFormat.OpenXml.Presentation;
using D = DocumentFormat.OpenXml.Drawing;

internal static partial class Program
{
    private static void PrivacyValidOffice()
    {
        OpenXmlPackage Open(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".docx" => WordprocessingDocument.Open(path, false), ".xlsx" => SpreadsheetDocument.Open(path, false), _ => PresentationDocument.Open(path, false) };
        void Validate(string path) { using var package = Open(path); var errors = new OpenXmlValidator(FileFormatVersions.Office2019).Validate(package).Take(5).ToArray(); Check(errors.Length == 0, "Independent OOXML validation: " + string.Join("; ", errors.Select(x => x.Description))); }
        Dictionary<string, string> Bodies(string path)
        {
            using var zip = ZipFile.OpenRead(path); return zip.Entries.Where(x => x.FullName.StartsWith("word/") || x.FullName.StartsWith("xl/") || x.FullName.StartsWith("ppt/")).ToDictionary(x => x.FullName, x => { using var read = x.Open(); return Convert.ToHexString(SHA256.HashData(read)); });
        }
        foreach (var format in new[] { "docx", "xlsx", "pptx" })
        {
            var path = Path.Combine(folder, "مستند صالح." + format);
            using (var package = Create(path, format)) { package.PackageProperties.Creator = "Private author"; package.PackageProperties.LastModifiedBy = "Private editor"; package.PackageProperties.Created = DateTime.UtcNow; package.PackageProperties.Title = "Retained title"; }
            Validate(path); var before = Bodies(path); var hash = SHA256.HashData(File.ReadAllBytes(path)); var service = Privacy();
            var analysis = service.AnalyzeAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult(); Check(analysis.Detected.Contains("Author") && analysis.Detected.Contains("Dates"), "Real Office properties not found");
            var result = service.CleanAsync(analysis, new(["Author", "Dates"])).GetAwaiter().GetResult(); Validate(result.Output.OutputPath);
            var after = Bodies(result.Output.OutputPath); Check(before.Count == after.Count && before.All(x => after[x.Key] == x.Value), "Actual Office body/relationships changed");
            using var verified = Open(result.Output.OutputPath); Check(string.IsNullOrEmpty(verified.PackageProperties.Creator) && string.IsNullOrEmpty(verified.PackageProperties.LastModifiedBy) && verified.PackageProperties.Created is null && verified.PackageProperties.Title == "Retained title", "Independent Office properties mismatch");
            Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Actual Office original hash changed");
        }
        static OpenXmlPackage Create(string path, string format)
        {
            if (format == "docx")
            {
                var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document); var main = package.AddMainDocumentPart();
                main.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text("مرحبا Naqla offline document"))))); return package;
            }
            if (format == "xlsx")
            {
                var package = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook); var book = package.AddWorkbookPart(); var sheet = book.AddNewPart<WorksheetPart>();
                sheet.Worksheet = new S.Worksheet(new S.SheetData(new S.Row(new S.Cell(new S.InlineString(new S.Text("مرحبا Naqla spreadsheet"))) { CellReference = "A1", DataType = S.CellValues.InlineString }) { RowIndex = 1 }));
                book.Workbook = new S.Workbook(new S.Sheets(new S.Sheet { Id = book.GetIdOfPart(sheet), SheetId = 1, Name = "نقلة" })); return package;
            }
            var presentation = PresentationDocument.Create(path, PresentationDocumentType.Presentation); var part = presentation.AddPresentationPart(); var slide = part.AddNewPart<SlidePart>();
            var master = part.AddNewPart<SlideMasterPart>(); var layout = master.AddNewPart<SlideLayoutPart>(); layout.AddPart(master); slide.AddPart(layout);
            P.ShapeTree EmptyShapes() => new(new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties { Id = 1, Name = "" }, new P.NonVisualGroupShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()), new P.GroupShapeProperties());
            layout.SlideLayout = new P.SlideLayout(new P.CommonSlideData(EmptyShapes()), new P.ColorMapOverride(new D.MasterColorMapping())) { Type = P.SlideLayoutValues.Blank };
            master.SlideMaster = new P.SlideMaster(new P.CommonSlideData(EmptyShapes()), new P.ColorMap { Accent1 = D.ColorSchemeIndexValues.Accent1, Accent2 = D.ColorSchemeIndexValues.Accent2, Accent3 = D.ColorSchemeIndexValues.Accent3, Accent4 = D.ColorSchemeIndexValues.Accent4, Accent5 = D.ColorSchemeIndexValues.Accent5, Accent6 = D.ColorSchemeIndexValues.Accent6, Background1 = D.ColorSchemeIndexValues.Light1, Background2 = D.ColorSchemeIndexValues.Light2, Text1 = D.ColorSchemeIndexValues.Dark1, Text2 = D.ColorSchemeIndexValues.Dark2, Hyperlink = D.ColorSchemeIndexValues.Hyperlink, FollowedHyperlink = D.ColorSchemeIndexValues.FollowedHyperlink }, new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2147483649U, RelationshipId = master.GetIdOfPart(layout) }));
            var theme = master.AddNewPart<ThemePart>();
            var fill = "<a:solidFill><a:schemeClr val='phClr'/></a:solidFill>";
            var colors = string.Join("", new[] { "dk1", "lt1", "dk2", "lt2", "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "hlink", "folHlink" }.Select((name, i) => $"<a:{name}><a:srgbClr val='{(i % 2 == 0 ? "121820" : "F5F0E6")}'/></a:{name}>"));
            var font = "<a:latin typeface='Arial'/><a:ea typeface=''/><a:cs typeface='Arial'/>";
            var themeXml = $"<a:theme xmlns:a='http://schemas.openxmlformats.org/drawingml/2006/main' name='Naqla'><a:themeElements><a:clrScheme name='Naqla'>{colors}</a:clrScheme><a:fontScheme name='Naqla'><a:majorFont>{font}</a:majorFont><a:minorFont>{font}</a:minorFont></a:fontScheme><a:fmtScheme name='Naqla'><a:fillStyleLst>{fill}{fill}{fill}</a:fillStyleLst><a:lnStyleLst>{string.Concat(Enumerable.Repeat("<a:ln w='9525'>" + fill + "</a:ln>", 3))}</a:lnStyleLst><a:effectStyleLst>{string.Concat(Enumerable.Repeat("<a:effectStyle><a:effectLst/></a:effectStyle>", 3))}</a:effectStyleLst><a:bgFillStyleLst>{fill}{fill}{fill}</a:bgFillStyleLst></a:fmtScheme></a:themeElements></a:theme>";
            using (var data = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(themeXml))) theme.FeedData(data);
            var shapes = new P.ShapeTree(new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties { Id = 1, Name = "" }, new P.NonVisualGroupShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()), new P.GroupShapeProperties(),
                new P.Shape(new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = 2, Name = "Text" }, new P.NonVisualShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()), new P.ShapeProperties(), new P.TextBody(new D.BodyProperties(), new D.ListStyle(), new D.Paragraph(new D.Run(new D.Text("مرحبا Naqla presentation"))))));
            slide.Slide = new P.Slide(new P.CommonSlideData(shapes), new P.ColorMapOverride(new D.MasterColorMapping()));
            part.Presentation = new P.Presentation(new P.SlideMasterIdList(new P.SlideMasterId { Id = 2147483648U, RelationshipId = part.GetIdOfPart(master) }), new P.SlideIdList(new P.SlideId { Id = 256, RelationshipId = part.GetIdOfPart(slide) }), new P.SlideSize { Cx = 9144000, Cy = 6858000 }, new P.NotesSize { Cx = 6858000, Cy = 9144000 }); return presentation;
        }
    }
}
