using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Naqla.Core;

namespace Naqla.Windows;

public sealed class DiagnosticLog : IDiagnostics, IDisposable
{
    public const string Build = "0.8.0-development.1";
    private readonly Channel<Entry> queue = Channel.CreateBounded<Entry>(new BoundedChannelOptions(512)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly ConcurrentDictionary<DiagnosticEvent, long> counts = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Task writer;
    private readonly long maximumBytes;
    private long sequence, attempt, dropped;
    private volatile bool enabled = true, available, limited;
    private int disposed;
    private DiagnosticData? storageError;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
    private sealed record Entry(long Sequence, DateTimeOffset Utc, long ElapsedMs, long Attempt, DiagnosticEvent Event, DiagnosticData? Data);
    public string DirectoryPath { get; }
    public string? CurrentFilePath { get; private set; }
    public bool Available => available;
    public bool LimitReached => limited;
    public long DroppedRecords => Interlocked.Read(ref dropped);
    public long Count(DiagnosticEvent eventId) => counts.TryGetValue(eventId, out var value) ? value : 0;
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value) return;
            if (!value) Write(DiagnosticEvent.DiagnosticsDisabled);
            enabled = value;
            if (value) Write(DiagnosticEvent.DiagnosticsEnabled);
        }
    }

    public DiagnosticLog(string? directory = null, long maximumBytes = 2 * 1024 * 1024, int keepSessions = 8)
    {
        DirectoryPath = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Naqla", "Diagnostics");
        this.maximumBytes = maximumBytes;
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            foreach (var old in Directory.EnumerateFiles(DirectoryPath, "session-*.jsonl")
                .OrderByDescending(Path.GetFileName).Skip(Math.Max(0, keepSessions - 1)))
            {
                try { File.Delete(old); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            CurrentFilePath = Path.Combine(DirectoryPath, $"session-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.jsonl");
            var stream = new FileStream(CurrentFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete, 4096, useAsync: true);
            available = true;
            writer = Task.Run(() => WriteLoopAsync(stream));
            Write(DiagnosticEvent.SessionStarted);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            storageError = DiagnosticData.FromException(error);
            available = false;
            writer = Task.CompletedTask;
        }
    }
    public long BeginAttempt() => Interlocked.Increment(ref attempt);
    public void Write(DiagnosticEvent eventId, DiagnosticData? data = null)
    {
        if (!enabled || !available || limited || Volatile.Read(ref disposed) != 0) return;
        counts.AddOrUpdate(eventId, 1, (_, value) => value + 1);
        var entry = new Entry(0, DateTimeOffset.UtcNow, clock.ElapsedMilliseconds,
            Interlocked.Read(ref attempt), eventId, data);
        // Hook callers only enqueue; never wait for disk or serialize JSON here.
        if (!queue.Writer.TryWrite(entry)) Interlocked.Increment(ref dropped);
    }
    private async Task WriteLoopAsync(FileStream stream)
    {
        try
        {
            await using var output = new StreamWriter(stream, new UTF8Encoding(false));
            var header = JsonSerializer.Serialize(new
            {
                Schema = 1, Build, Utc = DateTimeOffset.UtcNow,
                LocalOffsetMinutes = (int)DateTimeOffset.Now.Offset.TotalMinutes,
                Os = Environment.OSVersion.Version.ToString(), Is64Bit = Environment.Is64BitProcess,
                Privacy = "No filenames, paths, contents, window titles, typed text, cursor coordinates, or exception messages."
            });
            await output.WriteLineAsync(header);
            await output.FlushAsync();
            long bytes = Encoding.UTF8.GetByteCount(header) + 1;
            await foreach (var entry in queue.Reader.ReadAllAsync())
            {
                if (limited) continue;
                var ordered = entry with { Sequence = ++sequence };
                var json = JsonSerializer.Serialize(ordered, JsonOptions);
                if (bytes + Encoding.UTF8.GetByteCount(json) + 1 > maximumBytes)
                {
                    limited = true;
                    json = JsonSerializer.Serialize(ordered with { Event = DiagnosticEvent.LogLimitReached, Data = null }, JsonOptions);
                }
                await output.WriteLineAsync(json);
                await output.FlushAsync();
                bytes += Encoding.UTF8.GetByteCount(json) + 1;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            storageError = DiagnosticData.FromException(error);
            available = false;
        }
    }
    public string StatusText() =>
        $"Naqla {Build}\nDiagnostics: {(Enabled ? "ON" : "OFF")} · storage: {(Available ? "available" : "unavailable")}\n" +
        $"Mouse hook installed: {Count(DiagnosticEvent.MouseHookInstalled) > 0}\nKeyboard hook installed: {Count(DiagnosticEvent.KeyboardHookInstalled) > 0}\n" +
        $"Shift presses: {Count(DiagnosticEvent.ShiftDown)}\nShell mouse-downs: {Count(DiagnosticEvent.ShellLeftDown)}\nDrag thresholds: {Count(DiagnosticEvent.DragThresholdPassed)}\n" +
        $"Gesture candidates: {Count(DiagnosticEvent.GestureCandidate)}\nOverlay shown: {Count(DiagnosticEvent.OverlayShown)}\nOLE DragEnter: {Count(DiagnosticEvent.OleDragEnter)}\n" +
        $"Confirmed files: {Count(DiagnosticEvent.OleFileConfirmed)}\nAccepted drops: {Count(DiagnosticEvent.OleDropAccepted)}\nConversions succeeded: {Count(DiagnosticEvent.ConversionSucceeded)}\n" +
        $"Tray visible requested: {Count(DiagnosticEvent.TrayVisibleRequested) > 0}\nDropped log records: {DroppedRecords} · size limit reached: {LimitReached}\n" +
        (storageError is null ? "" : $"Storage error: {storageError.Error} / {storageError.ErrorCode}\n") +
        "Counters report app events; actual tray visibility and Explorer drag need your desktop test.\nNo personal file paths are recorded.";

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        queue.Writer.TryComplete();
        try { writer.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
    }
}
