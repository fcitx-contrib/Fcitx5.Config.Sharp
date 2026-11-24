using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fcitx5.Config.Core.Model;
using Fcitx5.Config.Core.Transport.Dbus;
using Tmds.DBus.Protocol;

namespace Fcitx5.Config.Core.Tests;

/// <summary>A configuration group served by <see cref="FakeFcitx"/>.</summary>
public sealed class FakeGroup
{
    public FakeGroup(string name, params FakeOption[] options)
    {
        Name = name;
        Options = options;
    }

    public string Name { get; }

    public FakeOption[] Options { get; }
}

/// <summary>A configuration option served by <see cref="FakeFcitx"/>.</summary>
public sealed class FakeOption
{
    public FakeOption(
        string name,
        string type,
        string description = "",
        RawConfigValue? defaultValue = null,
        RawConfigValue? properties = null)
    {
        Name = name;
        Type = type;
        Description = description;
        DefaultValue = defaultValue;
        Properties = properties;
    }

    public string Name { get; }

    public string Type { get; }

    public string Description { get; }

    public RawConfigValue? DefaultValue { get; }

    public RawConfigValue? Properties { get; }
}

/// <summary>How <see cref="FakeFcitx"/> answers <c>GetAddonsV2</c>.</summary>
public enum AddonReplyMode
{
    /// <summary>Answer with the modern struct.</summary>
    V2,

    /// <summary>Report <c>UnknownMethod</c> so the caller falls back to <c>GetAddons</c>.</summary>
    UnknownMethod,

    /// <summary>Fail with another error, which must not trigger the fallback.</summary>
    Error,
}

/// <summary>
/// A minimal fake of <c>org.fcitx.Fcitx.Controller1</c> on a private bus.
/// </summary>
/// <remarks>
/// It frames its replies and decodes the payloads it receives with the same codec the library
/// uses, so the tests exercise the real wire format rather than a mock.
/// </remarks>
public sealed class FakeFcitx : IPathMethodHandler, IAsyncDisposable
{
    private const string InterfaceName = "org.fcitx.Fcitx.Controller1";
    private const string ServiceName = "org.fcitx.Fcitx5";

    private readonly DBusConnection _connection;

    private FakeFcitx(DBusConnection connection)
    {
        _connection = connection;
    }

    public string Path => "/controller";

    public bool HandlesChildPaths => false;

    public int State { get; set; } = 2;

    public AddonReplyMode AddonsMode { get; set; } = AddonReplyMode.V2;

    public RawConfigValue ConfigValue { get; set; } = new();

    public List<FakeGroup> ConfigGroups { get; } = new();

    public InputMethodGroup? Group { get; set; }

    public string? LastSetConfigUri { get; private set; }

    public RawConfigValue? LastSetConfigValue { get; private set; }

    public Dictionary<string, bool>? LastAddonStates { get; private set; }

    public InputMethodGroup? LastSavedGroup { get; private set; }

    /// <summary>Starts the fake and claims <c>org.fcitx.Fcitx5</c>.</summary>
    public static async Task<FakeFcitx> StartAsync(string busAddress)
    {
        var connection = new DBusConnection(busAddress);
        await connection.ConnectAsync().ConfigureAwait(false);

        var fake = new FakeFcitx(connection);
        connection.AddMethodHandler(fake);
        await connection.RequestNameAsync(ServiceName, RequestNameOptions.Default).ConfigureAwait(false);
        return fake;
    }

    /// <summary>Drops the well-known name, as a stopping daemon does.</summary>
    public Task ReleaseNameAsync() => _connection.ReleaseNameAsync(ServiceName);

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        var request = context.Request;
        if (request.InterfaceAsString != InterfaceName)
        {
            context.ReplyUnknownMethodError();
            return default;
        }

