using System.Collections.Generic;
using Fcitx5.Config.Core.Abstractions;
using Fcitx5.Config.Core.Model;
using Tmds.DBus.Protocol;

namespace Fcitx5.Config.Core.Transport.Dbus;

/// <summary>
/// Converts fcitx5 configuration payloads between <see cref="RawConfigValue"/> and the
/// <see cref="VariantValue"/> trees the generated proxy hands over.
/// </summary>
/// <remarks>
/// <para>
/// The wire format is defined by <c>rawConfigToVariant</c>, <c>variantToRawConfig</c> and
/// <c>dumpDBusConfigDescription</c> in fcitx5's <c>src/lib/fcitx-config/dbushelper.cpp</c>:
/// </para>
/// <list type="bullet">
/// <item>a value is either a string (a leaf) or an <c>a{sv}</c> map whose empty key carries the
/// node's own value;</item>
/// <item>a description is <c>a(sa(sssva{sv}))</c>, where the option's default value is a
/// <c>v</c> and its properties a bare <c>a{sv}</c>.</item>
/// </list>
/// <para>
/// Map entries are visited in wire order through <see cref="VariantValue.GetDictionaryEntry"/>,
/// so a decoded tree keeps the order its entries arrived in. A caller that writes list elements as
/// "1" then "0" gets them back that way, which is what fcitx5 expects: the stored order is the
/// list order.
/// </para>
/// <para>
/// The properties of an option, a group or a group entry arrive as a plain dictionary, so their
/// enumeration order is not part of the contract. Nothing reads them positionally: constraint and
/// annotation keys are looked up by name, and the enum constraints carry their position in the key
/// (<c>Enum/0</c>, <c>Enum/1</c>, ...).
/// </para>
/// <para>
/// <see cref="VariantValue"/> holds the decoded payload as managed objects rather than a view of
/// the message, so a tree may be converted after the reply has been awaited.
/// </para>
/// </remarks>
internal static class ConfigValueCodec
{
    /// <summary>Reads a <c>v</c> holding a configuration tree.</summary>
    /// <exception cref="FcitxUnexpectedResponseException">
    /// The value is neither a string nor a map. fcitx5 emits nothing else, so this means the
    /// protocol changed; failing beats coercing the value into a string.
    /// </exception>
    public static RawConfigValue Read(VariantValue value)
    {
        if (value.Type == VariantValueType.String)
        {
            return new RawConfigValue(value.GetString());
        }

        if (value.Type != VariantValueType.Dictionary)
        {
            throw new FcitxUnexpectedResponseException(
                $"A configuration value must be a string or a map, but the daemon sent '{value.Type}'.");
        }

        var node = new RawConfigValue();
        for (var i = 0; i < value.Count; i++)
        {
            var entry = value.GetDictionaryEntry(i);
            AddEntry(node, entry.Key.GetString(), entry.Value);
        }

        return node;
    }

    /// <summary>Reads a bare <c>a{sv}</c> map holding a configuration tree.</summary>
    public static RawConfigValue ReadMap(IReadOnlyDictionary<string, VariantValue>? map)
    {
        var node = new RawConfigValue();
        if (map is null)
        {
            return node;
        }

        foreach (var pair in map)
        {
            AddEntry(node, pair.Key, pair.Value);
        }

        return node;
    }

    /// <summary>Builds the <c>v</c> the daemon expects for a configuration tree.</summary>
    public static VariantValue Write(RawConfigValue value)
    {
        if (!value.HasChildren)
        {
            return VariantValue.String(value.Value ?? string.Empty);
        }

        var map = new Dict<string, VariantValue>();
        var selfValue = value.Value;
        if (!string.IsNullOrEmpty(selfValue))
        {
            map.Add(RawConfigValue.SelfKey, VariantValue.String(selfValue));
        }

        foreach (var pair in value.OrderedChildren)
        {
            map.Add(pair.Key, Write(pair.Value));
        }

        return map.AsVariantValue();
    }

    /// <summary>Reads an <c>a(sa(sssva{sv}))</c> configuration description.</summary>
    public static ConfigDescription ReadDescription(
        (string, (string, string, string, VariantValue, Dictionary<string, VariantValue>)[])[] groups)
    {
        if (groups.Length == 0)
        {
            return ConfigDescription.Empty;
        }

        var types = new ConfigTypeDescription[groups.Length];
        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            var (groupName, rawOptions) = groups[groupIndex];
            var options = new OptionDescription[rawOptions.Length];

            for (var optionIndex = 0; optionIndex < rawOptions.Length; optionIndex++)
            {
                var (name, type, description, defaultValue, properties) = rawOptions[optionIndex];
                options[optionIndex] = new OptionDescription(
                    name,
                    OptionType.Parse(type),
                    description,
                    Read(defaultValue),
                    // The tree is decoded here and never handed out before, so it needs no copy.
                    OptionConstraints.FromOwned(ReadMap(properties)));
            }

            types[groupIndex] = new ConfigTypeDescription(groupName, options);
        }

        return new ConfigDescription(types);
    }

    private static void AddEntry(RawConfigValue node, string key, VariantValue value)
    {
        if (key.Length == 0)
        {
            node.Value = ReadSelfValue(value);
        }
        else
        {
            node.SetChild(key, Read(value));
        }
    }

    private static string ReadSelfValue(VariantValue value) =>
        value.Type == VariantValueType.String
            ? value.GetString()
            : throw new FcitxUnexpectedResponseException(
                $"The own value of a configuration node must be a string, but the daemon sent '{value.Type}'.");
}
