using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Naqla.Core;

namespace Naqla.Conversion;

public sealed class PrivacyCleanService(string mediaDirectory, string? qpdfDirectory = null)
{
    private static string Hash(string path) { using var read = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(read)); }
    private static bool Office(DraggedFile file) => file.Extension is "DOCX" or "XLSX" or "PPTX";
    public async Task<PrivacyAnalysis> AnalyzeAsync(DraggedFile source, CancellationToken token = default)
    {
        var file = FileClassifier.Inspect(source.FullPath); if (file.Size > 268_435_456) throw new NotSupportedException("Privacy Clean is limited to 256 MB per file.");
        token.ThrowIfCancellationRequested(); string[] categories; var limits = new List<string>();
        if (Office(file)) { categories = await Task.Run(() => OfficePrivacy.Categories(file.FullPath), token).ConfigureAwait(false); limits.Add("Document bodies, comments, tracked changes and embedded assets are preserved and may contain personal information."); }
        else if (file.Extension == "PDF") { categories = await Task.Run(() => PdfCategories(file.FullPath), token).ConfigureAwait(false); limits.Add("This removes document properties/XMP only. Visible content, annotations, embedded files and hidden revisions are not redacted. Interactive/signed/layered PDFs are refused."); }
        else if (file.Category == FileCategory.Image)
        {
            categories = await Task.Run(() => ImagePrivacy.Categories(file), token).ConfigureAwait(false);
            limits.Add("Essential ICC/color/orientation information is retained. Unknown proprietary APP/container metadata may remain.");
            if (file.Extension is "AVIF" or "HEIC") limits.Add("Clean Copy needs explicit permission to save a lossless PNG; Naqla does not claim a lossless HEIC/AVIF metadata rewrite.");
        }
        else if (file.Category is FileCategory.Audio or FileCategory.Video) { categories = (await MediaTags(file.FullPath, token).ConfigureAwait(false)).Select(TagCategory).Where(x => x != "").Distinct().ToArray(); limits.Add("Stream copying preserves encoded content; mandatory container/codec tags and unrecognized tags are reported as retained."); }
        else throw new NotSupportedException("Privacy Clean does not support this file type.");
        return new(file, await Task.Run(() => Hash(file.FullPath), token).ConfigureAwait(false), categories, limits.AsReadOnly());
    }
    public async Task<PrivacyResult> CleanAsync(PrivacyAnalysis analysis, PrivacySettings settings, CancellationToken token = default)
    {
        settings.Validate(); token.ThrowIfCancellationRequested(); var source = analysis.Source;
        using var guard = new FileStream(source.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (Hash(source.FullPath) != analysis.Sha256) throw new InvalidDataException("The source changed after its metadata preview.");
        var requested = analysis.Detected.Intersect(settings.Categories).ToArray();
        if (requested.Length == 0) throw new InvalidDataException("No selected removable metadata was detected. No misleading clean-copy success was created.");
        var fallback = source.Category == FileCategory.Image && source.Extension is "AVIF" or "HEIC";
        if (fallback && !settings.AllowLosslessPngFallback) throw new NotSupportedException("Approve the lossless PNG output explicitly before cleaning HEIC/AVIF.");
        var format = fallback ? "PNG" : source.Extension == "JPEG" ? "JPG" : source.Extension;
        using var output = new OutputFile(source.FullPath, format, "-clean");
        var reencoded = source.Category == FileCategory.Image && source.Extension is not ("PNG" or "JPG" or "JPEG");
        if (source.Extension == "PNG" && (int)new ImageMagick.MagickImageInfo(source.FullPath).Orientation is not (0 or 1)) reencoded = true;
        if (Office(source)) await Task.Run(() => OfficePrivacy.Clean(source.FullPath, output.Temporary, settings, token), token).ConfigureAwait(false);
        else if (source.Extension == "PDF") await CleanPdf(source, output.Temporary, settings, token).ConfigureAwait(false);
        else if (source.Category == FileCategory.Image) await Task.Run(() => ImagePrivacy.Clean(source, output.Temporary, format, settings, token), token).ConfigureAwait(false);
        else await CleanMedia(source, output.Temporary, settings, token).ConfigureAwait(false);
        var remaining = await AnalyzeAsync(FileClassifier.Inspect(output.Temporary), token).ConfigureAwait(false);
        if (remaining.Detected.Intersect(requested).Any()) throw new InvalidDataException("Independent reinspection found targeted metadata remaining: " + string.Join(", ", remaining.Detected.Intersect(requested)));
        if (Hash(source.FullPath) != analysis.Sha256) throw new InvalidDataException("Source preservation verification failed.");
        var bytes = new FileInfo(output.Temporary).Length; var outputHash = Hash(output.Temporary);
        var retained = remaining.Detected.Concat(source.Category == FileCategory.Image ? new[] { "Essential color/orientation structure" } : Array.Empty<string>()).ToArray();
        var report = new PrivacyReport(requested, retained, analysis.Limits, bytes, reencoded, analysis.Sha256, outputHash);
        return new(new(output.Publish(token), 0, 0, Report: new(source.Size, bytes, $"Removed: {string.Join(", ", requested)}. Content is not anonymized.", reencoded)), report);
    }
    private static readonly Dictionary<string, string> PdfFields = new()
    { ["/Author"] = "Author", ["/Creator"] = "Software", ["/Producer"] = "Software", ["/CreationDate"] = "Dates", ["/ModDate"] = "Dates", ["/Title"] = "Description", ["/Subject"] = "Description", ["/Keywords"] = "Description" };
    private static string[] PdfCategories(string path)
    {
        using var read = File.OpenRead(path); using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(read, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        if (pdf.SecuritySettings.IsEncrypted) throw new NotSupportedException("Encrypted PDFs require an unencrypted copy.");
        var result = PdfFields.Where(x => pdf.Info.Elements.ContainsKey(x.Key) && !string.IsNullOrEmpty(pdf.Info.Elements.GetString(x.Key))).Select(x => x.Value).ToHashSet();
        if (pdf.Info.Elements.Keys.Any(x => !PdfFields.ContainsKey(x) && x != "/Trapped")) result.Add("CustomProperties");
        if (pdf.Internals.Catalog.Elements.ContainsKey("/Metadata")) result.Add("XMP"); return result.ToArray();
    }
    private async Task CleanPdf(DraggedFile file, string output, PrivacySettings settings, CancellationToken token)
    {
        var executable = Path.Combine(qpdfDirectory ?? Path.Combine(AppContext.BaseDirectory, "qpdf-engine"), "qpdf.exe");
        if (!File.Exists(executable)) throw new NotSupportedException("The local qpdf metadata component is missing.");
        await Task.Run(() => ComponentStore.VerifyNativeDirectory("pdf-metadata", Path.GetDirectoryName(executable)!, token), token).ConfigureAwait(false);
        var service = new PdfToolkitService(); var session = await service.OpenAsync([file], token).ConfigureAwait(false);
        async Task<JsonObject> Objects(string key)
        {
            var text = await LocalProcess.RunCapturedAsync(executable, ["--json=2", "--json-key=qpdf", "--json-object=" + key, file.FullPath], token, 1_048_576).ConfigureAwait(false);
            return JsonNode.Parse(text.Output)!["qpdf"]![1]!.AsObject();
        }
        static string ObjectNumber(string reference)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(reference, @"\A\d{1,9} \d{1,5} R\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) throw new InvalidDataException("Invalid PDF object reference.");
            return string.Join(',', reference.Split(' ').Take(2));
        }
        var trailer = (await Objects("trailer").ConfigureAwait(false))["trailer"]!["value"]!.AsObject();
        var rootReference = trailer["/Root"]!.GetValue<string>(); var root = await Objects(ObjectNumber(rootReference)).ConfigureAwait(false); var updates = new JsonObject();
        var catalog = root["obj:" + rootReference]!["value"]!.AsObject(); if (settings.Removes("XMP")) catalog.Remove("/Metadata");
        updates["obj:" + rootReference] = root["obj:" + rootReference]!.DeepClone();
        if (trailer["/Info"] is JsonValue infoValue)
        {
            var reference = infoValue.GetValue<string>(); var objects = await Objects(ObjectNumber(reference)).ConfigureAwait(false); var info = objects["obj:" + reference]!["value"]!.AsObject();
            foreach (var pair in PdfFields.Where(x => settings.Removes(x.Value))) info.Remove(pair.Key);
            if (settings.Removes("CustomProperties")) foreach (var key in info.Select(x => x.Key).Where(x => !PdfFields.ContainsKey(x) && x != "/Trapped").ToArray()) info.Remove(key);
            updates["obj:" + reference] = objects["obj:" + reference]!.DeepClone();
        }
        using var work = new OutputDirectory(file.FullPath, " metadata-work"); var updatePath = Path.Combine(work.Temporary, "update.json");
        var patch = new JsonObject { ["qpdf"] = new JsonArray(new JsonObject { ["jsonversion"] = 2 }, updates) };
        await File.WriteAllTextAsync(updatePath, patch.ToJsonString(), token).ConfigureAwait(false);
        await LocalProcess.RunAsync(executable, [file.FullPath, "--update-from-json=" + updatePath, output], token).ConfigureAwait(false);
        PdfToolkitService.Validate(output, session.PageCount, token);
        using var before = UglyToad.PdfPig.PdfDocument.Open(file.FullPath); using var after = UglyToad.PdfPig.PdfDocument.Open(output);
        for (var page = 1; page <= before.NumberOfPages; page++)
        {
            token.ThrowIfCancellationRequested(); if (before.GetPage(page).Text != after.GetPage(page).Text) throw new InvalidDataException("PDF text content changed during cleanup.");
            lock (PdfConversions.PdfiumGate)
            {
                using var sourceRead = File.OpenRead(file.FullPath); using var outputRead = File.OpenRead(output);
                using var a = PDFtoImage.Conversion.ToImage(sourceRead, page: page - 1, options: new(Dpi: 72)); using var b = PDFtoImage.Conversion.ToImage(outputRead, page: page - 1, options: new(Dpi: 72));
                if (a.Width != b.Width || a.Height != b.Height || !a.Bytes.SequenceEqual(b.Bytes)) throw new InvalidDataException("PDF rendered page content changed during cleanup.");
            }
        }
    }
    private static string TagCategory(string tag) => tag.ToLowerInvariant() switch
    {
        "title" or "description" or "synopsis" or "album" or "genre" or "track" => "Description", "artist" or "author" or "composer" or "album_artist" or "copyright" => "Author", "comment" => "Comments", "creation_time" or "date" or "year" => "Dates", "encoder" or "encoded_by" or "software" => "Software", _ => ""
    };
    private async Task<string[]> MediaTags(string path, CancellationToken token)
    {
        var data = await LocalProcess.RunCapturedAsync(Path.Combine(mediaDirectory, "ffprobe.exe"), ["-v", "error", "-protocol_whitelist", "file,pipe", "-show_entries", "format_tags:stream_tags", "-of", "json", path], token).ConfigureAwait(false);
        using var json = JsonDocument.Parse(data.Output); var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Read(JsonElement element) { if (element.TryGetProperty("tags", out var list)) foreach (var tag in list.EnumerateObject()) tags.Add(tag.Name); }
        if (json.RootElement.TryGetProperty("format", out var format)) Read(format); if (json.RootElement.TryGetProperty("streams", out var streams)) foreach (var stream in streams.EnumerateArray()) Read(stream);
        return tags.ToArray();
    }
    private async Task CleanMedia(DraggedFile source, string output, PrivacySettings settings, CancellationToken token)
    {
        var tags = await MediaTags(source.FullPath, token).ConfigureAwait(false);
        var args = new List<string> { "-v", "error", "-nostdin", "-n", "-xerror", "-protocol_whitelist", "file,pipe", "-i", source.FullPath, "-map", "0", "-c", "copy" };
        foreach (var tag in tags.Where(x => settings.Removes(TagCategory(x)))) args.AddRange(["-metadata", tag + "=", "-metadata:s", tag + "="]);
        args.AddRange(["-fflags", "+bitexact", "-flags:a", "+bitexact", "-flags:v", "+bitexact", output]);
        await LocalProcess.RunAsync(Path.Combine(mediaDirectory, "ffmpeg.exe"), args, token).ConfigureAwait(false);
        // Compare encoded packet hashes rather than decode/re-encode samples.
        async Task<string> PacketHash(string path) => await LocalProcess.RunAsync(Path.Combine(mediaDirectory, "ffmpeg.exe"), ["-v", "error", "-nostdin", "-protocol_whitelist", "file,pipe", "-i", path, "-map", "0:v?", "-map", "0:a?", "-c", "copy", "-f", "streamhash", "-hash", "sha256", "-"], token, true).ConfigureAwait(false);
        if (await PacketHash(source.FullPath).ConfigureAwait(false) != await PacketHash(output).ConfigureAwait(false)) throw new InvalidDataException("Media packet preservation verification failed.");
        await new AdvancedMediaService(mediaDirectory).DecodeAsync(output, token).ConfigureAwait(false);
    }
}
