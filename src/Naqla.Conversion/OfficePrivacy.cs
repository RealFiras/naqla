using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using Naqla.Core;

namespace Naqla.Conversion;

internal static class OfficePrivacy
{
    private static readonly Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["creator"] = "Author", ["lastModifiedBy"] = "Author", ["Company"] = "Author", ["Manager"] = "Author",
        ["created"] = "Dates", ["modified"] = "Dates", ["lastPrinted"] = "Dates",
        ["title"] = "Description", ["subject"] = "Description", ["description"] = "Description", ["keywords"] = "Description", ["category"] = "Description",
        ["Application"] = "Software", ["AppVersion"] = "Software", ["Template"] = "Software"
    };
    internal static XDocument ReadXml(Stream input) { using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32_000_000 }); return XDocument.Load(reader, LoadOptions.PreserveWhitespace); }
    private static ZipArchive Open(string path)
    {
        var zip = ZipFile.OpenRead(path);
        try
        {
            if (zip.Entries.Count > 10000 || zip.Entries.Sum(x => x.Length) > 268_435_456 || zip.Entries.Any(x => x.Length > 67_108_864 || x.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase))) throw new NotSupportedException("Office privacy limits: no macro packages, 10,000 entries, 256 MB expanded.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries) { ArchiveService.SafeRelativePath(entry.FullName); if (!names.Add(entry.FullName)) throw new InvalidDataException("Duplicate Office ZIP entry."); }
            if (zip.GetEntry("[Content_Types].xml") is null || zip.GetEntry("_rels/.rels") is null || !zip.Entries.Any(x => x.FullName is "word/document.xml" or "xl/workbook.xml" or "ppt/presentation.xml")) throw new InvalidDataException("The Office package is incomplete.");
            return zip;
        }
        catch { zip.Dispose(); throw; }
    }
    public static string[] Categories(string path)
    {
        using var zip = Open(path); var found = new HashSet<string>(); var parts = PropertyParts(zip);
        foreach (var entry in zip.Entries.Where(x => parts.TryGetValue(x.FullName, out var kind) && kind != "custom"))
        { using var read = entry.Open(); foreach (var field in ReadXml(read).Descendants()) if (Fields.TryGetValue(field.Name.LocalName, out var category) && !string.IsNullOrWhiteSpace(field.Value)) found.Add(category); }
        if (parts.Any(x => x.Value == "custom")) found.Add("CustomProperties");
        return found.Order().ToArray();
    }
    private static Dictionary<string, string> PropertyParts(ZipArchive zip)
    {
        var parts = new Dictionary<string, string>(StringComparer.Ordinal);
        using var input = zip.GetEntry("[Content_Types].xml")!.Open(); var types = ReadXml(input);
        foreach (var entry in types.Descendants().Where(x => x.Name.LocalName == "Override"))
        {
            var type = entry.Attribute("ContentType")?.Value ?? ""; var name = Uri.UnescapeDataString(entry.Attribute("PartName")?.Value.TrimStart('/') ?? "");
            var kind = type.EndsWith("core-properties+xml", StringComparison.Ordinal) ? "core" : type.EndsWith("extended-properties+xml", StringComparison.Ordinal) ? "app" : type.EndsWith("custom-properties+xml", StringComparison.Ordinal) ? "custom" : null;
            if (kind is null) continue;
            ArchiveService.SafeRelativePath(name); if (zip.GetEntry(name) is null) throw new InvalidDataException("A declared Office property part is missing."); parts.Add(name, kind);
        }
        foreach (var entry in types.Descendants().Where(x => x.Name.LocalName == "Default"))
        {
            var type = entry.Attribute("ContentType")?.Value ?? ""; var extension = entry.Attribute("Extension")?.Value ?? "";
            var kind = type.EndsWith("core-properties+xml", StringComparison.Ordinal) ? "core" : type.EndsWith("extended-properties+xml", StringComparison.Ordinal) ? "app" : type.EndsWith("custom-properties+xml", StringComparison.Ordinal) ? "custom" : null;
            if (kind is not null) foreach (var file in zip.Entries.Where(x => Path.GetExtension(x.FullName).TrimStart('.').Equals(extension, StringComparison.OrdinalIgnoreCase))) parts.TryAdd(file.FullName, kind);
        }
        // Older producers may omit an override for a conventional properties part.
        foreach (var (name, kind) in new[] { ("docProps/core.xml", "core"), ("docProps/app.xml", "app"), ("docProps/custom.xml", "custom") }) if (zip.GetEntry(name) is not null) parts.TryAdd(name, kind);
        return parts;
    }
    public static void Clean(string source, string output, PrivacySettings settings, CancellationToken token)
    {
        using var zip = Open(source); var hashes = new Dictionary<string, string>(); var parts = PropertyParts(zip);
        var removedParts = parts.Where(x => x.Value == "custom" && settings.Removes("CustomProperties")).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        using (var write = ZipFile.Open(output, ZipArchiveMode.Create))
        {
            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested(); var name = entry.FullName;
                if (removedParts.Contains(name)) continue;
                using var read = entry.Open(); var target = write.CreateEntry(name, CompressionLevel.Optimal); using var stream = target.Open();
                if (parts.TryGetValue(name, out var kind) && kind != "custom")
                {
                    var xml = ReadXml(read);
                    foreach (var field in xml.Descendants().Where(x => Fields.TryGetValue(x.Name.LocalName, out var category) && settings.Removes(category)).ToArray()) field.Remove();
                    xml.Save(stream, SaveOptions.DisableFormatting);
                }
                else if (settings.Removes("CustomProperties") && name is "[Content_Types].xml" or "_rels/.rels")
                {
                    var xml = ReadXml(read);
                    foreach (var field in xml.Descendants().Where(x => x.Attributes().Any(a => a.Name.LocalName is "PartName" or "Target" && removedParts.Contains(Uri.UnescapeDataString(a.Value.TrimStart('/'))))).ToArray()) field.Remove();
                    xml.Save(stream, SaveOptions.DisableFormatting);
                }
                else { using var data = new MemoryStream(); read.CopyTo(data); var bytes = data.ToArray(); hashes.Add(name, Convert.ToHexString(SHA256.HashData(bytes))); stream.Write(bytes); }
            }
        }
        using var validated = Open(output);
        foreach (var pair in hashes)
        {
            token.ThrowIfCancellationRequested(); using var check = validated.GetEntry(pair.Key)?.Open() ?? throw new InvalidDataException("Office content part was lost.");
            if (Convert.ToHexString(SHA256.HashData(check)) != pair.Value) throw new InvalidDataException("Office body/asset/relationship changed during privacy cleanup.");
        }
        if (Categories(output).Intersect(settings.Categories).Any()) throw new InvalidDataException("Targeted Office metadata remains after cleanup.");
    }
}
