using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Naqla.Core;

namespace Naqla.Conversion;

/// <summary>Plans without mutation, then stages locked originals and renames those same files.
/// Never copies/deletes file contents. Confirmation and fresh identity checks are mandatory.</summary>
public sealed class SmartRenameService(IRenameFileSystem fileSystem, string? recoveryDirectory = null) : ISmartRenameService
{
    private readonly string recoveryRoot = recoveryDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Naqla", "RenameRecovery");
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    public string RecoveryDirectoryPath => recoveryRoot;
    public Task<RenamePlan> PreviewAsync(IReadOnlyList<DraggedFile> files, RenameSettings settings, CancellationToken token = default)
    {
        var snapshot = files.ToArray();
        return Task.Run(() =>
        {
            if (snapshot.Length is < 1 or > FileDragPayload.MaximumFiles) throw new ArgumentException("اختر 1–200 ملف");
            if (settings.Pattern.Length > 512 || settings.Find.Length > 512 || settings.Replace.Length > 512 || settings.Prefix.Length > 256 || settings.Suffix.Length > 256)
                throw new ArgumentException("اختصر إعدادات الاسم");
            if (settings.Start is < 0 or > 1_000_000_000 || settings.Padding is < 1 or > 9) throw new ArgumentException("تحقق من بداية التسلسل وعدد الخانات");
            if (settings.DateFormat is not ("yyyy-MM-dd" or "yyyyMMdd" or "yyyy-MM-dd_HHmm")) throw new ArgumentException("صيغة التاريخ غير مدعومة");
            var regex = settings.Regex && settings.Find.Length > 0 ? new Regex(settings.Find, RegexOptions.CultureInvariant | (settings.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None), RegexTimeout) : null;
            var today = DateTime.Now;
            var rows = new List<RenameEntry>();
            for (var index = 0; index < snapshot.Length; index++)
            {
                token.ThrowIfCancellationRequested(); var source = snapshot[index]; RenameFileState? state = null;
                try
                {
                    state = fileSystem.Capture(source.FullPath);
                    var original = settings.PreserveExtension ? Path.GetFileNameWithoutExtension(source.FullPath) : Path.GetFileName(source.FullPath);
                    var changed = settings.Find.Length == 0 ? original : regex is not null ? regex.Replace(original, settings.Replace)
                        : original.Replace(settings.Find, settings.Replace, settings.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                    var date = settings.DateSource switch { RenameDateSource.Created => DateTime.FromFileTimeUtc(state.CreatedFileTime).ToLocalTime(), RenameDateSource.Today => today, _ => DateTime.FromFileTimeUtc(state.ModifiedFileTime).ToLocalTime() };
                    var name = settings.Prefix + Expand(settings.Pattern, changed, (settings.Start + index).ToString("D" + settings.Padding, CultureInfo.InvariantCulture), date.ToString(settings.DateFormat, CultureInfo.InvariantCulture)) + settings.Suffix;
                    name = settings.Case switch { RenameCase.Lower => name.ToLowerInvariant(), RenameCase.Upper => name.ToUpperInvariant(), RenameCase.Title => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant()), _ => name };
                    if (settings.Cleanup) name = Clean(name);
                    if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("اسم الملف فارغ");
                    if (settings.PreserveExtension) name += Path.GetExtension(source.FullPath);
                    ValidateLeaf(name);
                    rows.Add(new(source, state, Path.Combine(Path.GetDirectoryName(source.FullPath)!, name)));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ArgumentException or NotSupportedException or RegexMatchTimeoutException)
                { rows.Add(new(source, state, source.FullPath, error is RegexMatchTimeoutException ? "تجاوز النمط مهلة 100 ms" : error.Message)); }
            }
            return ValidateCollisions(rows);
        }, token);
    }
    public Task<RenamePlan> PreviewUndoAsync(RenameReceipt receipt, CancellationToken token = default) => Task.Run(() =>
    {
        var rows = receipt.Files.Select(file =>
        {
            token.ThrowIfCancellationRequested(); RenameFileState? current = null; string? error = null;
            try { current = fileSystem.Capture(file.After); if (current != file.State) error = "تغير الملف منذ إعادة التسمية؛ التراجع غير آمن"; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or System.ComponentModel.Win32Exception) { error = "تعذر التحقق من الملف للتراجع"; }
            return new RenameEntry(new(file.After, Path.GetExtension(file.After).TrimStart('.').ToUpperInvariant(), current?.Length ?? 0, FileClassifier.Category(Path.GetExtension(file.After))), current, file.Before, error);
        }).ToList();
        return ValidateCollisions(rows);
    }, token);

