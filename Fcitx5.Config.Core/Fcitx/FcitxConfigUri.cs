using System;
using Fcitx5.Config.Core.Model;

namespace Fcitx5.Config.Core.Fcitx;

/// <summary>The kind of configuration a fcitx5 URI points at.</summary>
public enum FcitxConfigTarget
{
    /// <summary>The URI is not a fcitx5 configuration URI.</summary>
    Unknown = 0,

    /// <summary>The global configuration.</summary>
    Global,

    /// <summary>One addon's configuration.</summary>
    Addon,

    /// <summary>One input method's configuration.</summary>
    InputMethod,
}

/// <summary>A parsed fcitx5 configuration URI.</summary>
public readonly struct FcitxConfigUri
{
    private const string GlobalSegment = "global";
    private const string AddonSegment = "addon/";
    private const string InputMethodSegment = "inputmethod/";

    private FcitxConfigUri(FcitxConfigTarget target, string name, string? subPath)
    {
        Target = target;
        Name = name;
        SubPath = subPath;
    }

    /// <summary>The scheme prefix shared by every configuration URI.</summary>
    public const string Prefix = "fcitx://config/";

    /// <summary>The URI of the global configuration.</summary>
    public const string GlobalUri = Prefix + GlobalSegment;

    /// <summary>What this URI points at.</summary>
    public FcitxConfigTarget Target { get; }

    /// <summary>The addon name or input method unique name; empty for the global configuration.</summary>
    public string Name { get; }

    /// <summary>The addon sub-configuration path, when one was given.</summary>
    public string? SubPath { get; }

    /// <summary>Builds the URI of the global configuration.</summary>
    public static string Global() => GlobalUri;

    /// <summary>Builds the URI of an addon's configuration.</summary>
    public static string Addon(string addonName, string? subPath = null)
    {
        if (string.IsNullOrEmpty(addonName))
        {
            throw new ArgumentException("An addon name is required.", nameof(addonName));
        }

        return string.IsNullOrEmpty(subPath)
            ? Prefix + AddonSegment + addonName
            : Prefix + AddonSegment + addonName + "/" + subPath;
    }

    /// <summary>Builds the URI of an input method's configuration.</summary>
    public static string InputMethod(string uniqueName)
    {
        if (string.IsNullOrEmpty(uniqueName))
        {
            throw new ArgumentException("An input method unique name is required.", nameof(uniqueName));
        }

        return Prefix + InputMethodSegment + uniqueName;
    }

    /// <summary>Parses a fcitx5 configuration URI.</summary>
    public static bool TryParse(string? uri, out FcitxConfigUri parsed)
    {
        parsed = default;

        if (uri is null || uri.Length <= Prefix.Length ||
            !uri.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return TryParseTail(uri.AsSpan(Prefix.Length), out parsed);
    }

    private static bool TryParseTail(ReadOnlySpan<char> remainder, out FcitxConfigUri parsed)
    {
        if (remainder.Equals(GlobalSegment.AsSpan(), StringComparison.Ordinal))
        {
            parsed = new FcitxConfigUri(FcitxConfigTarget.Global, string.Empty, null);
            return true;
        }

        if (remainder.StartsWith(AddonSegment.AsSpan(), StringComparison.Ordinal))
        {
            var path = remainder[AddonSegment.Length..];
            if (path.Length == 0)
            {
                parsed = default;
                return false;
            }

            var slash = path.IndexOf('/');
            parsed = slash < 0
                ? new FcitxConfigUri(FcitxConfigTarget.Addon, path.ToString(), null)
                : new FcitxConfigUri(
                    FcitxConfigTarget.Addon,
                    path[..slash].ToString(),
                    path[(slash + 1)..].ToString());
            return true;
        }

        if (remainder.StartsWith(InputMethodSegment.AsSpan(), StringComparison.Ordinal))
        {
            var name = remainder[InputMethodSegment.Length..];
            if (name.Length == 0)
            {
                parsed = default;
                return false;
            }

            parsed = new FcitxConfigUri(FcitxConfigTarget.InputMethod, name.ToString(), null);
            return true;
        }

        parsed = default;
        return false;
    }
}