        switch (request.MemberAsString)
        {
            case "State":
                Reply(context, "i", (ref MessageWriter writer) => writer.WriteInt32(State));
                break;

            case "GetConfig":
                {
                    Reply(context, "va(sa(sssva{sv}))", (ref MessageWriter writer) =>
                    {
                        writer.WriteVariant(ConfigValueCodec.Write(ConfigValue));
                        var groups = writer.WriteArrayStart(DBusType.Struct);
                        foreach (var group in ConfigGroups)
                        {
                            writer.WriteStructureStart();
                            writer.WriteString(group.Name);
                            var options = writer.WriteArrayStart(DBusType.Struct);
                            foreach (var option in group.Options)
                            {
                                writer.WriteStructureStart();
                                writer.WriteString(option.Name);
                                writer.WriteString(option.Type);
                                writer.WriteString(option.Description);
                                writer.WriteVariant(ConfigValueCodec.Write(option.DefaultValue ?? new RawConfigValue()));
                                WriteMap(ref writer, option.Properties ?? new RawConfigValue());
                            }

                            writer.WriteArrayEnd(options);
                        }

                        writer.WriteArrayEnd(groups);
                    });
                    break;
                }

            case "SetConfig":
                {
                    var reader = request.GetBodyReader();
                    LastSetConfigUri = reader.ReadString();
                    LastSetConfigValue = ConfigValueCodec.Read(reader.ReadVariantValue());
                    ReplyEmpty(context);
                    break;
                }

            case "GetAddonsV2":
                if (AddonsMode == AddonReplyMode.UnknownMethod)
                {
                    context.ReplyUnknownMethodError();
                }
                else if (AddonsMode == AddonReplyMode.Error)
                {
                    context.ReplyError("org.fcitx.Test.Failure", "GetAddonsV2 failed on purpose.");
                }
                else
                {
                    Reply(context, "a(sssibbbasas)", WriteAddonsV2);
                }

                break;

            case "GetAddons":
                Reply(context, "a(sssibb)", WriteAddons);
                break;

            case "SetAddonsState":
                {
                    var reader = request.GetBodyReader();
                    var states = new Dictionary<string, bool>();
                    var end = reader.ReadArrayStart(DBusType.Struct);
                    while (reader.HasNext(end))
                    {
                        reader.AlignStruct();
                        states[reader.ReadString()] = reader.ReadBool();
                    }

                    LastAddonStates = states;
                    ReplyEmpty(context);
                    break;
                }

            case "AvailableInputMethods":
                Reply(context, "a(ssssssb)", static (ref MessageWriter writer) =>
                {
                    var array = writer.WriteArrayStart(DBusType.Struct);
                    writer.WriteStructureStart();
                    writer.WriteString("pinyin");
                    writer.WriteString("Pinyin");
                    writer.WriteString("拼音");
                    writer.WriteString("pinyin");
                    writer.WriteString("");
                    writer.WriteString("zh_CN");
                    writer.WriteBool(true);
                    writer.WriteStructureStart();
                    writer.WriteString("keyboard-us");
                    writer.WriteString("English (US)");
                    writer.WriteString("English (US)");
                    writer.WriteString("input-keyboard");
                    writer.WriteString("");
                    writer.WriteString("en");
                    writer.WriteBool(false);
                    writer.WriteArrayEnd(array);
                });
                break;

            case "AvailableKeyboardLayouts":
                Reply(context, "a(ssasa(ssas))", static (ref MessageWriter writer) =>
                {
                    var array = writer.WriteArrayStart(DBusType.Struct);
                    writer.WriteStructureStart();
                    writer.WriteString("us");
                    writer.WriteString("English (US)");
                    writer.WriteArray(new[] { "en" });
                    var variants = writer.WriteArrayStart(DBusType.Struct);
                    writer.WriteStructureStart();
                    writer.WriteString("intl");
                    writer.WriteString("International");
                    writer.WriteArray(new[] { "en" });
                    writer.WriteStructureStart();
                    writer.WriteString("dvorak");
                    writer.WriteString("Dvorak");
                    writer.WriteArray(Array.Empty<string>());
                    writer.WriteArrayEnd(variants);
                    writer.WriteArrayEnd(array);
                });
                break;

            case "InputMethodGroups":
                Reply(context, "as", static (ref MessageWriter writer) => writer.WriteArray(new[] { "Default", "Work" }));
                break;

            case "CurrentInputMethodGroup":
                Reply(context, "s", static (ref MessageWriter writer) => writer.WriteString("Default"));
                break;

            case "FullInputMethodGroupInfo":
                {
                    var group = Group;
                    Reply(context, "sssa{sv}a(sssssssbsa{sv})", (ref MessageWriter writer) =>
                    {
                        writer.WriteString(group?.Name ?? string.Empty);
                        writer.WriteString(group?.DefaultInputMethod ?? string.Empty);
                        writer.WriteString(group?.DefaultLayout ?? string.Empty);
                        WriteMap(ref writer, group?.Properties ?? new RawConfigValue());

                        var entries = writer.WriteArrayStart(DBusType.Struct);
                        foreach (var item in group?.Items ?? Array.Empty<FullInputMethodEntry>())
                        {
                            writer.WriteStructureStart();
                            writer.WriteString(item.UniqueName);
                            writer.WriteString(item.Name);
                            writer.WriteString(item.NativeName);
                            writer.WriteString(item.Icon);
                            writer.WriteString(item.Label);
                            writer.WriteString(item.LanguageCode);
                            writer.WriteString(item.Addon);
                            writer.WriteBool(item.Configurable);
                            writer.WriteString(item.Layout);
                            WriteMap(ref writer, item.Properties);
                        }

                        writer.WriteArrayEnd(entries);
                    });
                    break;
                }

            case "SetInputMethodGroupInfo":
                {
                    var reader = request.GetBodyReader();
                    var name = reader.ReadString();
                    var layout = reader.ReadString();
                    var items = new List<FullInputMethodEntry>();
                    var end = reader.ReadArrayStart(DBusType.Struct);
                    while (reader.HasNext(end))
                    {
                        reader.AlignStruct();
                        var uniqueName = reader.ReadString();
                        var itemLayout = reader.ReadString();
                        items.Add(new FullInputMethodEntry(
                            uniqueName, uniqueName, uniqueName, "", "", "", "", false, itemLayout, null));
                    }

                    LastSavedGroup = new InputMethodGroup(name, string.Empty, layout, items.ToArray());
                    ReplyEmpty(context);
                    break;
                }

            case "CanRestart":
                Reply(context, "b", static (ref MessageWriter writer) => writer.WriteBool(true));
                break;

            default:
                context.ReplyUnknownMethodError();
                break;
        }

