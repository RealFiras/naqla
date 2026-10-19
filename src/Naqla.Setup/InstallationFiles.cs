using System.IO.Compression;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Naqla.Setup;

internal sealed record InstalledFile(string Path, string Sha256);
internal sealed record InstallationManifest(int Schema, InstalledFile[] Files);
internal static class InstallationFiles
{
    internal const string ManifestName = ".naqla-installed-files.json";
    internal static void GuardDirectory(string directory)
    {
        for (var item = new DirectoryInfo(System.IO.Path.GetFullPath(directory)); item is not null; item = item.Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Installation paths must not traverse junctions/symbolic links.");
    }
    internal static string Target(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || System.IO.Path.IsPathRooted(relative) || relative.Contains(':') || relative.Split('/', '\\').Any(x => x is "" or "." or ".." || x.EndsWith('.') || x.EndsWith(' '))) throw new InvalidDataException("Invalid installation file path.");
        foreach (var part in relative.Split('/', '\\'))
        {
            var name = part.Split('.')[0].ToUpperInvariant();
            if (part.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || name is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" || name.Length == 4 && (name.StartsWith("COM") || name.StartsWith("LPT")) && (name[3] is >= '1' and <= '9' or '¹' or '²' or '³')) throw new InvalidDataException("Reserved installation path.");
        }
        var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
        if (!target.StartsWith(System.IO.Path.GetFullPath(root).TrimEnd('\\') + '\\', StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Installation file escaped its directory.");
        GuardDirectory(System.IO.Path.GetDirectoryName(target)!);
        if (File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new IOException("Installation files must not be links.");
        return target;
    }
    internal static string Hash(string path) { using var read = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); return Convert.ToHexString(SHA256.HashData(read)); }
    internal static List<string> Extract(Stream payload, string directory)
    {
        GuardDirectory(directory); Directory.CreateDirectory(directory); using var zip = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > 32000 || zip.Entries.Sum(x => x.Length) > 3_221_225_472L) throw new InvalidDataException("Installation payload limit exceeded.");
        var files = new List<string>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            if (entry.Name.Length == 0) continue;
            if (!names.Add(entry.FullName) || (entry.ExternalAttributes >> 16 & 0xf000) == 0xa000) throw new InvalidDataException("Duplicate/linked installation payload entry.");
            var target = Target(directory, entry.FullName); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            // Fresh staged files only. A stale/hardlinked existing target is never opened for writing.
            if (File.Exists(target) || Directory.Exists(target)) throw new IOException("Extraction requires unoccupied payload filenames.");
            using var input = entry.Open(); using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None); input.CopyTo(output);
            if (output.Length != entry.Length) throw new InvalidDataException("Installation extraction size mismatch.");
            files.Add(System.IO.Path.GetRelativePath(directory, target));
        }
        return files;
    }
    internal static void WriteManifest(string directory, IEnumerable<string> files)
    {
        var record = new InstallationManifest(2, files.Select(x => new InstalledFile(x, Hash(Target(directory, x)))).ToArray());
        using var output = new FileStream(System.IO.Path.Combine(directory, ManifestName), FileMode.CreateNew, FileAccess.Write); JsonSerializer.Serialize(output, record);
    }
    internal static Dictionary<string, string?> ReadOwned(string directory, bool allowLegacy)
    {
        var path = Target(directory, ManifestName); if (!File.Exists(path)) throw new IOException("Installation ownership manifest is missing; upgrade/removal stopped to protect existing files.");
        if (new FileInfo(path).Length > 8_388_608) throw new InvalidDataException("Oversized installation manifest.");
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var entries = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (json.RootElement.ValueKind == JsonValueKind.Array && allowLegacy)
            foreach (var item in json.RootElement.EnumerateArray()) entries.Add(item.GetString()!, null);
        else
        {
            var record = json.RootElement.Deserialize<InstallationManifest>() ?? throw new InvalidDataException("Invalid installation manifest.");
            if (record.Schema != 2 || record.Files.Length > 32000) throw new InvalidDataException("Unsupported installation manifest.");
            foreach (var entry in record.Files)
            { if (entry.Sha256.Length != 64 || entry.Sha256.Any(x => !Uri.IsHexDigit(x))) throw new InvalidDataException("Invalid installation file hash."); entries.Add(entry.Path, entry.Sha256); }
        }
        foreach (var entry in entries) Target(directory, entry.Key); return entries;
    }
    internal static void PreserveUnowned(string oldDirectory, string stage)
    {
        var owned = ReadOwned(oldDirectory, allowLegacy: true);
        void Visit(string directory)
        {
            GuardDirectory(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry); if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Upgrade refuses linked/junction content in the installation directory.");
                var relative = System.IO.Path.GetRelativePath(oldDirectory, entry); var target = Target(stage, relative);
                if ((attributes & FileAttributes.Directory) != 0) { Directory.CreateDirectory(target); Visit(entry); continue; }
                if (relative == ManifestName) continue;
                if (owned.TryGetValue(relative, out var hash)) { if (hash is not null && Hash(entry) != hash) throw new IOException("An installed application file was modified. Back it up before upgrading."); continue; }
                if (File.Exists(target) || Directory.Exists(target)) throw new IOException("A user-added installation file conflicts with the new payload. Back it up before upgrading.");
                File.Copy(entry, target, false); if (Hash(entry) != Hash(target)) throw new IOException("User-added file preservation verification failed.");
            }
        }
        Visit(oldDirectory);
    }
    internal static void RemoveKnown(string directory)
    {
        GuardDirectory(directory); var owned = ReadOwned(directory, allowLegacy: false);
        foreach (var pair in owned)
        {
            var path = Target(directory, pair.Key); if (File.Exists(path) && Hash(path) == pair.Value) File.Delete(path);
        }
        File.Delete(Target(directory, ManifestName)); PruneEmpty(directory);
    }
    private static void PruneEmpty(string directory)
    {
        GuardDirectory(directory);
        foreach (var child in Directory.EnumerateDirectories(directory)) { if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) PruneEmpty(child); }
        if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
    }
    internal static void RemoveOwnedStage(string parent, string directory)
    {
        var resolved = System.IO.Path.GetFullPath(directory); var prefix = System.IO.Path.GetFullPath(parent).TrimEnd('\\') + '\\';
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !System.IO.Path.GetFileName(resolved).StartsWith(".Naqla-", StringComparison.Ordinal)) throw new InvalidOperationException("Cleanup target is not an owned installation stage.");
        if (!Directory.Exists(resolved)) return; GuardDirectory(resolved);
        void Remove(string path)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                var attributes = File.GetAttributes(entry); if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Cleanup refuses links in an installation stage.");
                if ((attributes & FileAttributes.Directory) != 0) Remove(entry); else File.Delete(entry);
            }
            Directory.Delete(path);
        }
        Remove(resolved);
    }
}
