using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Naqla.Core;

namespace Naqla.Conversion;

/// <summary>Explicit optional LibreOffice component. No COM, Office dependency, network download, or system install.</summary>
public sealed class DocumentService(string officeDirectory)
{
    private string Executable => Path.Combine(officeDirectory, "program", "soffice.com");
    public bool Available => File.Exists(Executable);
    private static readonly string[] Formats = ["DOCX", "PDF", "TXT", "RTF", "HTML", "ODT"];
    public async Task<ConversionResult> ConvertAsync(DraggedFile source, string target, CancellationToken token = default)
    {
        if (!Available) throw new NotSupportedException("Install the local offline LibreOffice document component first. No runtime download will be attempted.");
        if (!Formats.Contains(source.Extension) || source.Extension == "PDF" || !Formats.Contains(target) || target == source.Extension) throw new NotSupportedException("Unsupported document conversion pair.");
        if (source.Size is < 1 or > 67_108_864) throw new NotSupportedException("Document input size is limited to 64 MB.");
        token.ThrowIfCancellationRequested();
        await Task.Run(() => ComponentStore.VerifyNativeDirectory("office", officeDirectory, token), token).ConfigureAwait(false);
        using var guard = new FileStream(source.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read); var before = Convert.ToHexString(SHA256.HashData(guard)); guard.Position = 0;
        Preflight(source, token);
        using var work = new OutputDirectory(source.FullPath, " document-work"); var input = Path.Combine(work.Temporary, "input." + source.Extension.ToLowerInvariant());
        using (var write = new FileStream(input, FileMode.CreateNew, FileAccess.Write)) await guard.CopyToAsync(write, token).ConfigureAwait(false);
        var profile = Path.Combine(work.Temporary, "profile"); Directory.CreateDirectory(Path.Combine(profile, "user"));
        await File.WriteAllTextAsync(Path.Combine(profile, "user", "registrymodifications.xcu"), """
            <?xml version="1.0" encoding="UTF-8"?>
            <oor:items xmlns:oor="http://openoffice.org/2001/registry">
              <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop></item>
              <item oor:path="/org.openoffice.Office.Common/Load"><prop oor:name="UpdateMode" oor:op="fuse"><value>0</value></prop></item>
              <item oor:path="/org.openoffice.Office.Jobs/Jobs/UpdateCheck/Arguments"><prop oor:name="AutoCheckEnabled" oor:op="fuse"><value>false</value></prop><prop oor:name="AutoDownloadEnabled" oor:op="fuse"><value>false</value></prop></item>
              <item oor:path="/org.openoffice.Office.Common/Misc"><prop oor:name="FirstRun" oor:op="fuse"><value>false</value></prop></item>
              <item oor:path="/org.openoffice.Office.Common/Misc"><prop oor:name="UseOpenCL" oor:op="fuse"><value>false</value></prop></item>
            </oor:items>
            """, token).ConfigureAwait(false);
        var resultDirectory = Path.Combine(work.Temporary, "output"); Directory.CreateDirectory(resultDirectory);
        var filter = target switch { "DOCX" => "docx:Office Open XML Text", "PDF" => "pdf:writer_pdf_Export", "TXT" => "txt:Text (encoded):UTF8", "RTF" => "rtf:Rich Text Format", "HTML" => "html:HTML (StarWriter)", "ODT" => "odt:writer8", _ => throw new NotSupportedException() };
        await LocalProcess.RunCapturedAsync(Executable, ["-env:UserInstallation=" + new Uri(profile + Path.DirectorySeparatorChar).AbsoluteUri, "--headless", "--norestore", "--nodefault", "--nofirststartwizard", "--nolockcheck", "--convert-to", filter, "--outdir", resultDirectory, input], token, 0, 0, timeout: TimeSpan.FromMinutes(3), environment: new Dictionary<string, string> { ["PYTHONDONTWRITEBYTECODE"] = "1", ["PYTHONPYCACHEPREFIX"] = Path.Combine(work.Temporary, "python-cache"), ["SAL_DISABLE_OPENCL"] = "1", ["SAL_DISABLEGL"] = "1" }).ConfigureAwait(false);
        var resultPath = Path.Combine(resultDirectory, "input." + target.ToLowerInvariant());
        if (!File.Exists(resultPath) || new FileInfo(resultPath).Length == 0) throw new InvalidDataException("The local document engine did not produce the requested format.");
        await Task.Run(() => ValidateOutput(resultPath, target, token), token).ConfigureAwait(false);
        guard.Position = 0; if (Convert.ToHexString(SHA256.HashData(guard)) != before) throw new InvalidDataException("Document source preservation verification failed.");
        if (target == "HTML")
        {
            // HTML may reference exported images. Publish the complete private export folder atomically.
            foreach (var entry in Directory.EnumerateFileSystemEntries(resultDirectory))
                if (Directory.Exists(entry) || (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0 || new FileInfo(entry).Length > 67_108_864) throw new NotSupportedException("Unexpected document HTML sidecar structure.");
            var htmlBytes = Directory.GetFiles(resultDirectory).Sum(x => new FileInfo(x).Length);
            using var folder = new OutputDirectory(source.FullPath, " HTML");
            foreach (var file in Directory.GetFiles(resultDirectory)) File.Move(file, Path.Combine(folder.Temporary, Path.GetFileName(file)), false);
            File.Move(Path.Combine(folder.Temporary, "input.html"), Path.Combine(folder.Temporary, "index.html"), false);
            return new(folder.Publish(token), 0, 0, Report: new(source.Size, htmlBytes, "HTML and local assets in a new sibling folder. Document layout may differ."));
        }
        using var output = new OutputFile(source.FullPath, target); File.Move(resultPath, output.Temporary, false);
        var bytes = new FileInfo(output.Temporary).Length;
        return new(output.Publish(token), 0, 0, Report: new(source.Size, bytes, target == "TXT" ? "UTF-8 plain text: images, tables and formatting are not retained." : "Offline document conversion. Complex layout, fields and fonts can differ; review the saved copy."));
    }
    private static void Preflight(DraggedFile source, CancellationToken token)
    {
        if (source.Extension is "DOCX" or "ODT")
        {
            using var zip = ZipFile.OpenRead(source.FullPath);
            if (zip.Entries.Count > 10000 || zip.Entries.Sum(x => x.Length) > 268_435_456) throw new NotSupportedException("Document expanded content exceeds the local resource limit.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested(); ArchiveService.SafeRelativePath(entry.FullName);
                if (!names.Add(entry.FullName) || entry.Length > 33_554_432 || entry.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || entry.FullName.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase) || entry.FullName.Contains("/activeX/", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("Macros, OLE/ActiveX and oversized document parts are refused.");
                if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)) continue;
                using var read = entry.Open(); var xml = OfficePrivacy.ReadXml(read);
                foreach (var node in xml.Descendants())
                {
                    if (node.Name.LocalName == "script" || node.Name.LocalName == "scripts" && node.HasElements || node.Attributes().Any(a => a.Name.LocalName == "TargetMode" && a.Value == "External") || node.Attributes().Any(a => a.Name.LocalName == "href" && !a.Value.StartsWith('#') && Uri.TryCreate(a.Value, UriKind.Absolute, out _))) throw new NotSupportedException("External resources and scripting are not allowed in offline document conversion.");
                    if (node.Name.LocalName == "instrText" && Regex.IsMatch(node.Value, @"\b(DDE|DDEAUTO|INCLUDETEXT|INCLUDEPICTURE|LINK)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) throw new NotSupportedException("External document fields are refused.");
                    foreach (var attribute in node.Attributes().Where(x => x.Name.LocalName is "href" or "Target"))
                    {
                        if (attribute.Value.StartsWith('#')) continue;
                        if (Uri.TryCreate(attribute.Value, UriKind.Absolute, out _)) throw new NotSupportedException("External package resources are refused.");
                        if (source.Extension == "ODT")
                        {
                            var part = attribute.Value.StartsWith("./", StringComparison.Ordinal) ? attribute.Value[2..] : attribute.Value;
                            ArchiveService.SafeRelativePath(part);
                            if (!zip.Entries.Any(x => x.FullName == part || x.FullName.StartsWith(part.TrimEnd('/') + "/", StringComparison.Ordinal))) throw new NotSupportedException("An ODT resource references content outside its package.");
                        }
                    }
                }
            }
            if (source.Extension == "DOCX" && zip.GetEntry("word/document.xml") is null || source.Extension == "ODT" && zip.GetEntry("content.xml") is null) throw new InvalidDataException("Document content part is missing.");
        }
        else
        {
            if (source.Size > 16_777_216) throw new NotSupportedException("Text/RTF/HTML documents are limited to 16 MB.");
            var text = File.ReadAllText(source.FullPath, new UTF8Encoding(false, true));
            if (source.Extension == "HTML") CheckHtml(text, allowLocalAssets: false);
            if (source.Extension == "RTF" && (!text.TrimStart().StartsWith(@"{\rtf", StringComparison.Ordinal) || Regex.IsMatch(text, @"\\(object|objdata|field|file|datastore)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))) throw new NotSupportedException("RTF active objects, external fields or an invalid header are refused.");
        }
    }
    private static void CheckHtml(string text, bool allowLocalAssets)
    {
        var document = new HtmlParser().ParseDocument(text);
        if (document.QuerySelector("script,iframe,object,embed,link,base,form,video,audio,source,svg,math") is not null || document.All.Any(x => x.Attributes.Any(a => a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase) || a.Name is "srcset" or "background"))) throw new NotSupportedException("HTML active content and external resource mechanisms are refused.");
        foreach (var style in document.QuerySelectorAll("style").Select(x => x.TextContent).Concat(document.All.Select(x => x.GetAttribute("style") ?? "")))
            if (style.Contains('\\') || Regex.IsMatch(style, @"@(?!page\b)|url\s*\(|expression\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) throw new NotSupportedException("HTML CSS resource loading is refused.");
        foreach (var meta in document.QuerySelectorAll("meta[http-equiv]")) if (meta.GetAttribute("http-equiv")!.Equals("refresh", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("HTML refresh is refused.");
        foreach (var image in document.QuerySelectorAll("img"))
        {
            var src = image.GetAttribute("src") ?? "";
            if (src.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) || src.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase))
            {
                if (src.Length > 27_962_028) throw new NotSupportedException("Embedded HTML image is too large.");
                using var bitmap = new ImageMagick.MagickImage(Convert.FromBase64String(src[(src.IndexOf(',') + 1)..]));
                if ((long)bitmap.Width * bitmap.Height > RasterImages.MaximumPixels || bitmap.Format is not (ImageMagick.MagickFormat.Png or ImageMagick.MagickFormat.Jpeg)) throw new NotSupportedException("Invalid/oversized embedded HTML image.");
                continue;
            }
            if (!allowLocalAssets) throw new NotSupportedException("HTML input images must be embedded PNG/JPEG data URLs in this development build.");
            ArchiveService.SafeRelativePath(src); if (src.Contains('/') || src.Contains('\\')) throw new NotSupportedException("Unexpected HTML asset reference.");
        }
        foreach (var element in document.All) foreach (var attribute in element.Attributes)
            if (attribute.Name is "src" or "href" or "data" or "poster" or "codebase" && !(element.LocalName == "img" && attribute.Name == "src") && !(element.LocalName == "a" && attribute.Name == "href")) throw new NotSupportedException("Unsupported HTML resource attribute.");
    }
    internal static void ValidateOutput(string path, string format, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (format == "PDF")
        {
            using var document = UglyToad.PdfPig.PdfDocument.Open(path); if (document.NumberOfPages is < 1 or > 200) throw new NotSupportedException("Document PDF output is limited to 200 pages.");
            PdfToolkitService.Validate(path, document.NumberOfPages, token); return;
        }
        if (format is "DOCX" or "ODT")
        {
            using var zip = ZipFile.OpenRead(path); var name = format == "DOCX" ? "word/document.xml" : "content.xml"; using var read = zip.GetEntry(name)?.Open() ?? throw new InvalidDataException("The output document has no content part.");
            var xml = OfficePrivacy.ReadXml(read); if (!xml.Descendants().Any(x => x.Name.LocalName is "body" or "text")) throw new InvalidDataException("The output document content is invalid."); return;
        }
        var text = File.ReadAllText(path, new UTF8Encoding(false, true));
        if (format == "RTF" && !text.TrimStart().StartsWith(@"{\rtf", StringComparison.Ordinal)) throw new InvalidDataException("Invalid RTF output.");
        if (format == "HTML") CheckHtml(text, allowLocalAssets: true);
        if (format == "TXT" && string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("The document produced no extractable text.");
    }
}
