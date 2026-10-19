using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Naqla.Conversion;
using Naqla.Core;

internal static partial class Program
{
    private static void RunArchiveTests()
    {
        Run("ZIP/7Z/TAR real creation/extraction preserves nested/empty directories, Unicode and hashes", ArchivesRoundtrip);
        Run("Archive traversal, device names, ADS, absolute and case/path collisions are refused", ArchivesUnsafe);
        Run("Archive expansion limits, corruption, cancellation and original preservation", ArchivesFailures);
    }
    private static ArchiveService Archives() => new(Path.Combine(Components, "sevenzip", "x64"));
    private static void ArchivesRoundtrip()
    {
        var root = Path.Combine(folder, "ملفات الأرشيف"); Directory.CreateDirectory(Path.Combine(root, "فرعي", "فارغ"));
        File.WriteAllText(Path.Combine(root, "عربي.txt"), "نقلة offline content"); File.WriteAllBytes(Path.Combine(root, "فرعي", "binary.bin"), Enumerable.Range(0, 256).Select(x => (byte)x).ToArray());
        var hashes = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(x => Path.GetRelativePath(root, x), x => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x))));
        foreach (var format in new[] { "ZIP", "7Z", "TAR" })
        {
            var created = Archives().CreateAsync([root], format).GetAwaiter().GetResult();
            var extracted = Archives().ExtractAsync(FileClassifier.Inspect(created.OutputPath)).GetAwaiter().GetResult();
            foreach (var pair in hashes)
            {
                var result = Path.Combine(extracted.OutputPath, Path.GetFileName(root), pair.Key); Check(File.Exists(result) && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(result))) == pair.Value, "Archive hash mismatch " + format);
                Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, pair.Key)))) == pair.Value, "Archive source modified");
            }
            Check(Directory.Exists(Path.Combine(extracted.OutputPath, Path.GetFileName(root), "فرعي", "فارغ")), "Empty directory lost " + format);
            var collision = Archives().ExtractAsync(FileClassifier.Inspect(created.OutputPath)).GetAwaiter().GetResult(); Check(collision.OutputPath != extracted.OutputPath, "Extract collision overwritten");
        }
    }
    private static void ZipFixture(string path, params string[] entries)
    {
        using var file = File.Create(path); using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        foreach (var name in entries) { using var text = new StreamWriter(zip.CreateEntry(name).Open()); text.Write("unsafe test content"); }
    }
    private static void ArchivesUnsafe()
    {
        var n = 0;
        foreach (var entries in new[] { new[] { "../escape.txt" }, new[] { "/absolute.txt" }, new[] { "C:/outside.txt" }, new[] { "file.txt:ads" }, new[] { "CON.txt" }, new[] { "trailing. /x" }, new[] { "same", "SAME" }, new[] { "file", "file/child" } })
        {
            var path = Path.Combine(folder, $"unsafe-{n++}.zip"); ZipFixture(path, entries); var rejected = false;
            try { Archives().ExtractAsync(FileClassifier.Inspect(path)).GetAwaiter().GetResult(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Unsafe archive accepted: " + entries[0]);
        }
        Check(!File.Exists(Path.Combine(folder, "escape.txt")) && !Directory.GetDirectories(folder, ".naqla-*").Any(), "Unsafe extraction left output");
    }
    private static void ArchivesFailures()
    {
        var path = Path.Combine(folder, "limited.zip"); ZipFixture(path, "one.txt", "two.txt"); var hash = SHA256.HashData(File.ReadAllBytes(path));
        foreach (var limits in new[] { new ArchiveLimits(MaximumEntries: 1), new ArchiveLimits(MaximumBytes: 3), new ArchiveLimits(MaximumFileBytes: 2) })
        {
            var rejected = false; try { Archives().ExtractAsync(FileClassifier.Inspect(path), limits).GetAwaiter().GetResult(); } catch (NotSupportedException) { rejected = true; } Check(rejected, "Expansion limit ignored");
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var cancelled = false;
        try { Archives().ExtractAsync(FileClassifier.Inspect(path), token: cancellation.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; } Check(cancelled, "Archive cancellation ignored");
        var corrupt = Path.Combine(folder, "corrupt-archive.zip"); File.WriteAllText(corrupt, "not an archive"); var failed = false;
        try { Archives().ExtractAsync(FileClassifier.Inspect(corrupt)).GetAwaiter().GetResult(); } catch (Exception error) when (error is not OutOfMemoryException) { failed = true; } Check(failed, "Corrupt archive accepted");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Archive source changed");
        Check(!Directory.GetDirectories(folder, ".naqla-*").Any(), "Archive failure left temporary work");
    }
}
