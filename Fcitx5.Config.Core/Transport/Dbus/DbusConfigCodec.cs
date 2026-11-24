using System;
using System.Collections.Generic;
using System.Globalization;
using Fcitx5.Config.Core.Model;

namespace Fcitx5.Config.Core.Transport.Dbus;

/// <summary>
/// Encodes and decodes fcitx5 configuration payloads for the D-Bus transport.
/// </summary>
/// <remarks>
/// The wire format is defined by <c>rawConfigToVariant</c> / <c>variantToRawConfig</c> and
/// <c>dumpDBusConfigDescription</c> in fcitx5's <c>src/lib/fcitx-config/dbushelper.cpp</c>:
/// <list type="bullet">
/// <item>a value is either a string (a leaf) or an <c>a{sv}</c> dictionary whose empty key carries
/// the node's own value;</item>
/// <item>a description is <c>a(sa(sssva{sv}))</c>.</item>
/// </list>
/// Every leaf is a D-Bus string; fcitx5 never emits numeric variants for configuration data, so
/// integers, booleans and colours all travel as their textual representation.
/// <para>
/// Encoded dictionaries are built as <see cref="KeyValuePair{TKey,TValue}"/> arrays rather than
/// <see cref="Dictionary{TKey,TValue}"/> instances. fcitx5 stores decoded entries in an ordered map,
/// so the wire order defines the order of list elements; <see cref="Dictionary{TKey,TValue}"/>
/// enumeration order is not specified and could silently reorder a configuration list.
/// </para>
/// </remarks>
public sealed class DbusConfigCodec
{
    /// <summary>A shared codec instance; the codec is stateless.</summary>
    public static DbusConfigCodec Instance { get; } = new();

    /// <summary>Decodes a D-Bus value variant into the shared model.</summary>
    public RawConfigValue DecodeValue(object? payload)
    {
        switch (payload)
        {
            case null:
                return new RawConfigValue();

            case string text:
                return new RawConfigValue(text);

            case IDictionary<string, object> map:
                return DecodeEntries(map);

            case IEnumerable<KeyValuePair<string, object>> entries:
                // Also accept the array form produced by EncodeValue, so the codec round-trips on
                // its own without a D-Bus round-trip in between.
                return DecodeEntries(entries);

            default:
                return new RawConfigValue(AsString(payload));
        }
    }

    /// <summary>
    /// Encodes the shared model into a D-Bus value variant: a string for a leaf, or an ordered
    /// <c>a{sv}</c> dictionary for a node with children.
    /// </summary>
    public object EncodeValue(RawConfigValue value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!value.HasChildren)
        {
            return value.Value ?? string.Empty;
        }

        var entries = new List<KeyValuePair<string, object>>(value.ChildNames.Count + 1);

        if (!string.IsNullOrEmpty(value.Value))
        {
            entries.Add(new KeyValuePair<string, object>(RawConfigValue.SelfKey, value.Value!));
        }

        foreach (var pair in value.OrderedChildren)
        {
            entries.Add(new KeyValuePair<string, object>(pair.Key, EncodeValue(pair.Value)));
        }

        return entries.ToArray();
    }

    /// <summary>Decodes a D-Bus configuration description into the shared model.</summary>
    public ConfigDescription DecodeDescription(object? payload)
    {
        var types = payload as DbusConfigType[];
        if (types is null || types.Length == 0)
        {
            return ConfigDescription.Empty;
        }

        var result = new ConfigTypeDescription[types.Length];
        for (var i = 0; i < types.Length; i++)
        {
            var type = types[i];
            var source = type.Options ?? Array.Empty<DbusConfigOption>();
            var options = new OptionDescription[source.Length];
            for (var j = 0; j < source.Length; j++)
            {
                var option = source[j];
                options[j] = new OptionDescription(
                    option.Name ?? string.Empty,
                    OptionType.Parse(option.Type),
                    option.Description ?? string.Empty,
                    option.DefaultValue is null ? null : DecodeValue(option.DefaultValue),
                    // The decoded tree is fresh and never mutated afterwards, so skip the
                    // defensive copy the public constructor would make.
                    OptionConstraints.FromOwned(DecodeValue(option.Properties)));
            }

            result[i] = new ConfigTypeDescription(type.Name ?? string.Empty, options);
        }

        return new ConfigDescription(result);
    }

    private static RawConfigValue DecodeEntries(IEnumerable<KeyValuePair<string, object>> entries)
    {
        var node = new RawConfigValue();
        foreach (var pair in entries)
        {
            if (pair.Key.Length == 0)
            {
                node.Value = AsString(pair.Value);
            }
            else
            {
                node.SetChild(pair.Key, Prepare(pair.Value));
            }
        }

        return node;
    }

    private static RawConfigValue Prepare(object? payload) => payload switch
    {
        null => new RawConfigValue(),
        IDictionary<string, object> map => DecodeEntries(map),
        IEnumerable<KeyValuePair<string, object>> entries => DecodeEntries(entries),
        _ => new RawConfigValue(AsString(payload)),
    };

    private static string AsString(object? payload) => payload switch
    {
        null => string.Empty,
        string text => text,
        _ => Convert.ToString(payload, CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
