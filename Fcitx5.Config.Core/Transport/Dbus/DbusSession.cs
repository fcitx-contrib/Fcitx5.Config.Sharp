using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fcitx5.Config.Core.Abstractions;
using Fcitx5.Config.Core.Model;
using Tmds.DBus.Protocol;

namespace Fcitx5.Config.Core.Transport.Dbus;

/// <summary>
/// An <see cref="IFcitxSession"/> backed by the fcitx5 D-Bus controller interface.
/// </summary>
/// <remarks>
/// This is the full-featured transport: it implements every member of <see cref="IFcitxSession"/>,
/// so <see cref="Capabilities"/> is always <see cref="FcitxCapabilities.Full"/>.
/// </remarks>
internal sealed class DbusSession : IFcitxSession
{
    private readonly FcitxConnection _connection;
    private ControllerClient? _client;
    private bool _disposed;

    /// <summary>Creates a session that owns its own bus connection.</summary>
    public DbusSession()
        : this(new FcitxConnection())
    {
    }

    /// <summary>Creates a session over an existing connection.</summary>
    public DbusSession(FcitxConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    /// <inheritdoc/>
    public FcitxCapabilities Capabilities => FcitxCapabilities.Full;

    /// <inheritdoc/>
    public bool IsAvailable => _connection.IsAvailable;

    /// <inheritdoc/>
    public event EventHandler<bool>? AvailabilityChanged
    {
        add => _connection.AvailabilityChanged += value;
        remove => _connection.AvailabilityChanged -= value;
    }

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _connection.ConnectAsync(cancellationToken).ConfigureAwait(false);

        var connection = _connection.Connection
                         ?? throw new InvalidOperationException("The D-Bus connection is not available.");

        _client = new ControllerClient(connection, FcitxConnection.ServiceName);
    }

    // ---------------------------------------------------------------- configuration

