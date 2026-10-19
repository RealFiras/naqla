using System.IO;

namespace Naqla.Conversion;

/// <summary>Owns only a random temporary file. Publication never overwrites an existing file.</summary>
internal sealed class OutputFile : IDisposable
{
    private readonly string directory, stem, extension;
    public string Temporary { get; }
    public OutputFile(string source, string extension, string suffix = "")
    {
        directory = Path.GetDirectoryName(source)!;
        stem = Path.GetFileNameWithoutExtension(source) + suffix;
        this.extension = extension.ToLowerInvariant();
        Temporary = Path.Combine(directory, $".naqla-{Guid.NewGuid():N}.{this.extension}");
    }
    public string Publish(CancellationToken token)
    {
        for (var i = 0; i < 10000; i++)
        {
            token.ThrowIfCancellationRequested();
            var target = Path.Combine(directory, $"{stem}{(i == 0 ? "" : $" ({i})")}.{extension}");
            try { File.Move(Temporary, target, false); return target; }
            catch (IOException) when (File.Exists(target) || Directory.Exists(target)) { }
        }
        throw new IOException("No available output name was found.");
    }
    public void Dispose() { if (File.Exists(Temporary)) File.Delete(Temporary); }
}
