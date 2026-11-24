using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Fcitx5.Config.Core.Model;

/// <summary>
/// Transport-neutral representation of a fcitx5 <c>RawConfig</c> tree.
/// </summary>
/// <remarks>
/// <para>
/// A fcitx5 raw config node is either a leaf holding a string, or a node that owns an optional
/// string value plus a set of named children. On the D-Bus wire format this maps to a variant
/// that is either a string or an <c>a{sv}</c> dictionary, where the empty key ("") carries the
/// node's own value. See <c>rawConfigToVariant</c> in fcitx5's <c>dbushelper.cpp</c>.
/// </para>
/// <para>
/// Lists are represented as nodes whose child names are "0", "1", "2", ... in order. Child order
/// is preserved exactly as received, because fcitx5 lists are order sensitive.
/// </para>
/// <para>
/// Instances are mutable and not thread safe. The read-only views handed out by
/// <see cref="ChildNames"/> and <see cref="Children"/> are created on first use and reflect later
/// mutations.
/// </para>
/// </remarks>
public sealed class RawConfigValue
{
    /// <summary>
    /// Key used inside a D-Bus <c>a{sv}</c> node to carry the node's own value.
    /// </summary>
    public const string SelfKey = "";

    private const char PathSeparator = '/';

    private readonly Dictionary<string, RawConfigValue> _children = new(StringComparer.Ordinal);
    private readonly List<string> _order = new();

    // Created lazily: most nodes are only ever walked as a tree, so allocating two wrapper
    // objects for every node up front is wasted work on large configurations.
    private IReadOnlyDictionary<string, RawConfigValue>? _childrenView;
    private IReadOnlyList<string>? _namesView;

    /// <summary>Creates an empty node.</summary>
    public RawConfigValue()
        : this(null)
    {
    }

    /// <summary>Creates a leaf node holding <paramref name="value"/>.</summary>
    public RawConfigValue(string? value)
    {
        Value = value;
    }

    /// <summary>The node's own value, or <see langword="null"/> when it has none.</summary>
    public string? Value { get; set; }

    /// <summary>
    /// The child names, in the order they were received. The returned view is read-only; use
    /// <see cref="SetChild(string, RawConfigValue)"/>, <see cref="GetOrAddChild"/> and
    /// <see cref="RemoveChild"/> to modify the node.
    /// </summary>
    public IReadOnlyList<string> ChildNames => _namesView ??= _order.AsReadOnly();

    /// <summary>
    /// The children keyed by name. The returned view is read-only; mutating the backing store
    /// directly would desynchronise it from <see cref="ChildNames"/>.
    /// </summary>
    public IReadOnlyDictionary<string, RawConfigValue> Children =>
        _childrenView ??= new ReadOnlyDictionary<string, RawConfigValue>(_children);

    /// <summary>Whether this node has any children.</summary>
    public bool HasChildren => _children.Count > 0;

    /// <summary>Whether this node is a leaf (no children).</summary>
    public bool IsLeaf => _children.Count == 0;

    /// <summary>Creates a new empty node.</summary>
    public static RawConfigValue Empty => new();

    /// <summary>The children in insertion order.</summary>
    public IEnumerable<KeyValuePair<string, RawConfigValue>> OrderedChildren
    {
        get
        {
            for (var i = 0; i < _order.Count; i++)
            {
                var name = _order[i];
                yield return new KeyValuePair<string, RawConfigValue>(name, _children[name]);
            }
        }
    }

    /// <summary>
    /// Adds or replaces a child, keeping the original position when replacing. The name must be
    /// non-empty, because the empty name is reserved for a node's own value on the wire.
    /// </summary>
    public void SetChild(string name, RawConfigValue child)
    {
        ValidateChildName(name);

        if (child is null)
        {
            throw new ArgumentNullException(nameof(child));
        }

        // TryAdd keeps the common "new child" case to a single dictionary probe.
        if (_children.TryAdd(name, child))
        {
            _order.Add(name);
        }
        else
        {
            _children[name] = child;
        }
    }

    /// <summary>Adds or replaces a leaf child.</summary>
    public void SetChild(string name, string value) => SetChild(name, new RawConfigValue(value));

    /// <summary>Gets or creates a child node.</summary>
    public RawConfigValue GetOrAddChild(string name)
    {
        ValidateChildName(name);

        if (_children.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var created = new RawConfigValue();
        _children[name] = created;
        _order.Add(name);
        return created;
    }

    /// <summary>Removes a child.</summary>
    public bool RemoveChild(string name)
    {
        if (name is null || !_children.Remove(name))
        {
            return false;
        }

        _order.Remove(name);
        return true;
    }

    /// <summary>Removes every child.</summary>
    public void Clear()
    {
        _children.Clear();
        _order.Clear();
    }

    /// <summary>Returns the child with this name, or <see langword="null"/>.</summary>
    public RawConfigValue? Find(string? name) =>
        name is not null && _children.TryGetValue(name, out var child) ? child : null;

    /// <summary>Looks up a child; returns <see langword="false"/> when it does not exist.</summary>
    public bool TryFind(string? name, [NotNullWhen(true)] out RawConfigValue? child)
    {
        child = Find(name);
        return child is not null;
    }

    /// <summary>Gets a child, or <see langword="null"/> when it does not exist.</summary>
    public RawConfigValue? this[string name] => Find(name);

    /// <summary>
    /// Gets the node at a slash separated path (fcitx5 convention), or <see langword="null"/>.
    /// An empty path returns this node.
    /// </summary>
    public RawConfigValue? FindPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return this;
        }

