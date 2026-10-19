using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Naqla.Conversion;

public sealed record ComponentFile(string Feature, long Bytes, string Sha256);
public sealed record ComponentStatus(string Feature, bool Installed, int Files, long Bytes);
/// <summary>Explicit local import of exact, build-pinned component files. No network, scripts or installer hooks.</summary>
public sealed class ComponentStore(string root)
{
    public const string CatalogVersion = "2026-10-19";
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Naqla", "Components");
    private static readonly Lazy<IReadOnlyDictionary<string, ComponentFile>> Catalog = new(() =>
    {
        using var read = typeof(ComponentStore).Assembly.GetManifestResourceStream("Naqla.ComponentCatalog.json") ?? throw new NotSupportedException("This build has no verified optional-component catalog.");
        return JsonSerializer.Deserialize<Dictionary<string, ComponentFile>>(read) ?? throw new InvalidDataException("Invalid embedded component catalog.");
    });
    private sealed record Pack(int SchemaVersion, string Feature, string CatalogVersion);
    private static string Digest(string path) { using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); return Convert.ToHexString(SHA256.HashData(input)); }
    private static void Guard(string path)
    {
        for (var item = new DirectoryInfo(Path.GetFullPath(path)); item is not null; item = item.Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Component storage must not traverse junctions/links.");
    }
    public IReadOnlyList<ComponentStatus> Status()
    {
        Guard(root);
        return Catalog.Value.GroupBy(x => x.Value.Feature).Select(x => new ComponentStatus(x.Key,
            File.Exists(Path.Combine(root, ".component-" + x.Key + ".json")) && x.All(file => File.Exists(Path.Combine(root, file.Key))), x.Count(), x.Sum(file => file.Value.Bytes))).ToArray();
    }
    public async Task InstallAsync(string zipPath, IProgress<int>? progress = null, CancellationToken token = default)
    {
        Guard(root); Directory.CreateDirectory(root); using var input = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 1_073_741_824) throw new NotSupportedException("Optional packs are limited to 1 GB compressed.");
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        if (zip.Entries.Count is < 2 or > 32000) throw new InvalidDataException("Invalid optional pack entry count.");
        var manifestEntry = zip.GetEntry("component.json") ?? throw new InvalidDataException("Missing optional pack manifest."); if (manifestEntry.Length > 4096) throw new InvalidDataException("Oversized pack manifest.");
        Pack manifest; using (var read = manifestEntry.Open()) manifest = JsonSerializer.Deserialize<Pack>(read) ?? throw new InvalidDataException("Invalid optional pack manifest.");
        if (manifest.SchemaVersion != 1 || manifest.CatalogVersion != CatalogVersion || !Catalog.Value.Any(x => x.Value.Feature == manifest.Feature)) throw new NotSupportedException("This optional pack is not compatible with this Naqla build.");
        var expected = Catalog.Value.Where(x => x.Value.Feature == manifest.Feature).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        if (zip.Entries.Count != expected.Count + 1) throw new InvalidDataException("The pack's dependency/license closure is incomplete or contains unexpected files.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var moved = new List<string>();
        using var stage = new OutputDirectory(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(root))!, "component.pack"), "work");
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested(); ArchiveService.SafeRelativePath(entry.FullName);
            if (!names.Add(entry.FullName) || (entry.ExternalAttributes >> 16 & 0xf000) == 0xa000) throw new InvalidDataException("Duplicate/linked optional pack entries.");
            if (entry.FullName == "component.json") continue;
            if (!expected.TryGetValue(entry.FullName, out var pinned) || entry.Length != pinned.Bytes) throw new InvalidDataException("Unrecognized/modified optional component file.");
            var path = Path.Combine(stage.Temporary, entry.FullName); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var read = entry.Open()) using (var write = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                var buffer = new byte[65536]; int n; long size = 0;
                while ((n = await read.ReadAsync(buffer, token).ConfigureAwait(false)) != 0) { size += n; if (size > pinned.Bytes) throw new InvalidDataException("Optional pack entry exceeded its pinned length."); await write.WriteAsync(buffer.AsMemory(0, n), token).ConfigureAwait(false); }
            }
            if (!Digest(path).Equals(pinned.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Optional component checksum mismatch.");
            progress?.Report(50 * names.Count / zip.Entries.Count);
        }
        try
        {
            foreach (var pair in expected)
            {
                token.ThrowIfCancellationRequested(); var path = Path.Combine(root, pair.Key); Guard(Path.GetDirectoryName(path)!);
                if (File.Exists(path)) { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || !Digest(path).Equals(pair.Value.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("An existing component file was modified. It will not be overwritten."); }
            }
            var count = 0;
            foreach (var pair in expected)
            {
                token.ThrowIfCancellationRequested(); var path = Path.Combine(root, pair.Key); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (!File.Exists(path)) { File.Move(Path.Combine(stage.Temporary, pair.Key), path, false); moved.Add(path); }
                progress?.Report(50 + 50 * ++count / expected.Count);
            }
            var marker = Path.Combine(root, ".component-" + manifest.Feature + ".json"); var temp = Path.Combine(stage.Temporary, "marker.json"); await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(manifest), token).ConfigureAwait(false); File.Move(temp, marker, true);
        }
        catch
        {
            foreach (var path in moved) if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }
    public void Uninstall(string feature)
    {
        Guard(root); var expected = Catalog.Value.Where(x => x.Value.Feature == feature).ToArray(); if (expected.Length == 0) throw new ArgumentException("Unknown component.");
        foreach (var pair in expected)
        {
            var path = Path.Combine(root, pair.Key); Guard(Path.GetDirectoryName(path)!);
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && Digest(path).Equals(pair.Value.Sha256, StringComparison.OrdinalIgnoreCase)) File.Delete(path);
        }
        var marker = Path.Combine(root, ".component-" + feature + ".json"); if (File.Exists(marker)) File.Delete(marker);
    }
    // Verify exact trusted native code immediately before execution; not merely a user-supplied manifest checksum.
    private sealed class Verification : IDisposable
    {
        public readonly object Gate = new();
        public long Generation;
        public long Verified = -1;
        public string LastChange = "";
        private readonly FileSystemWatcher watcher;
        public Verification(string directory, IEnumerable<string> relativeFiles)
        {
            var expected = relativeFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool Relevant(string? name, bool tree = false)
            {
                if (name is null) return true;
                var relative = name.Replace('\\', '/');
                if (expected.Contains(relative) || tree && expected.Any(x => x.StartsWith(relative.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))) return true;
                // LibreOffice maintains derived extension/cache state; it is not part of the static native pack.
                // New executable/configuration files elsewhere invalidate verification, too.
                if (relative.StartsWith("share/uno_packages/cache/", StringComparison.OrdinalIgnoreCase) || relative.Contains("/__pycache__/", StringComparison.OrdinalIgnoreCase)) return false;
                return new[] { ".dll", ".exe", ".com", ".pyd", ".py", ".jar", ".rdb", ".ini", ".xcd", ".xcu" }.Contains(Path.GetExtension(relative), StringComparer.OrdinalIgnoreCase);
            }
            void Changed(string? name, bool tree = false) { if (Relevant(name, tree)) { LastChange = name ?? "unknown"; Interlocked.Increment(ref Generation); } }
            watcher = new(directory) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Security, InternalBufferSize = 65536 };
            watcher.Changed += (_, e) => Changed(e.Name);
            watcher.Created += (_, e) => Changed(e.Name, true);
            watcher.Deleted += (_, e) => Changed(e.Name, true);
            watcher.Renamed += (_, e) => { Changed(e.OldName, true); Changed(e.Name, true); };
            watcher.Error += (_, _) => Interlocked.Increment(ref Generation);
            watcher.EnableRaisingEvents = true;
        }
        public void Dispose() => watcher.Dispose();
    }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<Verification>> Verifications = new(StringComparer.OrdinalIgnoreCase);
    public static void ReleaseVerificationHandles()
    {
        foreach (var pair in Verifications.ToArray()) if (Verifications.TryRemove(pair.Key, out var item) && item.IsValueCreated) item.Value.Dispose();
    }
    internal static void VerifyNativeDirectory(string feature, string directory, CancellationToken token)
    {
        var prefix = feature switch { "speech" => "whisper/Release/", "archives" => "sevenzip/x64/", "pdf-metadata" => "qpdf-engine/", "office" => "office/", _ => throw new ArgumentException("Unknown native component.") };
        directory = Path.GetFullPath(directory); Guard(directory);
        if (!Directory.Exists(directory)) throw new NotSupportedException("Install the matching local component pack first.");
        var files = Catalog.Value.Where(x => x.Value.Feature == feature && x.Key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        var canonicalDirectory = directory;
        var state = Verifications.GetOrAdd(feature + ":" + directory, _ => new(() => new(canonicalDirectory, files.Select(x => x.Key[prefix.Length..])))).Value;
        lock (state.Gate)
        {
        token.ThrowIfCancellationRequested(); var generation = Interlocked.Read(ref state.Generation);
        if (state.Verified == generation) return;
        if (files.Length == 0) throw new NotSupportedException("No trusted native code catalog is available for this component.");
        foreach (var pair in files)
        {
            token.ThrowIfCancellationRequested(); var path = Path.Combine(directory, pair.Key[prefix.Length..]);
            Guard(Path.GetDirectoryName(path)!);
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).Length != pair.Value.Bytes || !Digest(path).Equals(pair.Value.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The local native component failed its build-pinned checksum verification.");
        }
        if (generation != Interlocked.Read(ref state.Generation)) throw new IOException("The component changed while it was being verified (" + state.LastChange + "). Retry after changes stop.");
        state.Verified = generation;
        }
    }
}
