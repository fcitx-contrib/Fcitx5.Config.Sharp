using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fcitx5.Config.Core.Model;
using Tmds.DBus.Protocol;

namespace Fcitx5.Config.Core.Transport.Dbus;

/// <summary>
/// A hand written proxy for <c>org.fcitx.Fcitx.Controller1</c> at <c>/controller</c>.
/// </summary>
/// <remarks>
/// <para>
/// The proxy talks to <see cref="MessageWriter"/> and <see cref="Reader"/> directly instead of
/// relying on runtime proxy generation. That is what makes the library usable with Native AOT:
/// every type and every field access is statically known, and the trimmer has nothing to guess.
/// </para>
/// <para>
/// <see cref="MessageWriter"/> is a mutable <c>ref struct</c> that cannot live across an
/// <c>await</c>, so every call builds its message synchronously and only then dispatches it.
/// </para>
/// <para>
/// A cancellation token is checked <em>before</em> dispatch: a call the caller already cancelled
/// must not reach the daemon, otherwise a cancelled <c>SetConfig</c> would still overwrite the
/// user's configuration. Once dispatched a call cannot be recalled, so the token then only stops
/// waiting for the reply.
/// </para>
/// </remarks>
internal sealed class FcitxControllerProxy
{
    /// <summary>The object path of the controller interface.</summary>
    public const string ObjectPath = "/controller";

    /// <summary>The D-Bus interface name.</summary>
    public const string InterfaceName = "org.fcitx.Fcitx.Controller1";

    private const string UnknownMethodErrorName = "org.freedesktop.DBus.Error.UnknownMethod";

    private readonly DBusConnection _connection;
    private readonly string _destination;

