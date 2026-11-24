using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fcitx5.Config.Core.Abstractions;
using Fcitx5.Config.Core.Model;
using Tmds.DBus;

namespace Fcitx5.Config.Core.Transport.Dbus;

/// <summary>
/// An <see cref="IFcitxSession"/> backed by the fcitx5 D-Bus controller interface.
/// </summary>
/// <remarks>
/// This is the full-featured transport: it implements every member of <see cref="IFcitxSession"/>,
/// so <see cref="Capabilities"/> is always <see cref="FcitxCapabilities.Full"/>.
/// </remarks>
public sealed class DbusSession : IFcitxSession
{
    /// <summary>The object path of the controller interface.</summary>
    public static readonly ObjectPath ControllerPath = new("/controller");

    private static readonly DbusConfigCodec ConfigCodec = DbusConfigCodec.Instance;

    private readonly FcitxConnection _connection;
    private IFcitxController? _controller;
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

        _controller = connection.CreateProxy<IFcitxController>(FcitxConnection.ServiceName, ControllerPath);
    }

    // ---------------------------------------------------------------- configuration

    /// <inheritdoc/>
    public async Task<ConfigSnapshot> GetConfigAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(uri))
        {
            throw new ArgumentException("A fcitx5 configuration URI is required.", nameof(uri));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var reply = await Controller.GetConfigAsync(uri).ConfigureAwait(false);
        return new ConfigSnapshot(
            uri,
            ConfigCodec.DecodeValue(reply.Value),
            ConfigCodec.DecodeDescription(reply.Description));
    }

    /// <inheritdoc/>
    public async Task SetConfigAsync(string uri, RawConfigValue value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(uri))
        {
            throw new ArgumentException("A fcitx5 configuration URI is required.", nameof(uri));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Controller.SetConfigAsync(uri, ConfigCodec.EncodeValue(value)).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- addons

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AddonInfo>> GetAddonsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DbusAddonInfoV2[] source;
        try
        {
            source = await Controller.GetAddonsV2Async().ConfigureAwait(false);
        }
        catch (DBusException exception) when (IsUnknownMethod(exception))
        {
            // Daemons predating GetAddonsV2 only expose the shorter struct.
            var legacy = await Controller.GetAddonsAsync().ConfigureAwait(false);
            var legacyResult = new AddonInfo[legacy.Length];
            for (var i = 0; i < legacy.Length; i++)
            {
                var addon = legacy[i];
                legacyResult[i] = new AddonInfo(
                    addon.UniqueName,
                    addon.Name,
                    addon.Comment,
                    addon.Category,
                    addon.Configurable,
                    addon.Enabled);
            }

            return legacyResult;
        }

        var result = new AddonInfo[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            var addon = source[i];
            result[i] = new AddonInfo(
                addon.UniqueName,
                addon.Name,
                addon.Comment,
                addon.Category,
                addon.Configurable,
                addon.Enabled,
                addon.OnDemand,
                addon.Dependencies,
                addon.OptionalDependencies);
        }

        return result;
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

        cancellationToken.ThrowIfCancellationRequested();

        var payload = new DbusAddonState[states.Count];
        var index = 0;
        foreach (var pair in states)
        {
            payload[index++] = new DbusAddonState { UniqueName = pair.Key, Enabled = pair.Value };
        }

        await Controller.SetAddonsStateAsync(payload).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- input methods

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InputMethodEntry>> GetInputMethodsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entries = await Controller.AvailableInputMethodsAsync().ConfigureAwait(false);
        var result = new InputMethodEntry[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            result[i] = new InputMethodEntry(
                entry.UniqueName,
                entry.Name,
                entry.NativeName,
                entry.Icon,
                entry.Label,
                entry.LanguageCode,
                entry.Configurable);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetInputMethodGroupsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var groups = await Controller.InputMethodGroupsAsync().ConfigureAwait(false);
        return groups;
    }

    /// <inheritdoc/>
    public async Task<string> GetCurrentInputMethodGroupAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Controller.CurrentInputMethodGroupAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<InputMethodGroup> GetInputMethodGroupAsync(
        string? groupName = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var info = await Controller
            .FullInputMethodGroupInfoAsync(groupName ?? string.Empty)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(info.GroupName))
        {
            throw new KeyNotFoundException(
                groupName is null
                    ? "fcitx5 did not report a current input method group."
                    : $"Input method group '{groupName}' does not exist.");
        }

        var entries = info.Entries ?? Array.Empty<DbusFullInputMethodEntry>();
        var items = new FullInputMethodEntry[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            items[i] = new FullInputMethodEntry(
                entry.UniqueName,
                entry.Name,
                entry.NativeName,
                entry.Icon,
                entry.Label,
                entry.LanguageCode,
                entry.Addon,
                entry.Configurable,
                entry.Layout,
                ConfigCodec.DecodeValue(entry.Properties));
        }

        return new InputMethodGroup(
            info.GroupName,
            info.DefaultInputMethod,
            info.DefaultLayout,
            items,
            ConfigCodec.DecodeValue(info.Properties));
    }

    /// <inheritdoc/>
    public async Task SetInputMethodGroupAsync(InputMethodGroup group, CancellationToken cancellationToken = default)
    {
        if (group is null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var entries = new DbusStringPair[group.Items.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            var item = group.Items[i];
            entries[i] = new DbusStringPair { Key = item.UniqueName, Value = item.Layout };
        }

        await Controller
            .SetInputMethodGroupInfoAsync(group.Name, group.DefaultLayout, entries)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException("A group name is required.", nameof(groupName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Controller.AddInputMethodGroupAsync(groupName).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException("A group name is required.", nameof(groupName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Controller.RemoveInputMethodGroupAsync(groupName).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SwitchInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException("A group name is required.", nameof(groupName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Controller.SwitchInputMethodGroupAsync(groupName).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KeyboardLayoutInfo>> GetKeyboardLayoutsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var source = await Controller.AvailableKeyboardLayoutsAsync().ConfigureAwait(false);
        var result = new KeyboardLayoutInfo[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            var layout = source[i];
            var sourceVariants = layout.Variants ?? Array.Empty<DbusLayoutVariant>();
            var variants = new KeyboardLayoutVariant[sourceVariants.Length];
            for (var j = 0; j < sourceVariants.Length; j++)
            {
                var variant = sourceVariants[j];
                variants[j] = new KeyboardLayoutVariant(variant.Variant, variant.Description, variant.Languages);
            }

            result[i] = new KeyboardLayoutInfo(layout.Layout, layout.Description, layout.Languages, variants);
        }

        return result;
    }

    // ---------------------------------------------------------------- runtime

    /// <inheritdoc/>
    public async Task<FcitxState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = await Controller.StateAsync().ConfigureAwait(false);
        return state is >= 0 and <= 2 ? (FcitxState)state : FcitxState.Closed;
    }

    /// <inheritdoc/>
    public async Task<string> GetCurrentInputMethodAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Controller.CurrentInputMethodAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetCurrentInputMethodAsync(string uniqueName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(uniqueName))
        {
            throw new ArgumentException("An input method unique name is required.", nameof(uniqueName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Controller.SetCurrentIMAsync(uniqueName).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Controller.ActivateAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeactivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Controller.DeactivateAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ToggleAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Controller.ToggleAsync().ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- lifecycle

    /// <inheritdoc/>
    public async Task ReloadConfigAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Controller.ReloadConfigAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ReloadAddonConfigAsync(string addonName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(addonName))
        {
            throw new ArgumentException("An addon name is required.", nameof(addonName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Controller.ReloadAddonConfigAsync(addonName).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> CanRestartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Controller.CanRestartAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Controller.RestartAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ExitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Controller.ExitAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Controller.RefreshAsync().ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- internals

    private const string UnknownMethodErrorName = "org.freedesktop.DBus.Error.UnknownMethod";

    private IFcitxController Controller =>
        _controller ?? throw new InvalidOperationException(
            $"{nameof(DbusSession)} is not connected; call {nameof(ConnectAsync)} first.");

    private static bool IsUnknownMethod(DBusException exception) =>
        string.Equals(exception.ErrorName, UnknownMethodErrorName, StringComparison.Ordinal);

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
        _controller = null;
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
