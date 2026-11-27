using System.Threading.Channels;
using ContactTogetherApi.Dtos;

namespace ContactTogetherApi.Realtime;

/// <summary>
/// In-process <see cref="IInboxNotifier"/>: each open stream gets a small channel, grouped by employee id.
/// </summary>
/// <remarks>
/// Like <c>MemoryTokenRevocationStore</c>, it only reaches streams held by this process. If the API is
/// scaled out to several instances, replace it with one that relays signals through a shared bus
/// (Redis pub/sub), or a signal raised on one instance will not reach streams held by another.
/// </remarks>
public sealed class InboxNotifier : IInboxNotifier
{
    /// <summary>
    /// Streams kept per employee (one per open tab). Opening one more closes the oldest, so tabs left
    /// open or connections that never closed cannot pile up.
    /// </summary>
    public const int MaxStreamsPerEmployee = 10;

    /// <summary>
    /// Signals buffered per stream before the oldest is dropped. They only mean "reload", so a slow
    /// reader loses nothing that matters.
    /// </summary>
    private const int BufferedSignals = 8;

    private readonly object _gate = new();
    private readonly Dictionary<string, List<Entry>> _streams = new(StringComparer.Ordinal);
    private readonly ILogger<InboxNotifier> _logger;

    public InboxNotifier(ILogger<InboxNotifier> logger)
    {
        _logger = logger;
    }

    public InboxSubscription Subscribe(string employeeId)
    {
        var entry = new Entry(Channel.CreateBounded<InboxSignal>(new BoundedChannelOptions(BufferedSignals)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        }));

        Entry? evicted = null;
        int count;
        lock (_gate)
        {
            if (!_streams.TryGetValue(employeeId, out var entries))
            {
                entries = [];
                _streams[employeeId] = entries;
            }

            if (entries.Count >= MaxStreamsPerEmployee)
            {
                evicted = entries[0];
                entries.RemoveAt(0);
            }

            entries.Add(entry);
            count = entries.Count;
        }

        // Outside the lock: cancelling runs the evicted stream's callbacks synchronously.
        if (evicted is not null)
        {
            evicted.Close();
            _logger.LogDebug("Oldest inbox stream of employee {EmployeeId} closed to make room.", employeeId);
        }

        _logger.LogDebug("Inbox stream opened for employee {EmployeeId} ({Count} open).", employeeId, count);
        return new InboxSubscription(entry.Channel.Reader, entry.Closed.Token, () => Unsubscribe(employeeId, entry));
    }

    public void Notify(IEnumerable<string?> employeeIds, InboxSignal signal)
    {
        List<Entry> targets = [];
        lock (_gate)
        {
            foreach (var employeeId in employeeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
            {
                if (_streams.TryGetValue(employeeId!, out var entries))
                {
                    targets.AddRange(entries);
                }
            }
        }

        // A bounded DropOldest channel accepts every write until it is completed.
        foreach (var target in targets)
        {
            target.Channel.Writer.TryWrite(signal);
        }

        if (targets.Count > 0)
        {
            _logger.LogDebug(
                "Inbox signal {Reason} for service {ServiceId} sent to {Count} stream(s).",
                signal.Reason,
                signal.ServiceId,
                targets.Count);
        }
    }

    private void Unsubscribe(string employeeId, Entry entry)
    {
        int count;
        lock (_gate)
        {
            if (!_streams.TryGetValue(employeeId, out var entries))
            {
                count = 0;
            }
            else
            {
                entries.Remove(entry);
                count = entries.Count;
                if (count == 0)
                {
                    _streams.Remove(employeeId);
                }
            }
        }

        entry.Close();
        _logger.LogDebug("Inbox stream closed for employee {EmployeeId} ({Count} open).", employeeId, count);
    }

    private sealed class Entry(Channel<InboxSignal> channel)
    {
        public Channel<InboxSignal> Channel { get; } = channel;

        // Never disposed: it has no timer or wait handle, so there is nothing to release.
        public CancellationTokenSource Closed { get; } = new();

        /// <summary>Ends the stream: completes its channel and fires <see cref="Closed"/>. Safe to call twice.</summary>
        public void Close()
        {
            Channel.Writer.TryComplete();
            Closed.Cancel();
        }
    }
}
