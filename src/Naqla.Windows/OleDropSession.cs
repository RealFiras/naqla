using System.IO;
using System.Windows;
using System.Runtime.InteropServices;
using Naqla.Core;

namespace Naqla.Windows;

/// <summary>WPF AllowDrop registers the HWND's OLE IDropTarget. All paths come
/// exclusively from the source IDataObject's CF_HDROP, never Explorer selection.</summary>
public sealed class OleDropSession : IDisposable
{
    private readonly UIElement target;
    private readonly IDiagnostics diagnostics;
    private FileDragPayload? payload;
    private bool overLogged, lastCopy;
    private WheelOption? lastSelection;
    public event Action<FileDragPayload>? Entered;
    public event Action<Point>? Moved;
    public event Action? Left;
    public event Action<string>? Rejected;
    public event Action<FileDragPayload, WheelOption>? Accepted;
    public Func<WheelOption?>? Selection { get; set; }
    public bool HasFile => payload is not null;

    public OleDropSession(UIElement target, IDiagnostics? diagnostics = null)
    {
        this.target = target;
        this.diagnostics = diagnostics ?? NullDiagnostics.Instance;
        target.AllowDrop = true;
        this.diagnostics.Write(DiagnosticEvent.OleAllowDropEnabled);
        target.PreviewDragEnter += Enter;
        target.PreviewDragOver += Over;
        target.PreviewDragLeave += Leave;
        target.PreviewDrop += Drop;
    }
    private void Enter(object sender, DragEventArgs e)
    {
        Reset();
        diagnostics.Write(DiagnosticEvent.OleDragEnter, new() { CopyAllowed = e.AllowedEffects.HasFlag(DragDropEffects.Copy), LeftDown = e.KeyStates.HasFlag(DragDropKeyStates.LeftMouseButton) });
        try
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false))
            {
                diagnostics.Write(DiagnosticEvent.OleRejected, new() { Reason = DiagnosticReason.NotFileDrop });
                Rejected?.Invoke("A local FileDrop payload is required."); Over(sender, e); return;
            }
            if (e.Data.GetData(DataFormats.FileDrop, autoConvert: false) is not string[] paths || paths.Length is < 1 or > FileDragPayload.MaximumFiles)
            {
                diagnostics.Write(DiagnosticEvent.OleRejected, new() { Reason = DiagnosticReason.NotSingleFile });
                Rejected?.Invoke("Drag between 1 and 200 local files."); Over(sender, e); return;
            }
            var actual = FileDragPayload.Inspect(paths);
            payload = actual;
            diagnostics.Write(DiagnosticEvent.OleFileConfirmed, DiagnosticData.FromFile(actual.PreviewFile) with { Count = actual.Files.Count });
            Entered?.Invoke(actual);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or COMException)
        { diagnostics.Write(DiagnosticEvent.OleRejected, DiagnosticData.FromException(error) with { Reason = DiagnosticReason.MetadataFailure }); Rejected?.Invoke(error.Message); }
        Over(sender, e);
    }
    private void Over(object sender, DragEventArgs e)
    {
        Moved?.Invoke(e.GetPosition(target));
        // Never advertise MOVE. Explorer must preserve the original even with Shift.
        e.Effects = payload is not null && Selection?.Invoke() is { Enabled: true } && e.AllowedEffects.HasFlag(DragDropEffects.Copy)
            ? DragDropEffects.Copy : DragDropEffects.None;
        var selection = Selection?.Invoke();
        var copy = e.Effects == DragDropEffects.Copy;
        if (!overLogged) diagnostics.Write(DiagnosticEvent.OleFirstDragOver);
        if (!overLogged || lastSelection != selection || lastCopy != copy)
            diagnostics.Write(DiagnosticEvent.OleSelectionChanged, new() { Format = DiagnosticData.FormatOf(selection?.Id), Operation = DiagnosticData.OperationOf(selection?.Id), Enabled = selection?.Enabled ?? false, CopyEffect = copy });
        overLogged = true; lastSelection = selection; lastCopy = copy;
        e.Handled = true;
    }
    private void Leave(object sender, DragEventArgs e) { diagnostics.Write(DiagnosticEvent.OleDragLeave); Reset(); Left?.Invoke(); e.Handled = true; }
    private void Drop(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        e.Handled = true;
        diagnostics.Write(DiagnosticEvent.OleDropReceived);
        Moved?.Invoke(e.GetPosition(target));
        try
        {
            var option = Selection?.Invoke();
            if (payload is null) { diagnostics.Write(DiagnosticEvent.OleRejected, new() { Reason = DiagnosticReason.NoConfirmedFile }); return; }
            if (option is not { Enabled: true }) { diagnostics.Write(DiagnosticEvent.OleRejected, new() { Reason = DiagnosticReason.DisabledSelection }); return; }
            if (!e.AllowedEffects.HasFlag(DragDropEffects.Copy)) { diagnostics.Write(DiagnosticEvent.OleRejected, new() { Reason = DiagnosticReason.CopyNotAllowed }); return; }
            // Re-read the real drop payload rather than trusting cached hover information.
            if (e.Data.GetData(DataFormats.FileDrop, false) is string[] paths && payload.Matches(paths))
            {
                var dropped = FileDragPayload.Inspect(paths);
                e.Effects = DragDropEffects.Copy;
                diagnostics.Write(DiagnosticEvent.OleDropAccepted, new() { Format = DiagnosticData.FormatOf(option.Id), Operation = DiagnosticData.OperationOf(option.Id), CopyEffect = true, Count = dropped.Files.Count });
                Accepted?.Invoke(dropped, option);
            }
            else diagnostics.Write(DiagnosticEvent.OleRejected, new() { Reason = DiagnosticReason.PayloadMismatch });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or COMException)
        { diagnostics.Write(DiagnosticEvent.OleRejected, DiagnosticData.FromException(error) with { Reason = DiagnosticReason.MetadataFailure }); Rejected?.Invoke(error.Message); }
        finally { diagnostics.Write(DiagnosticEvent.OleSessionEnded, new() { Reason = DiagnosticReason.DropFinished }); Reset(); Left?.Invoke(); }
    }
    public void Reset() { payload = null; overLogged = false; lastSelection = null; lastCopy = false; }
    public void Dispose()
    {
        target.PreviewDragEnter -= Enter; target.PreviewDragOver -= Over;
        target.PreviewDragLeave -= Leave; target.PreviewDrop -= Drop;
        target.AllowDrop = false; Reset();
        diagnostics.Write(DiagnosticEvent.OleAllowDropDisabled);
    }
}
