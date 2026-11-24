using System;

namespace Fcitx5.Config.Core.Model;

/// <summary>The result of reading a configuration URI.</summary>
public sealed class ConfigSnapshot
{
    /// <summary>Creates a snapshot.</summary>
    public ConfigSnapshot(string uri, RawConfigValue value, ConfigDescription description)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Description = description ?? ConfigDescription.Empty;
    }

    /// <summary>The configuration URI this snapshot was read from.</summary>
    public string Uri { get; }

    /// <summary>The current values.</summary>
    public RawConfigValue Value { get; }

    /// <summary>The description (schema) of the configuration.</summary>
    public ConfigDescription Description { get; }

    /// <summary>Looks up an option description by name across every group.</summary>
    public OptionDescription? FindOption(string optionName) => Description.FindOption(optionName);

    /// <summary>
    /// Reads the current value of the option at a slash separated path, falling back to the
    /// option's default value when it is absent. Returns <see langword="null"/> when neither exists.
    /// </summary>
    public RawConfigValue? GetOptionValue(string path)
    {
        if (Value.TryFindPath(path, out var current))
        {
            return current;
        }

        var separator = path.LastIndexOf('/');
        var optionName = separator < 0 ? path : path[(separator + 1)..];
        return FindOption(optionName)?.DefaultValue;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Uri}: {Description.Types.Length} group(s)";
}
