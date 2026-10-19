using System.Diagnostics;
using System.IO;
using System.Text;

namespace Naqla.Conversion;

internal static class LocalProcess
{
    internal sealed record Result(string Output, string Error, int ExitCode, long Milliseconds, long? PeakBytes);
    public static async Task<string> RunAsync(string executable, IEnumerable<string> arguments, CancellationToken token, bool capture = false, string? workingDirectory = null) =>
        (await RunCapturedAsync(executable, arguments, token, capture ? 65536 : 0, 0, workingDirectory).ConfigureAwait(false)).Output;

    // Captured text stays private to a parser/editor. Never serialize it to diagnostics.
    public static async Task<Result> RunCapturedAsync(string executable, IEnumerable<string> arguments, CancellationToken token, int outputLimit = 1_048_576, int errorLimit = 0, string? workingDirectory = null, TimeSpan? timeout = null, IReadOnlyDictionary<string, string>? environment = null)
    {
        token.ThrowIfCancellationRequested();
        if (outputLimit is < 0 or > 16_777_216 || errorLimit is < 0 or > 16_777_216) throw new ArgumentOutOfRangeException(nameof(outputLimit));
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(45));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        start.StandardOutputEncoding = start.StandardErrorEncoding = new UTF8Encoding(false);
        if (workingDirectory is not null) start.WorkingDirectory = workingDirectory;
        if (environment is not null) foreach (var pair in environment) start.Environment[pair.Key] = pair.Value;
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        var watch = Stopwatch.StartNew();
        using var process = Process.Start(start) ?? throw new IOException("The local conversion engine could not start.");
        using var job = new ProcessJob(process);
        using var registration = linked.Token.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } });
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (System.ComponentModel.Win32Exception) { } catch (InvalidOperationException) { }
        // Engine error text can contain private source paths. Drain it, never log or display it.
        var error = ReadOutput(process.StandardError, errorLimit);
        var output = ReadOutput(process.StandardOutput, outputLimit);
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        job.Dispose(); // Kill any remaining descendants before draining inherited output handles.
        var errors = await error.ConfigureAwait(false);
        var text = await output.ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (deadline.IsCancellationRequested) throw new TimeoutException("The local engine exceeded its processing deadline; the original was preserved.");
        if (process.ExitCode != 0) throw new InvalidDataException($"The local codec rejected the file (code {process.ExitCode}). The original was preserved.");
        long? peak = null; try { peak = process.PeakWorkingSet64; } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
        return new(text, errors, process.ExitCode, watch.ElapsedMilliseconds, peak);
    }
    private static async Task<string> ReadOutput(StreamReader reader, int limit)
    {
        var builder = new StringBuilder(); var buffer = new char[4096];
        while (true)
        {
            var count = await reader.ReadAsync(buffer).ConfigureAwait(false); if (count == 0) break;
            if (builder.Length < limit) builder.Append(buffer, 0, Math.Min(count, limit - builder.Length));
        }
        return builder.ToString();
    }
}
