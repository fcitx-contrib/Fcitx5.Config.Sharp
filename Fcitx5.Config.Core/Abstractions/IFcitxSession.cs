using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fcitx5.Config.Core.Model;

namespace Fcitx5.Config.Core.Abstractions;

/// <summary>
/// A transport-neutral connection to a running fcitx5 instance.
/// </summary>
/// <remarks>
/// <para>
/// Implementations exist for D-Bus (Linux) and for the Beast HTTP bridge (macOS, Windows).
/// Call <see cref="ConnectAsync"/> once before using any other member.
/// </para>
/// <para>
/// <see cref="Capabilities"/> tells which members are usable. Calling a member that the transport
/// does not support throws <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// Disposal is synchronous internally; <see cref="IAsyncDisposable"/> is offered so that consumers
/// can use <c>await using</c> uniformly alongside other asynchronous resources.
/// </para>
/// </remarks>
public interface IFcitxSession : IDisposable, IAsyncDisposable
{
    /// <summary>The features supported by this transport.</summary>
    FcitxCapabilities Capabilities { get; }

    /// <summary>Whether fcitx5 is currently reachable.</summary>
    bool IsAvailable { get; }

    /// <summary>Raised when fcitx5 becomes available or unavailable.</summary>
    event EventHandler<bool>? AvailabilityChanged;

    /// <summary>Connects the transport and starts watching availability.</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- configuration

    /// <summary>Reads the configuration and its description for a fcitx5 URI.</summary>
    /// <param name="uri">
    /// For example <c>fcitx://config/global</c>, <c>fcitx://config/addon/&lt;name&gt;</c> or
    /// <c>fcitx://config/inputmethod/&lt;uniqueName&gt;</c>.
    /// </param>
    /// <param name="cancellationToken">Cancels the call before it is dispatched.</param>
    Task<ConfigSnapshot> GetConfigAsync(string uri, CancellationToken cancellationToken = default);

    /// <summary>Writes a configuration value for a fcitx5 URI.</summary>
    Task SetConfigAsync(string uri, RawConfigValue value, CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- addons

    /// <summary>Enumerates the addons known to fcitx5.</summary>
    Task<IReadOnlyList<AddonInfo>> GetAddonsAsync(CancellationToken cancellationToken = default);

    /// <summary>Enables or disables addons; keys are addon unique names.</summary>
    Task SetAddonsEnabledAsync(
        IReadOnlyDictionary<string, bool> states,
        CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- input methods

    /// <summary>Enumerates the available input methods.</summary>
    Task<IReadOnlyList<InputMethodEntry>> GetInputMethodsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the input method group names.</summary>
    Task<IReadOnlyList<string>> GetInputMethodGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the name of the currently active input method group.</summary>
    Task<string> GetCurrentInputMethodGroupAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads a full input method group; a null or empty name means the current group.</summary>
    Task<InputMethodGroup> GetInputMethodGroupAsync(
        string? groupName = null,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces the item list and default layout of an existing group.</summary>
    Task SetInputMethodGroupAsync(InputMethodGroup group, CancellationToken cancellationToken = default);

    /// <summary>Adds an empty input method group.</summary>
    Task AddInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default);

    /// <summary>Removes an input method group.</summary>
    Task RemoveInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default);

    /// <summary>Switches the active input method group.</summary>
    Task SwitchInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default);

    /// <summary>Lists the keyboard layouts usable as a group's default layout.</summary>
    Task<IReadOnlyList<KeyboardLayoutInfo>> GetKeyboardLayoutsAsync(
        CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- runtime

    /// <summary>Reads the daemon state.</summary>
    Task<FcitxState> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the unique name of the current input method.</summary>
    Task<string> GetCurrentInputMethodAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets the current input method by unique name.</summary>
    Task SetCurrentInputMethodAsync(string uniqueName, CancellationToken cancellationToken = default);

    /// <summary>Activates the input method.</summary>
    Task ActivateAsync(CancellationToken cancellationToken = default);

    /// <summary>Deactivates the input method.</summary>
    Task DeactivateAsync(CancellationToken cancellationToken = default);

    /// <summary>Toggles the input method.</summary>
    Task ToggleAsync(CancellationToken cancellationToken = default);

    // ---------------------------------------------------------------- lifecycle

    /// <summary>Reloads the global configuration.</summary>
    Task ReloadConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>Reloads the configuration of one addon.</summary>
    Task ReloadAddonConfigAsync(string addonName, CancellationToken cancellationToken = default);

    /// <summary>Whether the daemon supports restarting itself.</summary>
    Task<bool> CanRestartAsync(CancellationToken cancellationToken = default);

    /// <summary>Asks the daemon to restart.</summary>
    Task RestartAsync(CancellationToken cancellationToken = default);

    /// <summary>Asks the daemon to exit.</summary>
    Task ExitAsync(CancellationToken cancellationToken = default);

    /// <summary>Asks the daemon to rescan its addons and input methods.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
