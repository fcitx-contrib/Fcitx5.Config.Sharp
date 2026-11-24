using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fcitx5.Config.Core.Abstractions;
using Fcitx5.Config.Core.Model;
using Fcitx5.Config.Core.Transport.Dbus;

namespace Fcitx5.Config.Core.Fcitx;

/// <summary>
/// The high level entry point for a .NET fcitx5 configuration manager.
/// </summary>
/// <remarks>
/// <para>
/// The controller is transport agnostic: it forwards to an <see cref="IFcitxSession"/>, whose only
/// implementation today is D-Bus. Members that the underlying transport cannot serve throw
/// <see cref="NotSupportedException"/>; check
/// <see cref="Capabilities"/> (or <see cref="Supports"/>) first when building a UI.
/// </para>
/// <para>
/// A typical startup sequence is:
/// <code>
/// using var controller = await FcitxController.ConnectDbusAsync();
/// var global = await controller.GetGlobalConfigAsync();
/// </code>
/// </para>
/// </remarks>
public sealed class FcitxController : IDisposable, IAsyncDisposable
{
    private readonly IFcitxSession _session;
    private bool _disposed;

    /// <summary>Creates a controller over an existing session.</summary>
    public FcitxController(IFcitxSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>The underlying transport session.</summary>
    public IFcitxSession Session => _session;

    /// <summary>The features the underlying transport provides.</summary>
    public FcitxCapabilities Capabilities => _session.Capabilities;

    /// <summary>Whether fcitx5 is currently reachable.</summary>
    public bool IsAvailable => _session.IsAvailable;

    /// <summary>Raised when fcitx5 becomes available or unavailable.</summary>
    public event EventHandler<bool>? AvailabilityChanged
    {
        add => _session.AvailabilityChanged += value;
        remove => _session.AvailabilityChanged -= value;
    }

    /// <summary>Connects the D-Bus transport and returns a ready controller.</summary>
    /// <remarks>
    /// The controller owns the created session: if connecting fails, the session is disposed before
    /// the exception propagates.
    /// </remarks>
    public static async Task<FcitxController> ConnectDbusAsync(CancellationToken cancellationToken = default)
    {
        var controller = new FcitxController(new DbusSession());
        try
        {
            await controller.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return controller;
        }
        catch
        {
            await controller.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Connects the underlying transport.</summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _session.ConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether the transport supports all of <paramref name="required"/>.</summary>
    public bool Supports(FcitxCapabilities required) => (_session.Capabilities & required) == required;

    /// <summary>Throws <see cref="NotSupportedException"/> unless the transport supports the feature.</summary>
    public void EnsureSupported(FcitxCapabilities required)
    {
        if (!Supports(required))
        {
            var missing = required & ~_session.Capabilities;
            throw new NotSupportedException(
                $"The {_session.GetType().Name} transport does not support: {missing}.");
        }
    }

    // ---------------------------------------------------------------- configuration

    /// <summary>Reads the global configuration.</summary>
    public Task<ConfigSnapshot> GetGlobalConfigAsync(CancellationToken cancellationToken = default) =>
        GetConfigAsync(FcitxConfigUri.GlobalUri, cancellationToken);

    /// <summary>Writes the global configuration.</summary>
    public Task SetGlobalConfigAsync(RawConfigValue value, CancellationToken cancellationToken = default) =>
        SetConfigAsync(FcitxConfigUri.GlobalUri, value, cancellationToken);

    /// <summary>Reads an addon's configuration.</summary>
    public Task<ConfigSnapshot> GetAddonConfigAsync(string addonName, CancellationToken cancellationToken = default) =>
        GetConfigAsync(FcitxConfigUri.Addon(addonName), cancellationToken);

    /// <summary>Writes an addon's configuration.</summary>
    public Task SetAddonConfigAsync(
        string addonName,
        RawConfigValue value,
        CancellationToken cancellationToken = default) =>
        SetConfigAsync(FcitxConfigUri.Addon(addonName), value, cancellationToken);

    /// <summary>Reads an input method's configuration.</summary>
    public Task<ConfigSnapshot> GetInputMethodConfigAsync(
        string uniqueName,
        CancellationToken cancellationToken = default) =>
        GetConfigAsync(FcitxConfigUri.InputMethod(uniqueName), cancellationToken);

    /// <summary>Writes an input method's configuration.</summary>
    public Task SetInputMethodConfigAsync(
        string uniqueName,
        RawConfigValue value,
        CancellationToken cancellationToken = default) =>
        SetConfigAsync(FcitxConfigUri.InputMethod(uniqueName), value, cancellationToken);

    /// <summary>Reads any fcitx5 configuration URI.</summary>
    public Task<ConfigSnapshot> GetConfigAsync(string uri, CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.ConfigRead);
        return _session.GetConfigAsync(uri, cancellationToken);
    }

    /// <summary>Writes any fcitx5 configuration URI.</summary>
    public Task SetConfigAsync(string uri, RawConfigValue value, CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.ConfigWrite);
        return _session.SetConfigAsync(uri, value, cancellationToken);
    }

    // ---------------------------------------------------------------- addons

    /// <summary>Enumerates every addon.</summary>
    public Task<IReadOnlyList<AddonInfo>> GetAddonsAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.AddonEnumeration);
        return _session.GetAddonsAsync(cancellationToken);
    }

    /// <summary>Enumerates the addons of one category.</summary>
    public async Task<IReadOnlyList<AddonInfo>> GetAddonsAsync(
        AddonCategory category,
        CancellationToken cancellationToken = default)
    {
        var addons = await GetAddonsAsync(cancellationToken).ConfigureAwait(false);
        return addons.Where(addon => addon.CategoryKind == category).ToArray();
    }

    /// <summary>Enables or disables one addon.</summary>
    public Task SetAddonEnabledAsync(
        string addonName,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.AddonControl);
        return _session.SetAddonsEnabledAsync(
            new Dictionary<string, bool> { [addonName] = enabled },
            cancellationToken);
    }

    /// <summary>Enables or disables several addons in one call.</summary>
    public Task SetAddonsEnabledAsync(
        IReadOnlyDictionary<string, bool> states,
        CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.AddonControl);
        return _session.SetAddonsEnabledAsync(states, cancellationToken);
    }

    // ---------------------------------------------------------------- input methods

    /// <summary>Enumerates the available input methods.</summary>
    public Task<IReadOnlyList<InputMethodEntry>> GetInputMethodsAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.InputMethodEnumeration);
        return _session.GetInputMethodsAsync(cancellationToken);
    }

    /// <summary>Lists the input method group names.</summary>
    public Task<IReadOnlyList<string>> GetInputMethodGroupsAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.InputMethodGroupManagement);
        return _session.GetInputMethodGroupsAsync(cancellationToken);
    }

    /// <summary>Reads the currently active input method group name.</summary>
    public Task<string> GetCurrentInputMethodGroupAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.InputMethodGroupManagement);
        return _session.GetCurrentInputMethodGroupAsync(cancellationToken);
    }

    /// <summary>Reads one input method group; a null or empty name means the current group.</summary>
    public Task<InputMethodGroup> GetInputMethodGroupAsync(
        string? groupName = null,
        CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.InputMethodGroupManagement);
        return _session.GetInputMethodGroupAsync(groupName, cancellationToken);
    }

    /// <summary>Replaces the items and default layout of an existing group.</summary>
    public Task SetInputMethodGroupAsync(InputMethodGroup group, CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.InputMethodGroupManagement);
        return _session.SetInputMethodGroupAsync(group, cancellationToken);
    }

    /// <summary>Adds an empty input method group.</summary>
    public Task AddInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.InputMethodGroupManagement);
        return _session.AddInputMethodGroupAsync(groupName, cancellationToken);
    }

    /// <summary>Removes an input method group.</summary>
    public Task RemoveInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.InputMethodGroupManagement);
        return _session.RemoveInputMethodGroupAsync(groupName, cancellationToken);
    }

    /// <summary>Switches the active input method group.</summary>
    public Task SwitchInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.InputMethodGroupManagement);
        return _session.SwitchInputMethodGroupAsync(groupName, cancellationToken);
    }

    /// <summary>Lists the keyboard layouts usable as a group's default layout.</summary>
    public Task<IReadOnlyList<KeyboardLayoutInfo>> GetKeyboardLayoutsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.InputMethodGroupManagement);
        return _session.GetKeyboardLayoutsAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- runtime

    /// <summary>Reads the daemon state.</summary>
    public Task<FcitxState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.RuntimeCommands);
        return _session.GetStateAsync(cancellationToken);
    }

    /// <summary>Reads the unique name of the current input method.</summary>
    public Task<string> GetCurrentInputMethodAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.RuntimeCommands);
        return _session.GetCurrentInputMethodAsync(cancellationToken);
    }

    /// <summary>Sets the current input method.</summary>
    public Task SetCurrentInputMethodAsync(string uniqueName, CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.RuntimeCommands);
        return _session.SetCurrentInputMethodAsync(uniqueName, cancellationToken);
    }

    /// <summary>Activates the input method.</summary>
    public Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.RuntimeCommands);
        return _session.ActivateAsync(cancellationToken);
    }

    /// <summary>Deactivates the input method.</summary>
    public Task DeactivateAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.RuntimeCommands);
        return _session.DeactivateAsync(cancellationToken);
    }

    /// <summary>Toggles the input method.</summary>
    public Task ToggleAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.RuntimeCommands);
        return _session.ToggleAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- lifecycle

    /// <summary>Reloads the global configuration.</summary>
    public Task ReloadConfigAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.Lifecycle);
        return _session.ReloadConfigAsync(cancellationToken);
    }

    /// <summary>Reloads one addon's configuration.</summary>
    public Task ReloadAddonConfigAsync(string addonName, CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.Lifecycle);
        return _session.ReloadAddonConfigAsync(addonName, cancellationToken);
    }

    /// <summary>Whether the daemon supports restarting itself.</summary>
    public Task<bool> CanRestartAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.Lifecycle);
        return _session.CanRestartAsync(cancellationToken);
    }

    /// <summary>Asks the daemon to restart.</summary>
    public Task RestartAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.Lifecycle);
        return _session.RestartAsync(cancellationToken);
    }

    /// <summary>Asks the daemon to exit.</summary>
    public Task ExitAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.Lifecycle);
        return _session.ExitAsync(cancellationToken);
    }

    /// <summary>Asks the daemon to rescan its addons and input methods.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported(FcitxCapabilities.Lifecycle);
        return _session.RefreshAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- internals

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FcitxController));
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
        _session.Dispose();
    }

    /// <summary>
    /// Disposes the controller and its session. Disposal of the transport is synchronous, so nothing
    /// is awaited.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}
