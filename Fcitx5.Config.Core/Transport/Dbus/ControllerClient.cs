using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fcitx5.Config.Core.Model;
using Fcitx5.Config.Core.Transport.Dbus.Generated;
using Tmds.DBus.Protocol;

namespace Fcitx5.Config.Core.Transport.Dbus;

/// <summary>
/// The typed, cancellation aware face of <c>org.fcitx.Fcitx.Controller1</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Controller1"/> (generated from <c>org.fcitx.Fcitx.Controller1.xml</c>) speaks D-Bus
/// tuples; this class maps them to the model, so the rest of the library never sees a wire type.
/// Keeping the mapping in one place also keeps two policies in one place: the token is checked
/// before a call is dispatched, and a call that is already on the wire is only stopped from being
/// awaited.
/// </para>
/// <para>
/// The generated proxy has no cancellation support of its own, and a dispatched call cannot be
/// recalled. Checking the token first matters for writes: a cancelled <c>SetConfig</c> must not
/// reach the daemon, or the user's configuration is overwritten after they cancelled.
/// </para>
/// </remarks>
internal sealed class ControllerClient
{
    private const string UnknownMethodErrorName = "org.freedesktop.DBus.Error.UnknownMethod";

    private const string ObjectPath = "/controller";

    private readonly Controller1 _proxy;

    public ControllerClient(DBusConnection connection, string destination)
    {
        _proxy = new Controller1(
            connection ?? throw new ArgumentNullException(nameof(connection)),
            destination ?? throw new ArgumentNullException(nameof(destination)),
            ObjectPath);
    }

    // ---------------------------------------------------------------- configuration

    public async Task<(RawConfigValue Value, ConfigDescription Description)> GetConfigAsync(
        string uri,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (value, description) = await _proxy
            .GetConfigAsync(uri)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        return (ConfigValueCodec.Read(value), ConfigValueCodec.ReadDescription(description));
    }

    public Task SetConfigAsync(string uri, RawConfigValue value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _proxy
            .SetConfigAsync(uri, ConfigValueCodec.Write(value))
            .WithCancellation(cancellationToken);
    }

    // ---------------------------------------------------------------- addons

    /// <summary>Reads the addon list, including the fields only <c>GetAddonsV2</c> reports.</summary>
    public async Task<AddonInfo[]> GetAddonsV2Async(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = await _proxy
            .GetAddonsV2Async()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        var result = new AddonInfo[raw.Length];
        for (var i = 0; i < raw.Length; i++)
        {
            var (uniqueName, name, comment, category, configurable, enabled, onDemand, dependencies, optional) = raw[i];

            result[i] = new AddonInfo(
                uniqueName,
                name,
                comment,
                category,
                configurable,
                enabled,
                onDemand,
                dependencies,
                optional);
        }

        return result;
    }

    /// <summary>Reads addons from daemons that predate <c>GetAddonsV2</c>.</summary>
    public async Task<AddonInfo[]> GetAddonsV1Async(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = await _proxy
            .GetAddonsAsync()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        var result = new AddonInfo[raw.Length];
        for (var i = 0; i < raw.Length; i++)
        {
            var (uniqueName, name, comment, category, configurable, enabled) = raw[i];
            result[i] = new AddonInfo(uniqueName, name, comment, category, configurable, enabled);
        }

        return result;
    }

    public Task SetAddonsEnabledAsync(
        IReadOnlyDictionary<string, bool> states,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var raw = new (string, bool)[states.Count];
        var index = 0;
        foreach (var pair in states)
        {
            raw[index++] = (pair.Key, pair.Value);
        }

        return _proxy.SetAddonsStateAsync(raw).WithCancellation(cancellationToken);
    }

    // ---------------------------------------------------------------- input methods

    public async Task<InputMethodEntry[]> GetInputMethodsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = await _proxy
            .AvailableInputMethodsAsync()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        var result = new InputMethodEntry[raw.Length];
        for (var i = 0; i < raw.Length; i++)
        {
            var (uniqueName, name, nativeName, icon, label, languageCode, configurable) = raw[i];
            result[i] = new InputMethodEntry(
                uniqueName,
                name,
                nativeName,
                icon,
                label,
                languageCode,
                configurable);
        }