    public FcitxControllerProxy(DBusConnection connection, string destination)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _destination = destination ?? throw new ArgumentNullException(nameof(destination));
    }

    // ---------------------------------------------------------------- configuration

    public Task<(RawConfigValue Value, ConfigDescription Description)> GetConfigAsync(
        string uri,
        CancellationToken cancellationToken = default) =>
        CallAsync(
            "GetConfig",
            "s",
            cancellationToken,
            static (message, _) => ReadConfig(message),
            (ref MessageWriter writer) => writer.WriteString(uri));

    public Task SetConfigAsync(string uri, RawConfigValue value, CancellationToken cancellationToken = default) =>
        CallAsync(
            "SetConfig",
            "sv",
            cancellationToken,
            (ref MessageWriter writer) =>
            {
                writer.WriteString(uri);
                DbusConfigCodec.WriteVariant(ref writer, value);
            });

    // ---------------------------------------------------------------- addons

    public Task<AddonInfo[]> GetAddonsV2Async(CancellationToken cancellationToken = default) =>
        CallAsync(
            "GetAddonsV2",
            null,
            cancellationToken,
            static (message, _) => ReadAddonsV2(message));

    /// <summary>Reads addons from daemons that predate <c>GetAddonsV2</c>.</summary>
    public Task<AddonInfo[]> GetAddonsAsync(CancellationToken cancellationToken = default) =>
        CallAsync(
            "GetAddons",
            null,
            cancellationToken,
            static (message, _) => ReadAddons(message));

    public Task SetAddonsEnabledAsync(
        IReadOnlyDictionary<string, bool> states,
        CancellationToken cancellationToken = default) =>
        CallAsync(
            "SetAddonsState",
            "a(sb)",
            cancellationToken,
            (ref MessageWriter writer) =>
            {
                var array = writer.WriteArrayStart(DBusType.Struct);
                foreach (var pair in states)
                {
                    writer.WriteStructureStart();
                    writer.WriteString(pair.Key);
                    writer.WriteBool(pair.Value);
                }

                writer.WriteArrayEnd(array);
            });

    // ---------------------------------------------------------------- input methods

    public Task<InputMethodEntry[]> GetInputMethodsAsync(CancellationToken cancellationToken = default) =>
        CallAsync(
            "AvailableInputMethods",
            null,
            cancellationToken,
            static (message, _) => ReadInputMethods(message));

    public Task<KeyboardLayoutInfo[]> GetKeyboardLayoutsAsync(CancellationToken cancellationToken = default) =>
        CallAsync(
            "AvailableKeyboardLayouts",
            null,
            cancellationToken,
            static (message, _) => ReadKeyboardLayouts(message));

    public Task<string[]> GetInputMethodGroupsAsync(CancellationToken cancellationToken = default) =>
        CallAsync(
            "InputMethodGroups",
            null,
            cancellationToken,
            static (message, _) => message.GetBodyReader().ReadArrayOfString());

    public Task<string> GetCurrentInputMethodGroupAsync(CancellationToken cancellationToken = default) =>
        CallAsync(
            "CurrentInputMethodGroup",
            null,
            cancellationToken,
            static (message, _) => message.GetBodyReader().ReadString());

    public Task<InputMethodGroup> GetInputMethodGroupAsync(
        string groupName,
        CancellationToken cancellationToken = default) =>
        CallAsync(
            "FullInputMethodGroupInfo",
            "s",
            cancellationToken,
            static (message, _) => ReadInputMethodGroup(message),
            (ref MessageWriter writer) => writer.WriteString(groupName));

    public Task SetInputMethodGroupAsync(InputMethodGroup group, CancellationToken cancellationToken = default) =>
        CallAsync(
            "SetInputMethodGroupInfo",
            "ssa(ss)",
            cancellationToken,
            (ref MessageWriter writer) =>
            {
                writer.WriteString(group.Name);
                writer.WriteString(group.DefaultLayout);

                var array = writer.WriteArrayStart(DBusType.Struct);
                foreach (var item in group.Items)
                {
                    writer.WriteStructureStart();
                    writer.WriteString(item.UniqueName);
                    writer.WriteString(item.Layout);
                }

                writer.WriteArrayEnd(array);
            });

    public Task AddInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default) =>
        CallWithNameAsync("AddInputMethodGroup", groupName, cancellationToken);

    public Task RemoveInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default) =>
        CallWithNameAsync("RemoveInputMethodGroup", groupName, cancellationToken);

    public Task SwitchInputMethodGroupAsync(string groupName, CancellationToken cancellationToken = default) =>
        CallWithNameAsync("SwitchInputMethodGroup", groupName, cancellationToken);

    // ---------------------------------------------------------------- runtime

    public Task<int> GetStateAsync(CancellationToken cancellationToken = default) =>
        CallAsync(
            "State",
            null,
            cancellationToken,
            static (message, _) => message.GetBodyReader().ReadInt32());

    public Task<string> GetCurrentInputMethodAsync(CancellationToken cancellationToken = default) =>
        CallAsync(
            "CurrentInputMethod",
            null,
            cancellationToken,
            static (message, _) => message.GetBodyReader().ReadString());

    public Task SetCurrentInputMethodAsync(string uniqueName, CancellationToken cancellationToken = default) =>
        CallWithNameAsync("SetCurrentIM", uniqueName, cancellationToken);

    public Task ActivateAsync(CancellationToken cancellationToken = default) =>
        CallAsync("Activate", null, cancellationToken);

    public Task DeactivateAsync(CancellationToken cancellationToken = default) =>
        CallAsync("Deactivate", null, cancellationToken);

    public Task ToggleAsync(CancellationToken cancellationToken = default) =>
        CallAsync("Toggle", null, cancellationToken);

    // ---------------------------------------------------------------- lifecycle

    public Task ReloadConfigAsync(CancellationToken cancellationToken = default) =>
        CallAsync("ReloadConfig", null, cancellationToken);

    public Task ReloadAddonConfigAsync(string addonName, CancellationToken cancellationToken = default) =>
        CallWithNameAsync("ReloadAddonConfig", addonName, cancellationToken);

    public Task<bool> CanRestartAsync(CancellationToken cancellationToken = default) =>
        CallAsync(
            "CanRestart",
            null,
            cancellationToken,
            static (message, _) => message.GetBodyReader().ReadBool());

    public Task RestartAsync(CancellationToken cancellationToken = default) =>
        CallAsync("Restart", null, cancellationToken);

    public Task ExitAsync(CancellationToken cancellationToken = default) =>
        CallAsync("Exit", null, cancellationToken);

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        CallAsync("Refresh", null, cancellationToken);

    // ---------------------------------------------------------------- internals

    /// <summary>Whether <paramref name="exception"/> reports a method the daemon does not have.</summary>
    public static bool IsUnknownMethod(DBusErrorReplyException exception) =>
        string.Equals(exception.ErrorName, UnknownMethodErrorName, StringComparison.Ordinal);

    /// <summary>Dispatches a call whose only argument is a single string.</summary>
    private Task CallWithNameAsync(string member, string name, CancellationToken cancellationToken) =>
        CallAsync(
            member,
            "s",
            cancellationToken,
            (ref MessageWriter writer) => writer.WriteString(name));

    /// <summary>Dispatches a call that has no reply value.</summary>
    private Task CallAsync(
        string member,
        string? signature,
        CancellationToken cancellationToken,
        ArgumentWriter? write = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var message = CreateMessage(member, signature, write);
        return _connection.CallMethodAsync(message).WithCancellation(cancellationToken);
    }

    /// <summary>Dispatches a call and reads its reply value.</summary>
    private Task<T> CallAsync<T>(
        string member,
        string? signature,
        CancellationToken cancellationToken,
        MessageValueReader<T> reader,
        ArgumentWriter? write = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var message = CreateMessage(member, signature, write);
        return _connection.CallMethodAsync(message, reader).WithCancellation(cancellationToken);
    }

    private MessageBuffer CreateMessage(string member, string? signature, ArgumentWriter? write)
    {
        var writer = _connection.GetMessageWriter();
        try
        {
            writer.WriteMethodCallHeader(_destination, ObjectPath, InterfaceName, member, signature);
            write?.Invoke(ref writer);
            return writer.CreateMessage();
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static (RawConfigValue Value, ConfigDescription Description) ReadConfig(Message message)
    {
        var reader = message.GetBodyReader();
        var value = DbusConfigCodec.ReadVariant(ref reader);
        var description = DbusConfigCodec.ReadDescription(ref reader);
        return (value, description);
    }

    private static AddonInfo[] ReadAddonsV2(Message message)
    {
        var reader = message.GetBodyReader();
        var end = reader.ReadArrayStart(DBusType.Struct);
        var result = new List<AddonInfo>();
        while (reader.HasNext(end))
        {
            reader.AlignStruct();
            var uniqueName = reader.ReadString();
            var name = reader.ReadString();
            var comment = reader.ReadString();
            var category = reader.ReadInt32();
            var configurable = reader.ReadBool();
            var enabled = reader.ReadBool();
            var onDemand = reader.ReadBool();
            var dependencies = reader.ReadArrayOfString();
            var optionalDependencies = reader.ReadArrayOfString();

            result.Add(new AddonInfo(
                uniqueName,
                name,
                comment,
                category,
                configurable,
                enabled,
                onDemand,
                dependencies,
                optionalDependencies));
        }

        return result.ToArray();
    }

    private static AddonInfo[] ReadAddons(Message message)
    {
        var reader = message.GetBodyReader();
        var end = reader.ReadArrayStart(DBusType.Struct);
        var result = new List<AddonInfo>();
        while (reader.HasNext(end))
        {
            reader.AlignStruct();
            var uniqueName = reader.ReadString();
            var name = reader.ReadString();
            var comment = reader.ReadString();
            var category = reader.ReadInt32();
            var configurable = reader.ReadBool();
            var enabled = reader.ReadBool();

            result.Add(new AddonInfo(uniqueName, name, comment, category, configurable, enabled));
        }

        return result.ToArray();
    }

    private static InputMethodEntry[] ReadInputMethods(Message message)
    {
        var reader = message.GetBodyReader();
        var end = reader.ReadArrayStart(DBusType.Struct);
        var result = new List<InputMethodEntry>();
        while (reader.HasNext(end))
        {
            reader.AlignStruct();
            var uniqueName = reader.ReadString();
            var name = reader.ReadString();
            var nativeName = reader.ReadString();
            var icon = reader.ReadString();
            var label = reader.ReadString();
            var languageCode = reader.ReadString();
            var configurable = reader.ReadBool();

            result.Add(new InputMethodEntry(
                uniqueName,
                name,
                nativeName,
                icon,
                label,
                languageCode,
                configurable));
        }

        return result.ToArray();
    }

    private static KeyboardLayoutInfo[] ReadKeyboardLayouts(Message message)
    {
        var reader = message.GetBodyReader();
        var end = reader.ReadArrayStart(DBusType.Struct);
        var result = new List<KeyboardLayoutInfo>();
        while (reader.HasNext(end))
        {
            reader.AlignStruct();
            var layout = reader.ReadString();
            var description = reader.ReadString();
            var languages = reader.ReadArrayOfString();

            var variants = new List<KeyboardLayoutVariant>();
            var variantsEnd = reader.ReadArrayStart(DBusType.Struct);
            while (reader.HasNext(variantsEnd))
            {
                reader.AlignStruct();
                var variant = reader.ReadString();
                var variantDescription = reader.ReadString();
                var variantLanguages = reader.ReadArrayOfString();
                variants.Add(new KeyboardLayoutVariant(variant, variantDescription, variantLanguages));
            }

            result.Add(new KeyboardLayoutInfo(layout, description, languages, variants.ToArray()));
        }

        return result.ToArray();
    }

    private static InputMethodGroup ReadInputMethodGroup(Message message)
    {
        var reader = message.GetBodyReader();
        var groupName = reader.ReadString();
        var defaultInputMethod = reader.ReadString();
        var defaultLayout = reader.ReadString();
        var properties = DbusConfigCodec.ReadMap(ref reader);

        var items = new List<FullInputMethodEntry>();
        var end = reader.ReadArrayStart(DBusType.Struct);
        while (reader.HasNext(end))
        {
            reader.AlignStruct();
            var uniqueName = reader.ReadString();
            var name = reader.ReadString();
            var nativeName = reader.ReadString();
            var icon = reader.ReadString();
            var label = reader.ReadString();
            var languageCode = reader.ReadString();
            var addon = reader.ReadString();
            var configurable = reader.ReadBool();
            var layout = reader.ReadString();
            var entryProperties = DbusConfigCodec.ReadMap(ref reader);

            items.Add(new FullInputMethodEntry(
                uniqueName,
                name,
                nativeName,
                icon,
                label,
                languageCode,
                addon,
                configurable,
                layout,
                entryProperties));
        }

        return new InputMethodGroup(groupName, defaultInputMethod, defaultLayout, items.ToArray(), properties);
    }

    /// <summary>Writes a call's arguments; <see cref="MessageWriter"/> is a ref struct.</summary>
    private delegate void ArgumentWriter(ref MessageWriter writer);
}
