using System;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Fcitx5.Config.Core.Transport.Dbus;

/// <summary>
/// Owns the D-Bus connection to the session bus and tracks whether fcitx5 is registered.
/// </summary>
/// <remarks>
/// <para>
/// fcitx5 registers the well-known name <c>org.fcitx.Fcitx5</c>. Availability is tracked with
/// <see cref="DBusConnection.WatchNameOwnerAsync"/>, which reports the current owner immediately
/// and yields a token that fires whenever that owner goes away.
/// </para>
/// <para>
/// The connection is not re-established after the bus disconnects; create a new instance in that
/// case. Calling <see cref="ConnectAsync"/> again after a failure retries.
/// </para>
/// </remarks>
internal sealed class FcitxConnection : IDisposable, IAsyncDisposable
{
    /// <summary>The well-known bus name of the fcitx5 daemon.</summary>
    public const string ServiceName = "org.fcitx.Fcitx5";

    private readonly string? _busAddress;
    private readonly object _gate = new();

    private DBusConnection? _connection;
    private NameOwnerWatcher? _watcher;
    private CancellationTokenSource? _watchCancellation;
    private Task? _connectTask;
    private bool _isAvailable;
    private bool _disposed;

    /// <param name="busAddress">
    /// The bus to connect to; <see langword="null"/> means the session bus. Tests use it to point
    /// at a private bus.
    /// </param>
    public FcitxConnection(string? busAddress = null)
    {
        _busAddress = busAddress;
    }

    /// <summary>Raised when fcitx5 becomes available or unavailable.</summary>
    public event EventHandler<bool>? AvailabilityChanged;

    /// <summary>The underlying connection, or <see langword="null"/> before <see cref="ConnectAsync"/>.</summary>
    public DBusConnection? Connection
    {
        get
        {
            lock (_gate)
            {
                return _connection;
            }
        }
    }

    /// <summary>Whether fcitx5 is currently registered on the bus.</summary>
    public bool IsAvailable
    {
        get
        {
            lock (_gate)
            {
                return _isAvailable;
            }
        }
    }

    /// <summary>
    /// Connects to the bus and starts watching for fcitx5. Calling this more than once returns the
    /// same operation; after a failure the next call retries.
    /// </summary>
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            ThrowIfDisposed();
            _connectTask ??= ConnectCoreAsync();
            return _connectTask;
        }
    }

    private async Task ConnectCoreAsync()
    {
        DBusConnection? connection = null;

        try
        {
            var address = _busAddress ?? DBusAddress.Session
                ?? throw new InvalidOperationException("The D-Bus session bus address is not set.");

            connection = new DBusConnection(address);

            lock (_gate)
            {
                _connection = connection;
            }

            await connection.ConnectAsync().ConfigureAwait(false);

            var watcher = await connection.WatchNameOwnerAsync(ServiceName).ConfigureAwait(false);
            var cancellation = new CancellationTokenSource();

            bool disposeNow;
            lock (_gate)
            {
                disposeNow = _disposed;
                if (!disposeNow)
                {
                    _watcher = watcher;
                    _watchCancellation = cancellation;
                }
            }

            if (disposeNow)
            {
                watcher.Dispose();
                connection.Dispose();
                return;
            }

            SetAvailability(watcher.GetCurrentOwner() is not null);
            _ = WatchAvailabilityAsync(watcher, cancellation.Token);
        }
        catch
        {
            lock (_gate)
            {
                if (connection is not null && ReferenceEquals(_connection, connection))
                {
                    _connection = null;
                }

                // Reset so that the next ConnectAsync retries instead of replaying the failure.
                _connectTask = null;
                _isAvailable = false;
            }

            connection?.Dispose();
            throw;
        }
    }

    private async Task WatchAvailabilityAsync(NameOwnerWatcher watcher, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string owner;
                try
                {
                    owner = await watcher.WaitForOwnerAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                SetAvailability(true);

                // The owner token fires when the daemon exits, restarts or drops the name.
                using var ownerChanged = CancellationTokenSource.CreateLinkedTokenSource(
                    watcher.GetOwnerChangedCancellationToken(owner),
                    cancellationToken);
                try
                {
                    await Task.Delay(Timeout.Infinite, ownerChanged.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Either the owner changed or the loop was asked to stop; the loop condition
                    // decides which, and SetAvailability ignores the update after disposal.
                }

                SetAvailability(false);
            }
        }
        catch (Exception)
        {
            // A closed bus or a disposed watcher ends tracking. The connection is not re-established;
            // create a new instance, as the type remarks say.
            SetAvailability(false);
        }
    }

    private void SetAvailability(bool available)
    {
        bool changed;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            changed = _isAvailable != available;
            _isAvailable = available;
        }

        if (changed)
        {
            AvailabilityChanged?.Invoke(this, available);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FcitxConnection));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        DBusConnection? connection;
        NameOwnerWatcher? watcher;
        CancellationTokenSource? watchCancellation;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isAvailable = false;
            connection = _connection;
            watcher = _watcher;
            watchCancellation = _watchCancellation;
            _connection = null;
            _watcher = null;
            _watchCancellation = null;
            _connectTask = null;
        }

        // The source is cancelled but not disposed: the availability loop may still be awaiting
        // its token, and disposing it underneath would surface as ObjectDisposedException.
        watchCancellation?.Cancel();
        watcher?.Dispose();
        connection?.Dispose();
    }

    /// <summary>
    /// Disposes the connection. Both Tmds.DBus connections and watchers only support synchronous
    /// disposal, so nothing is awaited.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}