        var current = this;
        var remaining = path.AsSpan();

        while (true)
        {
            var separator = remaining.IndexOf(PathSeparator);
            var segment = separator < 0 ? remaining : remaining[..separator];

            if (segment.Length == 0)
            {
                return null;
            }

            var next = current.Find(segment.ToString());
            if (next is null)
            {
                return null;
            }

            if (separator < 0)
            {
                return next;
            }

            current = next;
            remaining = remaining[(separator + 1)..];
        }
    }

    /// <summary>Looks up a node by path; returns <see langword="false"/> when it does not exist.</summary>
    public bool TryFindPath(string path, [NotNullWhen(true)] out RawConfigValue? node)
    {
        node = FindPath(path);
        return node is not null;
    }

    /// <summary>
    /// Gets the node at a slash separated path, creating missing nodes along the way.
    /// An empty path returns this node.
    /// </summary>
    public RawConfigValue GetOrAddPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return this;
        }

        var current = this;
        var remaining = path.AsSpan();

        while (true)
        {
            var separator = remaining.IndexOf(PathSeparator);
            var segment = separator < 0 ? remaining : remaining[..separator];

            if (segment.Length == 0)
            {
                throw new ArgumentException("A path must not contain empty segments.", nameof(path));
            }

            current = current.GetOrAddChild(segment.ToString());

            if (separator < 0)
            {
                return current;
            }

            remaining = remaining[(separator + 1)..];
        }
    }

    /// <summary>Sets a leaf value at a slash separated path, creating missing nodes.</summary>
    public void SetPath(string path, string value) => GetOrAddPath(path).Value = value;

    /// <summary>Sets a node at a slash separated path, creating missing parents.</summary>
    public void SetPath(string path, RawConfigValue node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (string.IsNullOrEmpty(path))
        {
            Value = node.Value;
            Clear();
            foreach (var pair in node.OrderedChildren)
            {
                SetChild(pair.Key, pair.Value.DeepClone());
            }

            return;
        }

        var separator = path.LastIndexOf(PathSeparator);
        if (separator < 0)
        {
            SetChild(path, node);
            return;
        }

        GetOrAddPath(path[..separator]).SetChild(path[(separator + 1)..], node);
    }

    /// <summary>
    /// Reads this node as an ordered list of child values (fcitx5 lists use numeric names).
    /// </summary>
    public IReadOnlyList<RawConfigValue> ToList()
    {
        var result = new RawConfigValue[_order.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = _children[_order[i]];
        }

        return result;
    }

    /// <summary>Reads this node as a list of child strings.</summary>
    public IReadOnlyList<string> ToStringList()
    {
        var result = new string[_order.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = _children[_order[i]].Value ?? string.Empty;
        }

        return result;
    }

    /// <summary>Creates a new node holding <paramref name="values"/> as a list.</summary>
    public static RawConfigValue FromList(IEnumerable<string> values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var node = new RawConfigValue();
        var index = 0;
        foreach (var value in values)
        {
            node.SetChild(index.ToString(CultureInfo.InvariantCulture), value);
            index++;
        }

        return node;
    }

    /// <summary>Creates a deep copy of this node.</summary>
    public RawConfigValue DeepClone()
    {
        var clone = new RawConfigValue(Value);
        foreach (var name in _order)
        {
            clone.SetChild(name, _children[name].DeepClone());
        }

        return clone;
    }

    /// <summary>Compares shape and values recursively.</summary>
    public bool DeepEquals(RawConfigValue? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (!string.Equals(Value, other.Value, StringComparison.Ordinal) ||
            _order.Count != other._order.Count)
        {
            return false;
        }

        for (var i = 0; i < _order.Count; i++)
        {
            var name = _order[i];
            if (!string.Equals(name, other._order[i], StringComparison.Ordinal) ||
                !_children[name].DeepEquals(other._children[other._order[i]]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var builder = new StringBuilder();
        AppendTo(builder);
        return builder.ToString();
    }

    private static void ValidateChildName(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (name.Length == 0)
        {
            throw new ArgumentException(
                "A child name must not be empty; the empty name carries a node's own value.",
                nameof(name));
        }
    }

    private void AppendTo(StringBuilder builder)
    {
        if (Value is not null)
        {
            builder.Append(Value);
        }

        if (!HasChildren)
        {
            return;
        }

        builder.Append('{');
        for (var i = 0; i < _order.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var name = _order[i];
            builder.Append(name).Append('=');
            _children[name].AppendTo(builder);
        }

        builder.Append('}');
    }
}
