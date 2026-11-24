using System.Collections.Generic;
using Fcitx5.Config.Core.Model;
using Tmds.DBus.Protocol;

namespace Fcitx5.Config.Core.Transport.Dbus;

/// <summary>
/// Encodes and decodes fcitx5 configuration payloads on the D-Bus wire format.
/// </summary>
/// <remarks>
/// <para>
/// The format is defined by <c>rawConfigToVariant</c> / <c>variantToRawConfig</c> and
/// <c>dumpDBusConfigDescription</c> in fcitx5's <c>src/lib/fcitx-config/dbushelper.cpp</c>:
/// </para>
/// <list type="bullet">
/// <item>a value is either a string (a leaf) or an <c>a{sv}</c> map whose empty key carries the
/// node's own value;</item>
/// <item>a description is <c>a(sa(sssva{sv}))</c>, where the option's default value is a
/// <c>v</c> and its properties a bare <c>a{sv}</c>.</item>
/// </list>
/// <para>
/// Both encode and decode are streamed through the message reader and writer instead of building
/// intermediate dictionaries. That keeps the arrival order of map entries, which <em>is</em> the
/// order of a fcitx5 list, and avoids materialising a boxed object graph per configuration.
/// </para>
/// <para>
/// A variant (<c>v</c>) carries its content type signature before the value; a bare <c>a{sv}</c>
/// struct member does not. Mixing the two up produces a well formed but semantically wrong
/// message, which the bus rejects by dropping the connection.
/// </para>
/// </remarks>
internal static class DbusConfigCodec
{
    private const string StringSignature = "s";
    private const string MapSignature = "a{sv}";

    // ------------------------------------------------------------------ writing

    /// <summary>Writes a <c>v</c> whose content is a configuration tree.</summary>
    public static void WriteVariant(ref MessageWriter writer, RawConfigValue value)
    {
        if (!value.HasChildren)
        {
            writer.WriteVariantString(value.Value ?? string.Empty);
            return;
        }

        writer.WriteSignature(MapSignature);
        WriteMap(ref writer, value);
    }

    /// <summary>Writes a bare <c>a{sv}</c> map holding a configuration tree.</summary>
    public static void WriteMap(ref MessageWriter writer, RawConfigValue value)
    {
        var start = writer.WriteDictionaryStart();

        if (!string.IsNullOrEmpty(value.Value))
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(RawConfigValue.SelfKey);
            writer.WriteVariantString(value.Value!);
        }

        foreach (var pair in value.OrderedChildren)
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(pair.Key);
            WriteVariant(ref writer, pair.Value);
        }

        writer.WriteDictionaryEnd(start);
    }

    // ------------------------------------------------------------------ reading

    /// <summary>Reads a <c>v</c> holding a configuration tree.</summary>
    public static RawConfigValue ReadVariant(ref Reader reader)
    {
        var signature = reader.ReadSignatureAsString();
        if (signature == StringSignature)
        {
            return new RawConfigValue(reader.ReadString());
        }

        if (signature == MapSignature)
        {
            return ReadMap(ref reader);
        }

        // fcitx5 only ever emits a string or a map here (every configuration leaf is textual).
        // Anything else means the protocol changed, so fail with the signature instead of
        // silently coercing the value into the string based model.
        throw new DBusReadException($"Unsupported configuration value signature '{signature}'.");
    }

    /// <summary>Reads a bare <c>a{sv}</c> map holding a configuration tree.</summary>
    public static RawConfigValue ReadMap(ref Reader reader)
    {
        var node = new RawConfigValue();
        var end = reader.ReadDictionaryStart();
        while (reader.HasNext(end))
        {
            reader.AlignStruct();
            var key = reader.ReadString();
            var value = ReadVariant(ref reader);

            if (key.Length == 0)
            {
                node.Value = value.Value;
            }
            else
            {
                node.SetChild(key, value);
            }
        }

        return node;
    }

    /// <summary>Reads an <c>a(sa(sssva{sv}))</c> configuration description.</summary>
    public static ConfigDescription ReadDescription(ref Reader reader)
    {
        var groups = new List<ConfigTypeDescription>();
        var end = reader.ReadArrayStart(DBusType.Struct);
        while (reader.HasNext(end))
        {
            reader.AlignStruct();
            var groupName = reader.ReadString();

            var options = new List<OptionDescription>();
            var optionsEnd = reader.ReadArrayStart(DBusType.Struct);
            while (reader.HasNext(optionsEnd))
            {
                reader.AlignStruct();
                var name = reader.ReadString();
                var type = reader.ReadString();
                var description = reader.ReadString();
                var defaultValue = ReadVariant(ref reader);
                var properties = ReadMap(ref reader);

                options.Add(new OptionDescription(
                    name,
                    OptionType.Parse(type),
                    description,
                    defaultValue,
                    // The decoded tree is private to this option, so skip the defensive copy.
                    OptionConstraints.FromOwned(properties)));
            }

            groups.Add(new ConfigTypeDescription(groupName, options.ToArray()));
        }

        return groups.Count == 0 ? ConfigDescription.Empty : new ConfigDescription(groups.ToArray());
    }
}
