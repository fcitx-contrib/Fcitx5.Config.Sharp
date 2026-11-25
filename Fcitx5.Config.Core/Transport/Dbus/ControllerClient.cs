using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fcitx5.Config.Core.Abstractions;
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
/// tuples; this class maps them to the model and is the only place a D-Bus failure becomes a
/// <see cref="FcitxException"/>.
/// </para>
/// <para>
/// Every call goes through <see cref="CallAsync{T}"/>, which checks the token before dispatch and
/// translates on the way out. The check has to come first because invoking the proxy is what sends
/// the message, which is why the helper takes a factory rather than a task.
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
        var (value, description) = await CallAsync(
                () => _proxy.GetConfigAsync(uri),
                cancellationToken)
            .ConfigureAwait(false);

        return (ConfigValueCodec.Read(value), ConfigValueCodec.ReadDescription(description));
    }

    public Task SetConfigAsync(string uri, RawConfigValue value, CancellationToken cancellationToken = default) =>
        CallAsync(() => _proxy.SetConfigAsync(uri, ConfigValueCodec.Write(value)), cancellationToken);

    // ---------------------------------------------------------------- addons

    /// <summary>
    /// Reads the addon list, falling back to the shorter reply of daemons predating
    /// <c>GetAddonsV2</c>.
    /// </summary>
    public async Task<AddonInfo[]> GetAddonsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAddonsV2Async(cancellationToken).ConfigureAwait(false);
        }
        catch (FcitxErrorException exception)
            when (string.Equals(exception.TransportErrorName, UnknownMethodErrorName, StringComparison.Ordinal))
        {
            // Only a missing method means "old daemon". Any other refusal is a real failure, and
            // retrying it against the older method would report something unrelated.
            return await GetAddonsV1Async(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<AddonInfo[]> GetAddonsV2Async(CancellationToken cancellationToken)
    {
        var raw = await CallAsync(_proxy.GetAddonsV2Async, cancellationToken).ConfigureAwait(false);

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

    private async Task<AddonInfo[]> GetAddonsV1Async(CancellationToken cancellationToken)
    {
        var raw = await CallAsync(_proxy.GetAddonsAsync, cancellationToken).ConfigureAwait(false);

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
        var raw = new (string, bool)[states.Count];
        var index = 0;
        foreach (var pair in states)
        {
            raw[index++] = (pair.Key, pair.Value);
        }

        return CallAsync(() => _proxy.SetAddonsStateAsync(raw), cancellationToken);
    }

    // ---------------------------------------------------------------- input methods

    public async Task<InputMethodEntry[]> GetInputMethodsAsync(CancellationToken cancellationToken = default)
    {
        var raw = await CallAsync(_proxy.AvailableInputMethodsAsync, cancellationToken).ConfigureAwait(false);

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
        var raw = await CallAsync(_proxy.AvailableKeyboardLayoutsAsync, cancellationToken).ConfigureAwait(false);

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

    public Task<string[]> GetInputMethodGroupsAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.InputMethodGroupsAsync, cancellationToken);

    public Task<string> GetCurrentInputMethodGroupAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.CurrentInputMethodGroupAsync, cancellationToken);

    public async Task<InputMethodGroup> GetInputMethodGroupAsync(
        string groupName,
        CancellationToken cancellationToken = default)
    {
        var group = await CallAsync(() => _proxy.FullInputMethodGroupInfoAsync(groupName), cancellationToken)
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
        var items = new (string, string)[group.Items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = (group.Items[i].UniqueName, group.Items[i].Layout);
        }

        return CallAsync(
            () => _proxy.SetInputMethodGroupInfoAsync(group.Name, group.DefaultLayout, items),
            cancellationToken);
    }

    public Task AddInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default) =>
        CallAsync(() => _proxy.AddInputMethodGroupAsync(groupName), cancellationToken);

    public Task RemoveInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default) =>
        CallAsync(() => _proxy.RemoveInputMethodGroupAsync(groupName), cancellationToken);

    public Task SwitchInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default) =>
        CallAsync(() => _proxy.SwitchInputMethodGroupAsync(groupName), cancellationToken);

    // ---------------------------------------------------------------- runtime

    public Task<int> GetStateAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.StateAsync, cancellationToken);

    public Task<string> GetCurrentInputMethodAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.CurrentInputMethodAsync, cancellationToken);

    public Task SetCurrentInputMethodAsync(string uniqueName, CancellationToken cancellationToken = default) =>
        CallAsync(() => _proxy.SetCurrentIMAsync(uniqueName), cancellationToken);

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
        CallAsync(() => _proxy.ReloadAddonConfigAsync(addonName), cancellationToken);

    public Task<bool> CanRestartAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.CanRestartAsync, cancellationToken);

    public Task RestartAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.RestartAsync, cancellationToken);

    public Task ExitAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.ExitAsync, cancellationToken);

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        CallAsync(_proxy.RefreshAsync, cancellationToken);

    // ---------------------------------------------------------------- internals

    /// <summary>Checks the token, dispatches, and translates a transport failure.</summary>
    private static async Task<T> CallAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await call().WithCancellation(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (DbusErrors.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    /// <inheritdoc cref="CallAsync{T}"/>
    private static async Task CallAsync(Func<Task> call, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await call().WithCancellation(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (DbusErrors.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }
}