    /// <inheritdoc/>
    public async Task<ConfigSnapshot> GetConfigAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(uri))
        {
            throw new ArgumentException("A fcitx5 configuration URI is required.", nameof(uri));
        }

        var (value, description) = await Client.GetConfigAsync(uri, cancellationToken).ConfigureAwait(false);
        return new ConfigSnapshot(uri, value, description);
    }

    /// <inheritdoc/>
    public async Task SetConfigAsync(
        string uri,
        RawConfigValue value,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(uri))
        {
            throw new ArgumentException("A fcitx5 configuration URI is required.", nameof(uri));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        await Client.SetConfigAsync(uri, value, cancellationToken).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- addons

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AddonInfo>> GetAddonsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await Client.GetAddonsV2Async(cancellationToken).ConfigureAwait(false);
        }
        catch (DBusErrorReplyException exception) when (ControllerClient.IsUnknownMethod(exception))
        {
            // Daemons predating GetAddonsV2 only report the shorter set of fields.
            return await Client.GetAddonsV1Async(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task SetAddonsEnabledAsync(
        IReadOnlyDictionary<string, bool> states,
        CancellationToken cancellationToken = default)
    {
        if (states is null)
        {
            throw new ArgumentNullException(nameof(states));
        }

        await Client.SetAddonsEnabledAsync(states, cancellationToken).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- input methods

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InputMethodEntry>> GetInputMethodsAsync(
        CancellationToken cancellationToken = default) =>
        await Client.GetInputMethodsAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetInputMethodGroupsAsync(
        CancellationToken cancellationToken = default) =>
        await Client.GetInputMethodGroupsAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<string> GetCurrentInputMethodGroupAsync(CancellationToken cancellationToken = default) =>
        await Client.GetCurrentInputMethodGroupAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<InputMethodGroup> GetInputMethodGroupAsync(
        string? groupName = null,
        CancellationToken cancellationToken = default)
    {
        var group = await Client
            .GetInputMethodGroupAsync(groupName ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(group.Name))
        {
            throw new KeyNotFoundException(
                groupName is null
                    ? "fcitx5 did not report a current input method group."
                    : $"Input method group '{groupName}' does not exist.");
        }

        return group;
    }

    /// <inheritdoc/>
    public async Task SetInputMethodGroupAsync(
        InputMethodGroup group,
        CancellationToken cancellationToken = default)
    {
        if (group is null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        await Client.SetInputMethodGroupAsync(group, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default)
    {
        RequireName(groupName, nameof(groupName), "A group name is required.");
        await Client.AddInputMethodGroupAsync(groupName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveInputMethodGroupAsync(
        string groupName,
        CancellationToken cancellationToken = default)
    {
        RequireName(groupName, nameof(groupName), "A group name is required.");
        await Client.RemoveInputMethodGroupAsync(groupName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SwitchInputMethodGroupAsync(
        string groupName,
        CancellationToken cancellationToken = default)
    {
        RequireName(groupName, nameof(groupName), "A group name is required.");
        await Client.SwitchInputMethodGroupAsync(groupName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KeyboardLayoutInfo>> GetKeyboardLayoutsAsync(
        CancellationToken cancellationToken = default) =>
        await Client.GetKeyboardLayoutsAsync(cancellationToken).ConfigureAwait(false);

    // ---------------------------------------------------------------- runtime

    /// <inheritdoc/>
    public async Task<FcitxState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var state = await Client.GetStateAsync(cancellationToken).ConfigureAwait(false);
        return state is >= 0 and <= 2 ? (FcitxState)state : FcitxState.Closed;
    }

    /// <inheritdoc/>
    public async Task<string> GetCurrentInputMethodAsync(CancellationToken cancellationToken = default) =>
        await Client.GetCurrentInputMethodAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task SetCurrentInputMethodAsync(
        string uniqueName,
        CancellationToken cancellationToken = default)
    {
        RequireName(uniqueName, nameof(uniqueName), "An input method unique name is required.");
        await Client.SetCurrentInputMethodAsync(uniqueName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task ActivateAsync(CancellationToken cancellationToken = default) =>
        Client.ActivateAsync(cancellationToken);

    /// <inheritdoc/>
    public Task DeactivateAsync(CancellationToken cancellationToken = default) =>
        Client.DeactivateAsync(cancellationToken);

    /// <inheritdoc/>
    public Task ToggleAsync(CancellationToken cancellationToken = default) =>
        Client.ToggleAsync(cancellationToken);

    // ---------------------------------------------------------------- lifecycle

    /// <inheritdoc/>
    public Task ReloadConfigAsync(CancellationToken cancellationToken = default) =>
        Client.ReloadConfigAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task ReloadAddonConfigAsync(string addonName, CancellationToken cancellationToken = default)
    {
        RequireName(addonName, nameof(addonName), "An addon name is required.");
        await Client.ReloadAddonConfigAsync(addonName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<bool> CanRestartAsync(CancellationToken cancellationToken = default) =>
        Client.CanRestartAsync(cancellationToken);

    /// <inheritdoc/>
    public Task RestartAsync(CancellationToken cancellationToken = default) => Client.RestartAsync(cancellationToken);

    /// <inheritdoc/>
    public Task ExitAsync(CancellationToken cancellationToken = default) => Client.ExitAsync(cancellationToken);

    /// <inheritdoc/>
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Client.RefreshAsync(cancellationToken);

    // ---------------------------------------------------------------- internals

    private ControllerClient Client =>
        _client ?? throw new InvalidOperationException(
            $"{nameof(DbusSession)} is not connected; call {nameof(ConnectAsync)} first.");

    private static void RequireName(string value, string parameterName, string message)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException(message, parameterName);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DbusSession));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _client = null;
        _connection.Dispose();
    }

    /// <summary>
    /// Disposes the session. Disposal of the bus connection is synchronous, so nothing is awaited.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}
