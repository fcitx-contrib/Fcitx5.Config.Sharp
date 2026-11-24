using System;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;

namespace Fcitx5.Config.Core.Transport.Dbus;

/// <summary>
/// Owns the D-Bus session bus connection and tracks whether fcitx5 is currently registered.
/// </summary>
/// <remarks>
/// <para>
/// fcitx5 registers the well-known name <c>org.fcitx.Fcitx5</c> on the session bus. Availability is
/// tracked with <see cref="IConnection.ResolveServiceOwnerAsync(string, Action{ServiceOwnerChangedEventArgs}, Action{Exception})"/>,
/// which invokes the handler immediately with the current owner and again on every change.
/// </para>
/// <para>
/// The connection is not re-established automatically after the bus disconnects; create a new
/// instance in that case.
/// </para>
/// </remarks>
public sealed class FcitxConnection : IDisposable, IAsyncDisposable
{
    /// <summary>The well-known bus name of the fcitx5 daemon.</summary>
    public const string ServiceName = "org.fcitx.Fcitx5";

    private readonly object _gate = new();
    private Connection? _connection;
    private Task? _connectTask;
    private IDisposable? _ownerWatcher;
    private bool _isAvailable;
    private bool _disposed;

    /// <summary>Raised when fcitx5 becomes available or unavailable.</summary>
    public event EventHandler<bool>? AvailabilityChanged;

    /// <summary>The underlying connection, or <see langword="null"/> before <see cref="ConnectAsync"/>.</summary>
    public IConnection? Connection
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
    /// Connects to the session bus and starts watching for fcitx5. Calling this more than once
    /// returns the same operation; after a failure the next call retries.
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
        var connection = new Connection(Address.Session);

        lock (_gate)
        {
            _connection = connection;
        }

        connection.StateChanged += OnStateChanged;

        try
        {
            await connection.ConnectAsync().ConfigureAwait(false);

            var watcher = await connection
                .ResolveServiceOwnerAsync(ServiceName, OnServiceOwnerChanged, OnWatcherError)
                .ConfigureAwait(false);

            bool disposeNow;
            lock (_gate)
            {
                disposeNow = _disposed;
                if (!disposeNow)
                {
                    _ownerWatcher = watcher;
                }
            }

            if (disposeNow)
            {
                watcher.Dispose();
                connection.StateChanged -= OnStateChanged;
                connection.Dispose();
            }
        }
        catch
        {
            lock (_gate)
            {
                if (ReferenceEquals(_connection, connection))
                {
                    _connection = null;
                }

                _connectTask = null;
                _isAvailable = false;
            }

            connection.StateChanged -= OnStateChanged;
            connection.Dispose();
            throw;
        }
    }

    private void OnServiceOwnerChanged(ServiceOwnerChangedEventArgs args) =>
        SetAvailability(!string.IsNullOrEmpty(args.NewOwner));

    private void OnWatcherError(Exception exception) => SetAvailability(false);

    private void OnStateChanged(object? sender, ConnectionStateChangedEventArgs args)
    {
        if (args.State == ConnectionState.Disconnected)
        {
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
        Connection? connection;
        IDisposable? watcher;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isAvailable = false;
            connection = _connection;
            watcher = _ownerWatcher;
            _connection = null;
            _ownerWatcher = null;
            _connectTask = null;
        }

        watcher?.Dispose();

        if (connection is not null)
        {
            connection.StateChanged -= OnStateChanged;
            connection.Dispose();
        }
    }

    /// <summary>
    /// Disposes the connection. The underlying Tmds.DBus connection only supports synchronous
    /// disposal, so nothing is awaited.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}
