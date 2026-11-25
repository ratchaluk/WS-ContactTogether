using System.Threading.Channels;
using ContactTogetherApi.Dtos;

namespace ContactTogetherApi.Realtime;

/// <summary>
/// Pushes "your inbox may have changed" signals to the open <c>GET /Home/InboxStream</c> connections
/// of an employee. A signal is only a hint to reload <c>GET /Home/GetInboxServices</c>, so losing one
/// is harmless: the client reloads whenever its stream (re)connects.
/// </summary>
public interface IInboxNotifier
{
    /// <summary>Registers one stream of <paramref name="employeeId"/>. Dispose it when the stream ends.</summary>
    InboxSubscription Subscribe(string employeeId);

    /// <summary>
    /// Sends <paramref name="signal"/> to every open stream of the given employees. Null, blank and
    /// repeated ids are skipped. Call it only after the change is committed.
    /// </summary>
    void Notify(IEnumerable<string?> employeeIds, InboxSignal signal);
}

/// <summary>One open stream: the signals sent to it, and a token that fires when the notifier closes it.</summary>
public sealed class InboxSubscription : IDisposable
{
    private readonly Action _unsubscribe;
    private int _disposed;

    internal InboxSubscription(ChannelReader<InboxSignal> reader, CancellationToken closed, Action unsubscribe)
    {
        Reader = reader;
        Closed = closed;
        _unsubscribe = unsubscribe;
    }

    public ChannelReader<InboxSignal> Reader { get; }

    /// <summary>
    /// Cancelled when the stream is closed: pushed out because the employee opened more streams than
    /// allowed and this was the oldest, or disposed.
    /// </summary>
    public CancellationToken Closed { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _unsubscribe();
        }
    }
}
