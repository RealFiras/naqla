using System.IO;

namespace Naqla.Conversion;

/// <summary>An owned, unpublished sibling directory. Cleanup never follows reparse points.</summary>
internal sealed class OutputDirectory : IDisposable
{
    private readonly string parent, stem;
    private bool published;
    public string Temporary { get; }
    public OutputDirectory(string source, string suffix)
    {
        parent = Path.GetDirectoryName(Path.GetFullPath(source))!;
        stem = Path.GetFileNameWithoutExtension(source) + suffix;
        Temporary = Path.Combine(parent, ".naqla-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Temporary);
    }
    public string Publish(CancellationToken token)
    {
        for (var i = 0; i < 10000; i++)
        {
            token.ThrowIfCancellationRequested();
            var target = Path.Combine(parent, stem + (i == 0 ? "" : $" ({i})"));
            try { Directory.Move(Temporary, target); published = true; return target; }
            catch (IOException) when (File.Exists(target) || Directory.Exists(target)) { }
        }
        throw new IOException("No available output folder name was found.");
    }
    internal static void DeleteOwned(string directory)
    {
        if (!Directory.Exists(directory)) return;
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) { Directory.Delete(directory); return; }
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.Directory) != 0) DeleteOwned(entry);
            else File.Delete(entry);
        }
        Directory.Delete(directory);
    }
    public void Dispose() { if (!published) DeleteOwned(Temporary); }
}
