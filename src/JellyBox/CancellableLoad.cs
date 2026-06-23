namespace JellyBox;

/// <summary>
/// Coordinates a single in-flight asynchronous load so that starting a new load cancels any
/// prior one. This prevents a superseded load's post-await continuation from overwriting
/// view-model state after a newer load has begun (the stale-load race during rapid navigation
/// or refresh).
/// </summary>
// CA1001: The CancellationTokenSource assigned to _cts is owned and disposed by the RunAsync
// call that created it (via the local 'using'); this field only retains a transient reference so
// a superseded load can be cancelled, so this type itself does not need to be IDisposable.
#pragma warning disable CA1001
internal sealed class CancellableLoad
#pragma warning restore CA1001
{
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Cancels any in-flight load without starting a new one.
    /// </summary>
    public async Task CancelAsync()
    {
        CancellationTokenSource? previous = Interlocked.Exchange(ref _cts, null);
        if (previous is not null)
        {
            await previous.CancelAsync();
        }
    }

    /// <summary>
    /// Cancels any prior in-flight load, then runs <paramref name="operation"/> with a fresh
    /// cancellation token. If this load is itself superseded by a later call, the resulting
    /// cancellation is swallowed.
    /// </summary>
    /// <param name="operation">The load to run, observing the supplied cancellation token.</param>
    public async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using CancellationTokenSource cts = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _cts, cts);
        if (previous is not null)
        {
            await previous.CancelAsync();
        }

        CancellationToken token = cts.Token;
        try
        {
            await operation(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Superseded by a newer load; discard this result.
        }
        finally
        {
            Interlocked.CompareExchange(ref _cts, null, cts);
        }
    }
}