        return result;
    }

    public async Task<KeyboardLayoutInfo[]> GetKeyboardLayoutsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = await _proxy
            .AvailableKeyboardLayoutsAsync()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        var result = new KeyboardLayoutInfo[raw.Length];
        for (var i = 0; i < raw.Length; i++)
        {
            var (layout, description, languages, rawVariants) = raw[i];
            var variants = new KeyboardLayoutVariant[rawVariants.Length];
            for (var v = 0; v < rawVariants.Length; v++)
            {
                var (variant, variantDescription, variantLanguages) = rawVariants[v];
                variants[v] = new KeyboardLayoutVariant(variant, variantDescription, variantLanguages);
            }

            result[i] = new KeyboardLayoutInfo(layout, description, languages, variants);
        }

        return result;
    }

    public Task<string[]> GetInputMethodGroupsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _proxy.InputMethodGroupsAsync().WithCancellation(cancellationToken);
    }

    public Task<string> GetCurrentInputMethodGroupAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _proxy.CurrentInputMethodGroupAsync().WithCancellation(cancellationToken);
    }

    public async Task<InputMethodGroup> GetInputMethodGroupAsync(
        string groupName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var group = await _proxy
            .FullInputMethodGroupInfoAsync(groupName)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        var items = new FullInputMethodEntry[group.Items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            var (uniqueName, name, nativeName, icon, label, languageCode, addon, configurable, layout, properties) =
                group.Items[i];

            items[i] = new FullInputMethodEntry(
                uniqueName,
                name,
                nativeName,
                icon,
                label,
                languageCode,
                addon,
                configurable,
                layout,
                ConfigValueCodec.ReadMap(properties));
        }

        return new InputMethodGroup(
            group.Name,
            group.DefaultInputMethod,
            group.DefaultLayout,
            items,
            ConfigValueCodec.ReadMap(group.Properties));
    }

    public Task SetInputMethodGroupAsync(InputMethodGroup group, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = new (string, string)[group.Items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = (group.Items[i].UniqueName, group.Items[i].Layout);
        }

        return _proxy
            .SetInputMethodGroupInfoAsync(group.Name, group.DefaultLayout, items)
            .WithCancellation(cancellationToken);
    }

    public Task AddInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default) =>
        CallWithNameAsync(_proxy.AddInputMethodGroupAsync, groupName, cancellationToken);

    public Task RemoveInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default) =>
        CallWithNameAsync(_proxy.RemoveInputMethodGroupAsync, groupName, cancellationToken);

    public Task SwitchInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default) =>
        CallWithNameAsync(_proxy.SwitchInputMethodGroupAsync, groupName, cancellationToken);

    // ---------------------------------------------------------------- runtime

    public Task<int> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _proxy.StateAsync().WithCancellation(cancellationToken);
    }

    public Task<string> GetCurrentInputMethodAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _proxy.CurrentInputMethodAsync().WithCancellation(cancellationToken);
    }

    public Task SetCurrentInputMethodAsync(string uniqueName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _proxy.SetCurrentIMAsync(uniqueName).WithCancellation(cancellationToken);
    }

    public Task ActivateAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.ActivateAsync, cancellationToken);

    public Task DeactivateAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.DeactivateAsync, cancellationToken);

    public Task ToggleAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.ToggleAsync, cancellationToken);

    // ---------------------------------------------------------------- lifecycle

    public Task ReloadConfigAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.ReloadConfigAsync, cancellationToken);

    public Task ReloadAddonConfigAsync(string addonName, CancellationToken cancellationToken = default) =>
        CallWithNameAsync(_proxy.ReloadAddonConfigAsync, addonName, cancellationToken);

    public Task<bool> CanRestartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _proxy.CanRestartAsync().WithCancellation(cancellationToken);
    }

    public Task RestartAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.RestartAsync, cancellationToken);

    public Task ExitAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.ExitAsync, cancellationToken);

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.RefreshAsync, cancellationToken);

    // ---------------------------------------------------------------- internals

    /// <summary>Whether <paramref name="exception"/> reports a method the daemon does not have.</summary>
    public static bool IsUnknownMethod(DBusErrorReplyException exception) =>
        string.Equals(exception.ErrorName, UnknownMethodErrorName, StringComparison.Ordinal);

    /// <summary>
    /// Runs a call that takes no arguments. It is deferred so the token is checked before dispatch.
    /// </summary>
    private static Task CallAsync(Func<Task> call, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return call().WithCancellation(cancellationToken);
    }

    /// <summary>
    /// Runs a call whose only argument is a single string. It is deferred so the token is checked
    /// before dispatch.
    /// </summary>
    private static Task CallWithNameAsync(
        Func<string, Task> call,
        string name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return call(name).WithCancellation(cancellationToken);
    }
}