        return default;
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.ReleaseNameAsync(ServiceName).ConfigureAwait(false);
        _connection.Dispose();
    }

    /// <summary>Writes a bare <c>a{sv}</c> member, as the daemon does for properties.</summary>
    private static void WriteMap(ref MessageWriter writer, RawConfigValue node)
    {
        var map = writer.WriteDictionaryStart();
        if (!string.IsNullOrEmpty(node.Value))
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(RawConfigValue.SelfKey);
            writer.WriteVariant(VariantValue.String(node.Value!));
        }

        foreach (var pair in node.OrderedChildren)
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(pair.Key);
            writer.WriteVariant(ConfigValueCodec.Write(pair.Value));
        }

        writer.WriteDictionaryEnd(map);
    }

    private static void WriteAddonsV2(ref MessageWriter writer)
    {
        var array = writer.WriteArrayStart(DBusType.Struct);

        writer.WriteStructureStart();
        writer.WriteString("pinyin");
        writer.WriteString("Pinyin");
        writer.WriteString("Chinese input method");
        writer.WriteInt32(0);
        writer.WriteBool(true);
        writer.WriteBool(true);
        writer.WriteBool(false);
        writer.WriteArray(new[] { "core", "dbus" });
        writer.WriteArray(new[] { "quickphrase" });

        writer.WriteStructureStart();
        writer.WriteString("xim");
        writer.WriteString("XIM");
        writer.WriteString("X11 input method server");
        writer.WriteInt32(1);
        writer.WriteBool(false);
        writer.WriteBool(true);
        writer.WriteBool(false);
        writer.WriteArray(Array.Empty<string>());
        writer.WriteArray(Array.Empty<string>());

        writer.WriteArrayEnd(array);
    }

    private static void WriteAddons(ref MessageWriter writer)
    {
        var array = writer.WriteArrayStart(DBusType.Struct);

        writer.WriteStructureStart();
        writer.WriteString("pinyin");
        writer.WriteString("Pinyin");
        writer.WriteString("Chinese input method");
        writer.WriteInt32(0);
        writer.WriteBool(true);
        writer.WriteBool(true);

        writer.WriteArrayEnd(array);
    }

    private static void ReplyEmpty(MethodContext context) => Reply(context, null, static (ref MessageWriter _) => { });

    private static void Reply(MethodContext context, string? signature, WriteAction write)
    {
        var writer = context.CreateReplyWriter(signature);
        MessageBuffer buffer;
        try
        {
            write(ref writer);
            buffer = writer.CreateMessage();
        }
        finally
        {
            writer.Dispose();
        }

        context.Reply(buffer);
    }

    /// <summary>A reply body writer; <see cref="MessageWriter"/> is a ref struct and cannot be boxed.</summary>
    private delegate void WriteAction(ref MessageWriter writer);
}
