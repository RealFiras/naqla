using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Naqla.Core;

namespace Naqla.Conversion;

/// <summary>Declarative packages only. Never loads or runs package code. A worker process is not an OS sandbox.</summary>
public sealed class PluginService(string directory)
{
    public const int ApiVersion = 1;
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = true };
    private sealed record Approval(bool Enabled, string Fingerprint);
    private static string Id(string value) => Regex.IsMatch(value, @"\A[a-z][a-z0-9.-]{2,63}\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) ? value : throw new InvalidDataException("Invalid plugin identifier.");
    private static void NoLinks(string path)
    {
        for (var item = new DirectoryInfo(Path.GetFullPath(path)); item is not null; item = item.Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0) throw new NotSupportedException("Plugin storage must not traverse links or reparse points.");
    }
    private static PluginManifest Validate(string text)
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(text, Json) ?? throw new InvalidDataException("Missing plugin manifest."); Id(manifest.Id);
        if (manifest.SchemaVersion != 1 || manifest.ApiVersion != ApiVersion || !Version.TryParse(manifest.Version, out var version) || version.Major < 1 || manifest.Name is null || manifest.Name.Length is < 1 or > 60 || manifest.Name.Any(char.IsControl)) throw new NotSupportedException("Incompatible plugin manifest/API version.");
        if (manifest.Capabilities is null || !manifest.Capabilities.Order().SequenceEqual(new[] { "read-selection", "write-result" }) || manifest.Actions is null || manifest.Actions.Length is < 1 or > 4) throw new InvalidDataException("Unsupported plugin capabilities/actions.");
        var ids = new HashSet<string>();
        foreach (var action in manifest.Actions)
        {
            if (action is null || action.Label is null || action.Id is null) throw new InvalidDataException("Missing plugin action fields.");
            Id(action.Id);
            if (!ids.Add(action.Id) || action.Label.Length is < 1 or > 16 || action.Label.Any(char.IsControl) || action.Operation is not ("sha256" or "text-statistics") || action.Extensions is null || action.Extensions.Length is < 1 or > 40 || action.Extensions.Any(x => !Regex.IsMatch(x, @"\A[A-Z0-9]{1,8}\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))) throw new InvalidDataException("Plugin action is invalid or requests executable code.");
            if (action.Operation == "text-statistics" && action.Extensions.Any(x => x is not ("TXT" or "HTML" or "SRT" or "VTT"))) throw new NotSupportedException("Text statistics accepts UTF-8 text formats only.");
        }
        return manifest;
    }
    private static string Fingerprint(string package)
    {
        NoLinks(package); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.GetFiles(package).Order(StringComparer.Ordinal))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Plugin package contains a link.");
            if (new FileInfo(path).Length > 1_048_576 || Path.GetFileName(path) is not ("manifest.json" or "README.md")) throw new InvalidDataException("Only a declarative manifest and README are permitted.");
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(path))); using var file = File.OpenRead(path); hash.AppendData(SHA256.HashData(file));
        }
        if (Directory.GetDirectories(package).Any()) throw new InvalidDataException("Nested/executable plugin data is refused.");
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public IReadOnlyList<InstalledPlugin> List()
    {
        NoLinks(directory); if (!Directory.Exists(directory)) return [];
        var plugins = new List<InstalledPlugin>();
        foreach (var package in Directory.EnumerateDirectories(directory).Where(x => !Path.GetFileName(x).StartsWith('.')).Take(65))
        {
            if (plugins.Count >= 64) throw new NotSupportedException("At most 64 local plugin packages are supported.");
            try
            {
                var fingerprint = Fingerprint(package); var manifest = Validate(File.ReadAllText(Path.Combine(package, "manifest.json")));
                if (manifest.Id != Path.GetFileName(package)) throw new InvalidDataException("Plugin identifier/path mismatch.");
                var approvalPath = Path.Combine(directory, "." + manifest.Id + ".approval.json"); Approval? approval = null;
                if (File.Exists(approvalPath) && (File.GetAttributes(approvalPath) & FileAttributes.ReparsePoint) == 0 && new FileInfo(approvalPath).Length < 4096) approval = JsonSerializer.Deserialize<Approval>(File.ReadAllText(approvalPath), Json);
                var changed = approval is not null && fingerprint != approval.Fingerprint;
                plugins.Add(new(manifest, approval?.Enabled == true && !changed, changed, fingerprint));
            }
            catch (Exception error) when (error is IOException or JsonException or ArgumentException or NotSupportedException) { /* Invalid packages register no actions. */ }
        }
        return plugins.AsReadOnly();
    }
    public async Task<InstalledPlugin> InstallAsync(string zipPath, CancellationToken token = default)
    {
        NoLinks(directory); Directory.CreateDirectory(directory);
        using var zipFile = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (zipFile.Length > 2_097_152 || List().Count >= 64) throw new NotSupportedException("Plugin package/installation count limit exceeded.");
        using var zip = new ZipArchive(zipFile, ZipArchiveMode.Read); if (zip.Entries.Count is < 1 or > 2) throw new InvalidDataException("Plugin ZIP must contain only manifest.json and optional README.md.");
        using var work = new OutputDirectory(Path.Combine(directory, "plugin.zip"), "work"); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (entry.FullName is not ("manifest.json" or "README.md") || !names.Add(entry.FullName) || entry.Length > 1_048_576 || (entry.ExternalAttributes >> 16 & 0xf000) == 0xa000) throw new InvalidDataException("Invalid/executable/linked plugin ZIP entry.");
            using var read = entry.Open(); using var write = new FileStream(Path.Combine(work.Temporary, entry.FullName), FileMode.CreateNew, FileAccess.Write);
            var buffer = new byte[8192]; long bytes = 0; int n;
            while ((n = await read.ReadAsync(buffer, token).ConfigureAwait(false)) != 0) { bytes += n; if (bytes > entry.Length || bytes > 1_048_576) throw new InvalidDataException("Plugin entry expansion exceeded its limit."); await write.WriteAsync(buffer.AsMemory(0, n), token).ConfigureAwait(false); }
        }
        var manifest = Validate(await File.ReadAllTextAsync(Path.Combine(work.Temporary, "manifest.json"), token).ConfigureAwait(false)); var fingerprint = Fingerprint(work.Temporary);
        var target = Path.Combine(directory, Id(manifest.Id));
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("This plugin is already installed. Disable/uninstall it before replacing it.");
        token.ThrowIfCancellationRequested(); Directory.Move(work.Temporary, target);
        return new(manifest, false, false, fingerprint);
    }
    public void SetEnabled(string id, bool enabled)
    {
        Id(id); NoLinks(directory); var package = List().Single(x => x.Manifest.Id == id);
        var target = Path.Combine(directory, "." + id + ".approval.json"); var temporary = Path.Combine(directory, ".approval-" + Guid.NewGuid().ToString("N"));
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(new Approval(enabled, package.Fingerprint), Json)); File.Move(temporary, target, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Uninstall(string id)
    {
        Id(id); NoLinks(directory); var package = Path.Combine(directory, id); Fingerprint(package); // Verify exact owned shape before removal.
        foreach (var path in Directory.GetFiles(package)) File.Delete(path); Directory.Delete(package);
        var approval = Path.Combine(directory, "." + id + ".approval.json"); if (File.Exists(approval)) File.Delete(approval);
    }
    public IReadOnlyList<WheelOption> ActionsFor(DraggedFile source) => List().Where(x => x.Enabled).SelectMany(x => x.Manifest.Actions.Where(a => a.Extensions.Contains(source.Extension)).Select(a => new WheelOption("PLUGIN:" + x.Manifest.Id + ":" + a.Id, a.Label, true))).ToArray();
    public async Task<ConversionResult> ExecuteAsync(DraggedFile source, string actionId, CancellationToken token = default)
    {
        var parts = actionId.Split(':'); if (parts.Length != 3 || parts[0] != "PLUGIN") throw new ArgumentException("Invalid plugin action.");
        var package = List().SingleOrDefault(x => x.Enabled && x.Manifest.Id == parts[1]) ?? throw new NotSupportedException("The plugin is disabled, incompatible or changed.");
        var action = package.Manifest.Actions.SingleOrDefault(x => x.Id == parts[2] && x.Extensions.Contains(source.Extension)) ?? throw new NotSupportedException("The plugin does not accept this file.");
        if (source.Size > (action.Operation == "text-statistics" ? 8_388_608 : 268_435_456)) throw new NotSupportedException("The plugin input exceeds its size limit.");
        using var work = new OutputDirectory(source.FullPath, " plugin-work");
        using (var read = new FileStream(source.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var write = new FileStream(Path.Combine(work.Temporary, "input.bin"), FileMode.CreateNew, FileAccess.Write)) await read.CopyToAsync(write, token).ConfigureAwait(false);
        await LocalProcess.RunCapturedAsync(Path.Combine(AppContext.BaseDirectory, "Naqla.App.exe"), ["--internal-plugin", action.Operation], token, 0, 0, work.Temporary, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        var response = Path.Combine(work.Temporary, "result.json"); if (!File.Exists(response) || new FileInfo(response).Length > 4096) throw new InvalidDataException("The plugin host returned a missing or oversized response.");
        var json = await File.ReadAllTextAsync(response, token).ConfigureAwait(false);
        // Independently recompute from the exact staged input. Never accept arbitrary output paths or unchecked host values.
        if (json != Compute(action.Operation, Path.Combine(work.Temporary, "input.bin"))) throw new InvalidDataException("Plugin host result validation failed.");
        using var output = new OutputFile(source.FullPath, "JSON", " " + action.Operation); await File.WriteAllTextAsync(output.Temporary, json, token).ConfigureAwait(false);
        using var validated = JsonDocument.Parse(await File.ReadAllTextAsync(output.Temporary, token).ConfigureAwait(false));
        return new(output.Publish(token), 0, 0);
    }
    internal static string Compute(string operation, string path)
    {
        if (operation == "sha256") { using var stream = File.OpenRead(path); if (stream.Length > 268_435_456) throw new InvalidDataException(); return JsonSerializer.Serialize(new { Algorithm = "SHA256", Value = Convert.ToHexString(SHA256.HashData(stream)) }, Json); }
        if (operation != "text-statistics" || new FileInfo(path).Length > 8_388_608) throw new InvalidDataException();
        var text = File.ReadAllText(path, new UTF8Encoding(false, true));
        var words = Regex.Matches(text, @"\S+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)).Count;
        return JsonSerializer.Serialize(new TextStatistics(System.Globalization.StringInfo.ParseCombiningCharacters(text).Length, words, text.Length == 0 ? 0 : text.Count(c => c == '\n') + 1, Encoding.UTF8.GetByteCount(text)), Json);
    }
    public static int RunHost(string[] args)
    {
        try { if (args.Length != 2 || args[0] != "--internal-plugin") return 2; File.WriteAllText("result.json", Compute(args[1], "input.bin")); return 0; }
        catch { return 3; }
    }
}
