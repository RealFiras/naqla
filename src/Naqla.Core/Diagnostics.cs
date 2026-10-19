namespace Naqla.Core;

// Deliberately closed schema: callers cannot pass filenames, paths, window titles,
// exception messages, arbitrary text, or unrelated keyboard input to the logger.
public enum DiagnosticEvent
{
    SessionStarted, AppStarting, AppReady, AppStartupFailed, UnhandledError, AppExitRequested, AppStopped,
    DiagnosticsEnabled, DiagnosticsDisabled, TestMarked, LogLimitReached,
    MouseHookInstalled, KeyboardHookInstalled, MouseHookInstallFailed, KeyboardHookInstallFailed, HooksReleased,
    ShellLeftDown, DragThresholdPassed, LeftReleased, ShiftDown, ShiftUp, EscapeDuringGesture,
    GestureCandidate, GestureSuppressed, CandidateDispatched, CandidateIgnored,
    OverlayShowRequested, OverlayHandleCreated, OverlayPlacement, OverlayShown, OverlayFailed,
    OverlayClosed, OverlayUnconfirmedTimeout,
    OleAllowDropEnabled, OleAllowDropDisabled, OleDragEnter, OleFileConfirmed, OleRejected,
    OleFirstDragOver, OleSelectionChanged, OleDragLeave, OleDropReceived, OleDropAccepted, OleSessionEnded,
    ControlDown, ControlUp, ModeChanged, ConversionStarted, ConversionSucceeded, ConversionFailed, ConversionCancelled, OperationInputRequested, OperationInputAccepted,
    TrayVisibleRequested, TrayRemoved, NotificationRequested, StartupSettingChanged, SelectionAnalyzed, BatchProgressChanged, BatchFinished,
    OptimizationPreviewStarted, OptimizationPreviewMeasured, OptimizationPreviewFailed, OptimizationPreviewCancelled,
    PdfEditorOpened, PdfPreviewStarted, PdfPreviewReady, PdfPreviewFailed, PdfPreviewCancelled, ToolsPageChanged
}
public enum ShellOrigin { Unknown, Explorer, Desktop, Other }
public enum DiagnosticReason
{
    None, LeftNotDown, BelowDragThreshold, NonShellOrigin, AlreadyArmed, Busy, Exiting, AlreadyVisible,
    Escape, MouseReleased, OleEnded, AcceptedDrop, Exit, SessionEnding, NoOleConfirmation,
    NativeCallFailed, NotFileDrop, NotSingleFile, UnsupportedExtension, MetadataFailure,
    NoConfirmedFile, DisabledSelection, CopyNotAllowed, PayloadMismatch, DropFinished, NoCommonAction
}
public enum DiagnosticError { None, IO, Unauthorized, NotSupported, Argument, Win32, COM, Cancelled, OutOfMemory, Other }
public enum DiagnosticFormat { Unknown, PNG, JPG, JPEG, WEBP, AVIF, HEIC, TIFF, BMP, MP4, MOV, MKV, WEBM, AVI, MP3, WAV, FLAC, M4A, AIFF, OPUS, PDF, DOCX, TXT, RTF, GIF }
public enum DiagnosticOperation { Unknown, Convert, Compress, Crop, Rotate, StripMetadata, MergePdf, Mute, Rename, UndoRename, TargetSize, PdfReorder, PdfExtract, PdfDelete, PdfMerge, PdfNumbers, PdfWatermark, PdfRotate, Trim, Frames, Normalize, Silence, Channels, Background, Ocr, Subtitles, Privacy, Archive, ExtractArchive, Plugin }

