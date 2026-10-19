using Naqla.Core;

namespace Naqla.Conversion;

/// <summary>Reusable async queue. One heavyweight native job at a time across submissions;
/// successful published outputs survive unrelated failures and cancellation.</summary>
public sealed class BatchProcessor(IConversionService converter)
{
    public const int MaximumConcurrency = 1;
    public const int MaximumPendingSubmissions = 16;
    private int pending;
    private readonly SemaphoreSlim queue = new(MaximumConcurrency);
    private void Reserve()
    {
        if (Interlocked.Increment(ref pending) <= MaximumPendingSubmissions) return;
        Interlocked.Decrement(ref pending); throw new InvalidOperationException("The local processing queue is full. Wait for an existing task or cancel it.");
    }
    public async Task<T> RunExclusiveAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken token = default)
    {
        Reserve(); var entered = false;
        try { await queue.WaitAsync(token).ConfigureAwait(false); entered = true; return await operation(token).ConfigureAwait(false); }
        finally { if (entered) queue.Release(); Interlocked.Decrement(ref pending); }
    }

    public Task<BatchResult> RunAsync(ConversionRequest request, IProgress<BatchProgress>? progress = null, CancellationToken token = default)
    {
        if (request.Files.Count is < 1 or > FileDragPayload.MaximumFiles) throw new ArgumentException("Choose between 1 and 200 local files.");
        request = request with { Files = Array.AsReadOnly(request.Files.ToArray()) };
        if (request.Target == "MERGE" ? request.Files.Count < 2 || request.Files.Any(x => x.Category != FileCategory.Image || !converter.CanExecute(x, "MERGE"))
            : request.Files.Any(x => !converter.CanExecute(x, request.Target)))
            throw new NotSupportedException("The operation must be supported for every selected file.");
        // Metadata validation, native calls and publication must never run in the wheel's dispatcher.
        Reserve();
        return Task.Run(async () =>
        {
            var aggregate = request.Target is "MERGE" or "RENAME" or "ARCHIVE" || PdfToolIds.TryParse(request.Target, out _);
            var total = aggregate ? 1 : request.Files.Count;
            var results = new List<BatchItemResult>(total);
            var entered = false;
            try
            {
                await queue.WaitAsync(token).ConfigureAwait(false); entered = true;
                progress?.Report(new(total, 0, 0, 0, 0));
                for (var index = 0; index < total; index++)
                {
                    IReadOnlyList<DraggedFile> sources = aggregate ? request.Files : new[] { request.Files[index] };
                    if (token.IsCancellationRequested) { results.Add(new(index, sources, JobOutcome.Cancelled)); continue; }
                    progress?.Report(Snapshot(total, results, index + 1));
                    BatchItemResult item;
                    try
                    {
                        var output = await converter.ConvertAsync(request with { Files = sources }, token).ConfigureAwait(false);
                        item = new(index, sources, JobOutcome.Succeeded, output);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { item = new(index, sources, JobOutcome.Cancelled); }
                    catch (Exception error) { item = new(index, sources, JobOutcome.Failed, Failure: JobFailure.FromException(error)); }
                    results.Add(item);
                    progress?.Report(Snapshot(total, results, 0) with { LastFinished = item });
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                for (var index = 0; index < total; index++)
                    results.Add(new(index, aggregate ? request.Files : new[] { request.Files[index] }, JobOutcome.Cancelled));
            }
            finally { if (entered) queue.Release(); Interlocked.Decrement(ref pending); }
            return new BatchResult(results.AsReadOnly());
        });
    }
    private static BatchProgress Snapshot(int total, List<BatchItemResult> results, int current) => new(total,
        results.Count(x => x.Outcome != JobOutcome.Cancelled), results.Count(x => x.Outcome == JobOutcome.Succeeded),
        results.Count(x => x.Outcome == JobOutcome.Failed), current);
}
