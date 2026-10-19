namespace Naqla.Core;

public enum JobOutcome { Succeeded, Failed, Cancelled }
public enum JobFailureKind { PermissionDenied, InvalidInput, Unsupported, IO, Other, RenameRecovery, ImageTargetNotMet }
public sealed record JobFailure(JobFailureKind Kind, int ErrorCode, string? LocalRecoveryRecordPath = null, ImageOptimizationReport? Optimization = null)
{
    public string Message => Kind switch
    {
        JobFailureKind.PermissionDenied => "لا توجد صلاحية للقراءة أو الحفظ في مجلد المصدر",
        JobFailureKind.InvalidInput => "الملف تالف أو إعدادات العملية لا تناسبه",
        JobFailureKind.Unsupported => "المحرك المحلي لا يدعم هذه العملية لهذا الملف",
        JobFailureKind.IO => "تعذر قراءة الملف أو حفظ الناتج؛ تحقق من المساحة والمجلد",
        JobFailureKind.RenameRecovery => "تحتاج بعض الأسماء استردادًا؛ الملفات لم تُحذف. الخريطة المحلية: " + LocalRecoveryRecordPath,
        JobFailureKind.ImageTargetNotMet => "لم يتحقق الحجم؛ لم تُحفظ نسخة. " + Optimization?.Description,
        _ => "فشلت المعالجة المحلية؛ لم يتغير الملف الأصلي"
    };
    public static JobFailure FromException(Exception error) => new(error switch
    {
        RenameRecoveryException => JobFailureKind.RenameRecovery,
        ImageTargetNotMetException => JobFailureKind.ImageTargetNotMet,
        UnauthorizedAccessException => JobFailureKind.PermissionDenied,
        InvalidDataException or ArgumentException => JobFailureKind.InvalidInput,
        NotSupportedException => JobFailureKind.Unsupported,
        IOException => JobFailureKind.IO,
        _ => JobFailureKind.Other
    }, error.HResult, error is RenameRecoveryException recovery ? recovery.RecordPath : null, error is ImageTargetNotMetException target ? target.Report : null);
}

public sealed record BatchItemResult(int Index, IReadOnlyList<DraggedFile> Sources, JobOutcome Outcome,
    ConversionResult? Output = null, JobFailure? Failure = null);
public sealed record BatchProgress(int Total, int Completed, int Succeeded, int Failed, int Current,
    BatchItemResult? LastFinished = null)
{
    public int Percent => Total == 0 ? 0 : (int)(100L * Completed / Total);
}
public sealed record BatchResult(IReadOnlyList<BatchItemResult> Jobs)
{
    public int Succeeded => Jobs.Count(x => x.Outcome == JobOutcome.Succeeded);
    public int Failed => Jobs.Count(x => x.Outcome == JobOutcome.Failed);
    public int Cancelled => Jobs.Count(x => x.Outcome == JobOutcome.Cancelled);
    public int Completed => Succeeded + Failed;
    public IReadOnlyList<string> OutputDirectories => Jobs.Where(x => x.Output is not null)
        .SelectMany(x => x.Output!.OutputPaths ?? new[] { x.Output.OutputPath })
        .Select(path => Directory.Exists(path) ? path : Path.GetDirectoryName(path)!)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