    public Task<ConversionResult> ExecuteAsync(RenamePlan plan, bool confirmed, CancellationToken token = default) => Task.Run(() => Execute(plan, confirmed, token));
    private ConversionResult Execute(RenamePlan plan, bool confirmed, CancellationToken token)
    {
        if (!confirmed) throw new ArgumentException("تأكيد المستخدم مطلوب قبل تغيير الأسماء");
        if (!plan.IsValid || plan.ChangedCount == 0) throw new ArgumentException("راجع التعارضات والأسماء قبل التأكيد");
        if (plan.Entries.Any(x => !Path.IsPathFullyQualified(x.Source.FullPath) || !Path.IsPathFullyQualified(x.TargetPath))) throw new ArgumentException("المعاينة تتطلب مسارات محلية كاملة");
        var checkedPlan = ValidateCollisions(plan.Entries.ToList());
        if (!checkedPlan.IsValid) throw new IOException("تغيرت الأسماء المتاحة؛ أعد المعاينة");
        var locks = new List<(RenameEntry Row, IRenameFileHandle File, string Stage)>();
        string? journalPath = null;
        try
        {
            var id = Guid.NewGuid().ToString("N");
            foreach (var row in plan.Entries)
            {
                token.ThrowIfCancellationRequested(); ValidateLeaf(Path.GetFileName(row.TargetPath));
                if (!string.Equals(Path.GetDirectoryName(row.Source.FullPath), Path.GetDirectoryName(row.TargetPath), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("إعادة التسمية لا تنقل الملفات إلى مجلد آخر");
                var handle = fileSystem.Lock(row.Source.FullPath);
                if (handle.State != row.State) { handle.Dispose(); throw new IOException("تغير الملف بعد المعاينة؛ أعد التأكيد على معاينة جديدة"); }
                locks.Add((row, handle, $"Naqla-Rename-Recovery-{id}-{locks.Count}{Path.GetExtension(row.Source.FullPath)}"));
            }
            // A recovery map is separate from diagnostics; it contains local paths, never file contents.
            Directory.CreateDirectory(recoveryRoot);
            journalPath = Path.Combine(recoveryRoot, $"rename-{id}.json");
            using (var stream = new FileStream(journalPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                try { JsonSerializer.Serialize(stream, new { Schema = 1, Entries = locks.Select(x => new { Before = x.Row.Source.FullPath, After = x.Row.TargetPath, Recovery = Path.Combine(Path.GetDirectoryName(x.Row.Source.FullPath)!, x.Stage), x.Row.State }) }); stream.Flush(true); }
                catch { stream.Dispose(); TryRemoveRecord(journalPath); throw; }
            }
            try
            {
                foreach (var entry in locks.Where(x => x.Row.Changes)) { token.ThrowIfCancellationRequested(); entry.File.RenameToLeaf(entry.Stage); }
                foreach (var entry in locks.Where(x => x.Row.Changes)) { token.ThrowIfCancellationRequested(); entry.File.RenameToLeaf(Path.GetFileName(entry.Row.TargetPath)); }
            }
            catch
            {
                // Re-stage all moved objects before restoring names, so swaps/case-only changes roll back too.
                var rollbackFailed = false;
                foreach (var entry in locks.Where(x => x.Row.Changes && x.File.CurrentPath != x.Row.Source.FullPath))
                    try { if (Path.GetFileName(entry.File.CurrentPath) != entry.Stage) entry.File.RenameToLeaf(entry.Stage); } catch { rollbackFailed = true; }
                foreach (var entry in locks.Where(x => x.Row.Changes && x.File.CurrentPath != x.Row.Source.FullPath))
                    try { entry.File.RenameToLeaf(Path.GetFileName(entry.Row.Source.FullPath)); } catch { rollbackFailed = true; }
                if (rollbackFailed) throw new RenameRecoveryException(journalPath);
                TryRemoveRecord(journalPath); journalPath = null;
                throw;
            }
            var changed = locks.Where(x => x.Row.Changes).Select(x => new RenamedFile(x.Row.Source.FullPath, x.File.CurrentPath, x.File.State)).ToArray();
            TryRemoveRecord(journalPath); journalPath = null;
            return new(changed[0].After, 0, 0, changed.Length, Array.AsReadOnly(changed.Select(x => x.After).ToArray()), new(changed));
        }
        finally { foreach (var entry in locks) entry.File.Dispose(); }
    }
    private static void TryRemoveRecord(string path)
    { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    private static RenamePlan ValidateCollisions(List<RenameEntry> rows)
    {
        var sources = rows.Select(x => x.Source.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicates = rows.GroupBy(x => x.TargetPath, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicateSources = sources.Count != rows.Count;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; if (row.Error is not null) continue;
            var error = duplicateSources ? "المصدر نفسه مكرر" : duplicates.Contains(row.TargetPath) ? "تعارض داخل المجموعة" : Directory.Exists(row.TargetPath) || File.Exists(row.TargetPath) && !sources.Contains(row.TargetPath) ? "الاسم مستخدم؛ لن يُستبدل الملف الموجود" : null;
            rows[i] = row with { Error = error };
        }
        return new(rows);
    }
    public static void ValidateLeaf(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 240 || name is "." or ".." || name[^1] is '.' or ' ' || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("اسم غير صالح أو أطول من 240 حرفًا");
        var first = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (Reserved(first)) throw new ArgumentException("اسم جهاز محجوز في Windows");
    }
    private static bool Reserved(string name) => name is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
        name.Length == 4 && (name.StartsWith("COM", StringComparison.Ordinal) || name.StartsWith("LPT", StringComparison.Ordinal)) && "123456789¹²³".Contains(name[3]);
    private static string Clean(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        cleaned = Regex.Replace(cleaned, @"\s+", " ", RegexOptions.CultureInvariant, RegexTimeout).Trim().TrimEnd('.', ' ');
        if (Reserved(cleaned.Split('.')[0].ToUpperInvariant())) cleaned = "_" + cleaned;
        return cleaned;
    }
    private static string Expand(string pattern, string name, string number, string date)
    {
        var output = new StringBuilder();
        for (var index = 0; index < pattern.Length; index++)
        {
            var c = pattern[index];
            if ((c == '{' || c == '}') && index + 1 < pattern.Length && pattern[index + 1] == c) { output.Append(c); index++; continue; }
            if (c == '}') throw new ArgumentException("قوس غير مكتمل في قالب الاسم");
            if (c != '{') { output.Append(c); continue; }
            var end = pattern.IndexOf('}', index + 1); if (end < 0) throw new ArgumentException("قوس غير مكتمل في قالب الاسم");
            output.Append(pattern[(index + 1)..end] switch { "name" => name, "n" => number, "date" => date, _ => throw new ArgumentException("الرموز المتاحة: {name} {n} {date}") });
            index = end;
        }
        return output.ToString();
    }
}
