using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Naqla.Core;
using SharpCompress.Archives;

namespace Naqla.Conversion;

public sealed record ArchiveLimits(int MaximumEntries = 10000, long MaximumBytes = 1_073_741_824, long MaximumFileBytes = 536_870_912, int MaximumRatio = 1000);
public sealed class ArchiveService(string sevenZipDirectory)
{
    private string SevenZip => Path.Combine(sevenZipDirectory, "7za.exe");
    public bool SevenZipAvailable => File.Exists(SevenZip);
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase) { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "CONIN$", "CONOUT$" };
    public static string SafeRelativePath(string key)
    {
        var name = key.Replace('\\', '/').TrimEnd('/');
        if (name.Length is < 1 or > 220 || name.StartsWith('/') || name.Contains(':') || name.Any(c => c < 32)) throw new InvalidDataException("Unsafe archive entry path.");
        foreach (var segment in name.Split('/'))
        {
            var stem = segment.Split('.')[0];
            if (segment is "" or "." or ".." || segment.EndsWith(' ') || segment.EndsWith('.') || segment.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0 || Reserved.Contains(stem) || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is '¹' or '²' or '³') throw new InvalidDataException("Unsafe archive entry filename.");
        }
        return name;
    }
    private static void LimitsValid(ArchiveLimits limits)
    { if (limits.MaximumEntries is < 1 or > 10000 || limits.MaximumBytes is < 1 or > 1_073_741_824 || limits.MaximumFileBytes is < 1 or > 536_870_912 || limits.MaximumRatio is < 1 or > 1000) throw new ArgumentException("Invalid archive resource limits."); }

    public Task<ConversionResult> ExtractAsync(DraggedFile source, ArchiveLimits? limits = null, CancellationToken token = default) => Task.Run(async () =>
    {
        limits ??= new(); LimitsValid(limits); token.ThrowIfCancellationRequested();
        using var input = new FileStream(source.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 1_073_741_824) throw new NotSupportedException("Archive inputs are limited to 1 GB.");
        if (source.Extension == "TAR")
        {
            using (var tar = new TarReader(input, leaveOpen: true))
            {
                TarEntry? item; var count = 0;
                while ((item = tar.GetNextEntry(copyData: false)) is not null)
                {
                    token.ThrowIfCancellationRequested(); if (++count > limits.MaximumEntries) throw new NotSupportedException("Too many TAR entries.");
                    SafeRelativePath(item.Name);
                    if (item.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.Directory)) throw new NotSupportedException("TAR links and special entries are refused.");
                }
            }
            input.Position = 0;
        }
        using var archive = ArchiveFactory.OpenArchive(input); using var output = new OutputDirectory(source.FullPath, " extracted");
        var entries = archive.Entries.Take(limits.MaximumEntries + 1).ToArray();
        if (entries.Length is < 1 || entries.Length > limits.MaximumEntries) throw new NotSupportedException("Archive entry count exceeds the configured limit.");
        var names = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase); long declared = 0;
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            var name = SafeRelativePath(entry.Key ?? "");
            int attributes; try { attributes = entry.Attrib.GetValueOrDefault(); } catch (NotImplementedException) when (source.Extension == "TAR") { attributes = 0; }
            var unixMode = (attributes >> 16) & 0xf000;
            if (entry.IsEncrypted) throw new NotSupportedException("Password-protected archives are not supported in this development build.");
            if (entry.LinkTarget is not null || (attributes & (int)FileAttributes.ReparsePoint) != 0 || unixMode is not (0 or 0x8000 or 0x4000) || !entry.IsComplete) throw new NotSupportedException("Archive links, special files and split volumes are refused.");
            if (!names.TryAdd(name, entry.IsDirectory)) throw new InvalidDataException("Duplicate or case-colliding archive entries.");
            if (entry.Size < 0 || entry.Size > limits.MaximumFileBytes || entry.CompressedSize > 0 && entry.Size / entry.CompressedSize > limits.MaximumRatio) throw new NotSupportedException("Archive expansion limit exceeded.");
            declared = checked(declared + entry.Size); if (declared > limits.MaximumBytes) throw new NotSupportedException("Archive expanded size exceeds the configured limit.");
        }
        foreach (var name in names.Keys)
        {
            var parts = name.Split('/'); var prefix = "";
            for (var i = 0; i < parts.Length - 1; i++) { prefix += (i == 0 ? "" : "/") + parts[i]; if (names.TryGetValue(prefix, out var directory) && !directory) throw new InvalidDataException("Archive file/directory paths conflict."); }
        }
        long actualTotal = 0; var hashes = new Dictionary<string, string>();
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested(); var relative = SafeRelativePath(entry.Key!); var path = Path.Combine(output.Temporary, relative.Replace('/', Path.DirectorySeparatorChar));
            if (entry.IsDirectory) { Directory.CreateDirectory(path); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var read = await entry.OpenEntryStreamAsync(token).ConfigureAwait(false); await using var write = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[65536]; long bytes = 0; int count;
            while ((count = await read.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
            {
                bytes += count; actualTotal += count;
                if (bytes > limits.MaximumFileBytes || actualTotal > limits.MaximumBytes || bytes > entry.Size) throw new InvalidDataException("Actual archive expansion exceeded its declared/configured limits.");
                hash.AppendData(buffer, 0, count); await write.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            }
            if (bytes != entry.Size) throw new InvalidDataException("Extracted archive size did not match its entry.");
            hashes.Add(relative, Convert.ToHexString(hash.GetHashAndReset()));
        }
        // Verify independent filesystem reads, not just counts or headers.
        foreach (var pair in hashes)
        {
            token.ThrowIfCancellationRequested(); using var check = File.OpenRead(Path.Combine(output.Temporary, pair.Key.Replace('/', Path.DirectorySeparatorChar)));
            if (Convert.ToHexString(SHA256.HashData(check)) != pair.Value) throw new InvalidDataException("Extracted content verification failed.");
        }
        return new ConversionResult(output.Publish(token), 0, 0, hashes.Count);
    }, token);

    public Task<ConversionResult> CreateAsync(IReadOnlyList<string> sources, string format, CancellationToken token = default) => Task.Run(async () =>
    {
        if (sources.Count is < 1 or > 200 || format is not ("ZIP" or "7Z" or "TAR")) throw new ArgumentException("Choose 1–200 local sources and ZIP, 7Z, or TAR.");
        if (format == "7Z" && !SevenZipAvailable) throw new NotSupportedException("The local 7-Zip component is missing.");
        if (format == "7Z") ComponentStore.VerifyNativeDirectory("archives", sevenZipDirectory, token);
        var full = sources.Select(Path.GetFullPath).ToArray();
        if (full.Any(x => x.StartsWith(@"\\", StringComparison.Ordinal)) || full.Distinct(StringComparer.OrdinalIgnoreCase).Count() != full.Length) throw new ArgumentException("Choose distinct local inputs.");
        var first = full[0];
        // For a selected directory the archive is a sibling, never recursively inside itself.
        var anchor = Directory.Exists(first) ? Path.Combine(Path.GetDirectoryName(first)!, Path.GetFileName(first) + ".archive") : first;
        using var work = new OutputDirectory(anchor, " archive-work"); var staged = Path.Combine(work.Temporary, "data"); Directory.CreateDirectory(staged);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var expected = new Dictionary<string, string>(); var dirs = new HashSet<string>(); long total = 0; var count = 0;
        async Task Stage(string path, string key)
        {
            token.ThrowIfCancellationRequested(); key = SafeRelativePath(key); if (!names.Add(key) || ++count > 10000) throw new ArgumentException("Archive source name collision or entry count exceeded.");
            var attributes = File.GetAttributes(path); if ((attributes & FileAttributes.ReparsePoint) != 0) throw new NotSupportedException("Archive creation refuses links and reparse points.");
            var destination = Path.Combine(staged, key.Replace('/', Path.DirectorySeparatorChar));
            if ((attributes & FileAttributes.Directory) != 0)
            {
                Directory.CreateDirectory(destination); dirs.Add(key);
                foreach (var child in Directory.EnumerateFileSystemEntries(path).Order(StringComparer.OrdinalIgnoreCase)) await Stage(child, key + "/" + Path.GetFileName(child)).ConfigureAwait(false);
            }
            else
            {
                using var read = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                total = checked(total + read.Length); if (read.Length > 536_870_912 || total > 1_073_741_824) throw new NotSupportedException("Archive source size limit exceeded.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); using var write = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
                await read.CopyToAsync(write, token).ConfigureAwait(false); read.Position = 0; expected.Add(key, Convert.ToHexString(SHA256.HashData(read)));
            }
        }
        foreach (var source in full) await Stage(source, Path.GetFileName(source)).ConfigureAwait(false);
        using var output = new OutputFile(anchor, format, format == "7Z" ? "" : " archive");
        if (format == "ZIP")
        {
            using var stream = new FileStream(output.Temporary, FileMode.CreateNew, FileAccess.Write);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
            foreach (var directory in dirs.Order(StringComparer.Ordinal)) { token.ThrowIfCancellationRequested(); zip.CreateEntry(directory.TrimEnd('/') + "/"); }
            foreach (var name in expected.Keys.Order(StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested(); using var source = new FileStream(Path.Combine(staged, name), FileMode.Open, FileAccess.Read, FileShare.Read);
                using var destination = zip.CreateEntry(name, CompressionLevel.Optimal).Open(); await source.CopyToAsync(destination, token).ConfigureAwait(false);
            }
        }
        else if (format == "TAR") await TarFile.CreateFromDirectoryAsync(staged, output.Temporary, false, token).ConfigureAwait(false);
        else
        {
            // The engine sees ASCII relative arguments; staged filenames are discovered internally (Unicode-safe).
            await LocalProcess.RunAsync(SevenZip, ["a", "-t7z", "-mx=5", "-mmt=2", "-bd", "-y", "../result.7z", "."], token, workingDirectory: staged).ConfigureAwait(false);
            File.Move(Path.Combine(work.Temporary, "result.7z"), output.Temporary, false);
        }
        token.ThrowIfCancellationRequested();
        using var inspected = ArchiveFactory.OpenArchive(output.Temporary); var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var actualDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in inspected.Entries)
        {
            token.ThrowIfCancellationRequested(); var name = SafeRelativePath(entry.Key!);
            var components = name.Split('/');
            for (var i = 1; i < components.Length; i++) actualDirs.Add(string.Join('/', components.Take(i)));
            if (entry.IsDirectory) { actualDirs.Add(name); continue; }
            if (!actual.Add(name) || !expected.TryGetValue(name, out var expectedHash)) throw new InvalidDataException("Archive round-trip entry mismatch.");
            using var decoded = entry.OpenEntryStream(); if (Convert.ToHexString(SHA256.HashData(decoded)) != expectedHash) throw new InvalidDataException("Archive round-trip content hash mismatch.");
        }
        if (actual.Count != expected.Count || !dirs.IsSubsetOf(actualDirs)) throw new InvalidDataException("Archive round-trip lost files or empty directories.");
        inspected.Dispose(); return new ConversionResult(output.Publish(token), 0, 0, actual.Count);
    }, token);
}