public sealed record DiagnosticData
{
    public bool? LeftDown { get; init; }
    public bool? ThresholdPassed { get; init; }
    public bool? Armed { get; init; }
    public bool? Enabled { get; init; }
    public bool? Visible { get; init; }
    public bool? CopyAllowed { get; init; }
    public bool? CopyEffect { get; init; }
    public bool? Tools { get; init; }
    public bool? Busy { get; init; }
    public bool? Succeeded { get; init; }
    public ShellOrigin? Origin { get; init; }
    public DiagnosticReason? Reason { get; init; }
    public DiagnosticError? Error { get; init; }
    public int? ErrorCode { get; init; }
    public int? Count { get; init; }
    public int? CompletedCount { get; init; }
    public int? FailedCount { get; init; }
    public int? CancelledCount { get; init; }
    public int? ConversionOptionsCount { get; init; }
    public int? ToolOptionsCount { get; init; }
    public long? Bytes { get; init; }
    public long? TargetBytes { get; init; }
    public long? ResultBytes { get; init; }
    public DiagnosticFormat? Format { get; init; }
    public DiagnosticFormat? Target { get; init; }
    public DiagnosticOperation? Operation { get; init; }
    public FileCategory? Category { get; init; }
    public int? Dpi { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? Page { get; init; }

    public static DiagnosticData FromFile(DraggedFile file) => new()
    {
        Format = FormatOf(file.Extension), Category = file.Category, Bytes = file.Size
    };
    public static DiagnosticFormat FormatOf(string? extension) =>
        Enum.TryParse<DiagnosticFormat>(extension, true, out var format) && Enum.IsDefined(format)
            ? format : DiagnosticFormat.Unknown;
    public static DiagnosticOperation OperationOf(string? id) => id switch
    {
        "COMPRESS" => DiagnosticOperation.Compress, "ROTATE" => DiagnosticOperation.Rotate,
        "CROP" => DiagnosticOperation.Crop, "MERGE" => DiagnosticOperation.MergePdf,
        "STRIP" => DiagnosticOperation.StripMetadata, "MUTE" => DiagnosticOperation.Mute,
        "RENAME" => DiagnosticOperation.Rename,
        "TARGETSIZE" => DiagnosticOperation.TargetSize,
        "TRIM" => DiagnosticOperation.Trim, "FRAMES" => DiagnosticOperation.Frames, "NORMALIZE" => DiagnosticOperation.Normalize,
        "SILENCE" => DiagnosticOperation.Silence, "CHANNELS" => DiagnosticOperation.Channels, "BACKGROUND" => DiagnosticOperation.Background,
        "OCR" => DiagnosticOperation.Ocr, "SUBTITLE" => DiagnosticOperation.Subtitles, "PRIVACY" => DiagnosticOperation.Privacy,
        "ARCHIVE" => DiagnosticOperation.Archive, "EXTRACT" => DiagnosticOperation.ExtractArchive,
        not null when id.StartsWith("PLUGIN:", StringComparison.Ordinal) => DiagnosticOperation.Plugin,
        "PDFREORDER" => DiagnosticOperation.PdfReorder, "PDFEXTRACT" => DiagnosticOperation.PdfExtract,
        "PDFDELETE" => DiagnosticOperation.PdfDelete, "PDFMERGE" => DiagnosticOperation.PdfMerge,
        "PDFNUMBERS" => DiagnosticOperation.PdfNumbers, "PDFWATERMARK" => DiagnosticOperation.PdfWatermark, "PDFROTATE" => DiagnosticOperation.PdfRotate,
        null => DiagnosticOperation.Unknown, _ => DiagnosticOperation.Convert
    };
    public static DiagnosticData FromException(Exception error) => new()
    {
        Error = error switch
        {
            UnauthorizedAccessException => DiagnosticError.Unauthorized,
            IOException => DiagnosticError.IO,
            NotSupportedException => DiagnosticError.NotSupported,
            ArgumentException => DiagnosticError.Argument,
            System.ComponentModel.Win32Exception => DiagnosticError.Win32,
            System.Runtime.InteropServices.COMException => DiagnosticError.COM,
            OperationCanceledException => DiagnosticError.Cancelled,
            OutOfMemoryException => DiagnosticError.OutOfMemory,
            _ => DiagnosticError.Other
        },
        ErrorCode = error is System.ComponentModel.Win32Exception native ? native.NativeErrorCode : error.HResult
    };
}

public interface IDiagnostics
{
    long BeginAttempt();
    void Write(DiagnosticEvent eventId, DiagnosticData? data = null);
}
public sealed class NullDiagnostics : IDiagnostics
{
    public static NullDiagnostics Instance { get; } = new();
    private NullDiagnostics() { }
    public long BeginAttempt() => 0;
    public void Write(DiagnosticEvent eventId, DiagnosticData? data = null) { }
}
