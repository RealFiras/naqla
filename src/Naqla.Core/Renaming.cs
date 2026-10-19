namespace Naqla.Core;

public enum RenameCase { Keep, Lower, Upper, Title }
public enum RenameDateSource { Modified, Created, Today }
public sealed record RenameSettings(string Pattern = "{name}", string Find = "", string Replace = "", string Prefix = "", string Suffix = "",
    bool Regex = false, bool IgnoreCase = false, int Start = 1, int Padding = 3, RenameCase Case = RenameCase.Keep,
    bool Cleanup = false, bool PreserveExtension = true, RenameDateSource DateSource = RenameDateSource.Modified, string DateFormat = "yyyy-MM-dd");
public sealed record RenameFileState(uint Volume, ulong FileId, long Length, long ModifiedFileTime, long CreatedFileTime);
public sealed record RenameEntry(DraggedFile Source, RenameFileState? State, string TargetPath, string? Error = null)
{
    public string Before => Path.GetFileName(Source.FullPath);
    public string After => Path.GetFileName(TargetPath);
    public bool Changes => !string.Equals(Source.FullPath, TargetPath, StringComparison.Ordinal);
    public string Status => Error ?? (Changes ? "جاهز" : "دون تغيير");
}
public sealed class RenamePlan
{
    public IReadOnlyList<RenameEntry> Entries { get; }
    public bool IsValid => Entries.Count is > 0 and <= FileDragPayload.MaximumFiles && Entries.All(x => x.Error is null && x.State is not null);
    public int ChangedCount => Entries.Count(x => x.Changes);
    public RenamePlan(IEnumerable<RenameEntry> entries) => Entries = Array.AsReadOnly(entries.ToArray());
}
public sealed record RenamedFile(string Before, string After, RenameFileState State);
public sealed class RenameReceipt
{
    public IReadOnlyList<RenamedFile> Files { get; }
    public RenameReceipt(IEnumerable<RenamedFile> files) => Files = Array.AsReadOnly(files.ToArray());
}
public sealed class RenameRecoveryException(string recordPath) : IOException("تعذر استعادة كل الأسماء؛ الملفات لم تُحذف. خريطة الاسترداد المحلية: " + recordPath)
{
    public string RecordPath { get; } = recordPath;
}
public interface IRenameFileHandle : IDisposable
{
    RenameFileState State { get; }
    string CurrentPath { get; }
    void RenameToLeaf(string filename);
}
public interface IRenameFileSystem
{
    RenameFileState Capture(string path);
    IRenameFileHandle Lock(string path);
}
public interface ISmartRenameService
{
    Task<RenamePlan> PreviewAsync(IReadOnlyList<DraggedFile> files, RenameSettings settings, CancellationToken token = default);
    Task<RenamePlan> PreviewUndoAsync(RenameReceipt receipt, CancellationToken token = default);
    Task<ConversionResult> ExecuteAsync(RenamePlan plan, bool confirmed, CancellationToken token = default);
}
