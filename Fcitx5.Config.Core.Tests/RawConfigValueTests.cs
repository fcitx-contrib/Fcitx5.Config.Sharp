using Fcitx5.Config.Core.Model;

namespace Fcitx5.Config.Core.Tests;

public class RawConfigValueTests
{
    [Fact]
    public void Leaf_HasNoChildren()
    {
        var node = new RawConfigValue("False");

        Assert.Equal("False", node.Value);
        Assert.True(node.IsLeaf);
        Assert.False(node.HasChildren);
        Assert.Empty(node.ChildNames);
    }

    [Fact]
    public void SetChild_PreservesInsertionOrder()
    {
        var node = new RawConfigValue();
        node.SetChild("2", "c");
        node.SetChild("0", "a");
        node.SetChild("1", "b");

        Assert.Equal(new[] { "2", "0", "1" }, node.ChildNames);
        Assert.Equal(new[] { "c", "a", "b" }, node.ToStringList());
    }

    [Fact]
    public void SetChild_OnExistingKey_KeepsPosition()
    {
        var node = new RawConfigValue();
        node.SetChild("a", "1");
        node.SetChild("b", "2");
        node.SetChild("a", "3");

        Assert.Equal(new[] { "a", "b" }, node.ChildNames);
        Assert.Equal("3", node["a"]!.Value);
    }

    [Fact]
    public void Paths_GetSetAndCreate()
    {
        var node = new RawConfigValue();
        node.SetPath("Hotkey/TriggerKeys/0", "Control+space");
        node.SetPath("Hotkey/TriggerKeys/1", "Zenkaku_Hankaku");

        Assert.Equal("Control+space", node.FindPath("Hotkey/TriggerKeys/0")!.Value);
        Assert.Equal(2, node.FindPath("Hotkey/TriggerKeys")!.ChildNames.Count);
        Assert.Null(node.FindPath("Hotkey/Missing"));
        Assert.Same(node, node.FindPath(string.Empty));
    }

    [Fact]
    public void FromList_UsesNumericKeys()
    {
        var node = RawConfigValue.FromList(new[] { "a", "b", "c" });

        Assert.Equal(new[] { "0", "1", "2" }, node.ChildNames);
        Assert.Equal(new[] { "a", "b", "c" }, node.ToStringList());
    }

    [Fact]
    public void ToList_ReturnsChildNodesInOrder()
    {
        var node = new RawConfigValue();
        var first = node.GetOrAddChild("0");
        first.SetChild("Name", "x");
        node.GetOrAddChild("1").SetChild("Name", "y");

        var list = node.ToList();

        Assert.Equal(2, list.Count);
        Assert.Equal("x", list[0].FindPath("Name")!.Value);
    }

    [Fact]
    public void DeepClone_IsIndependent()
    {
        var original = new RawConfigValue();
        original.SetPath("A/B", "1");

        var clone = original.DeepClone();
        clone.SetPath("A/B", "2");

        Assert.Equal("1", original.FindPath("A/B")!.Value);
        Assert.Equal("2", clone.FindPath("A/B")!.Value);
        Assert.True(original.DeepEquals(original.DeepClone()));
        Assert.False(original.DeepEquals(clone));
    }

    [Fact]
    public void DeepEquals_ComparesValuesAndOrder()
    {
        var left = RawConfigValue.FromList(new[] { "a", "b" });
        var right = RawConfigValue.FromList(new[] { "b", "a" });

        Assert.False(left.DeepEquals(right));
        Assert.True(left.DeepEquals(RawConfigValue.FromList(new[] { "a", "b" })));
        Assert.False(left.DeepEquals(null));
    }

    [Fact]
    public void RemoveChild_UpdatesOrder()
    {
        var node = new RawConfigValue();
        node.SetChild("a", "1");
        node.SetChild("b", "2");

        Assert.True(node.RemoveChild("a"));
        Assert.False(node.RemoveChild("a"));
        Assert.Equal(new[] { "b" }, node.ChildNames);
    }

    [Fact]
    public void SetPath_WithEmptyPath_ReplacesContent()
    {
        var node = new RawConfigValue("old");
        node.SetPath("X", "1");

        node.SetPath(string.Empty, new RawConfigValue("new"));

        Assert.Equal("new", node.Value);
        Assert.Empty(node.ChildNames);
    }

    [Fact]
    public void Children_And_ChildNames_AreReadOnly()
    {
        var node = new RawConfigValue();
        node.SetChild("a", "1");

        var children = Assert.IsAssignableFrom<IDictionary<string, RawConfigValue>>(node.Children);
        var names = Assert.IsAssignableFrom<IList<string>>(node.ChildNames);

        Assert.Throws<NotSupportedException>(() => children.Add("b", new RawConfigValue("2")));
        Assert.Throws<NotSupportedException>(() => children.Remove("a"));
        Assert.Throws<NotSupportedException>(() => names.Add("b"));
        Assert.Throws<NotSupportedException>(() => names.RemoveAt(0));

        Assert.Equal(new[] { "a" }, node.ChildNames);
        Assert.Single(node.Children);
    }

    [Fact]
    public void Children_And_ChildNames_ReflectLaterChanges()
    {
        var node = new RawConfigValue();
        var children = node.Children;
        var names = node.ChildNames;
        Assert.Empty(names);

        node.SetChild("a", "1");

        Assert.Single(children);
        Assert.Equal(new[] { "a" }, names);
    }

    [Fact]
    public void EmptyChildName_IsRejected()
    {
        var node = new RawConfigValue();

        Assert.Throws<ArgumentException>(() => node.SetChild(string.Empty, "value"));
        Assert.Throws<ArgumentException>(() => node.GetOrAddChild(string.Empty));
    }

    [Fact]
    public void TryFind_ReportsHitAndMiss()
    {
        var node = new RawConfigValue();
        node.SetChild("a", "1");

        Assert.True(node.TryFind("a", out var found));
        Assert.Equal("1", found.Value);

        Assert.False(node.TryFind("b", out var missing));
        Assert.Null(missing);

        Assert.False(node.TryFind(null, out _));
    }

    [Fact]
    public void TryFindPath_ReportsHitAndMiss()
    {
        var node = new RawConfigValue();
        node.SetPath("A/B/C", "v");

        Assert.True(node.TryFindPath("A/B/C", out var found));
        Assert.Equal("v", found.Value);

        Assert.False(node.TryFindPath("A/B/Missing", out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void FindPath_RejectsEmptySegments()
    {
        var node = new RawConfigValue();
        node.SetPath("A/B", "v");

        Assert.Null(node.FindPath("A//B"));
        Assert.Null(node.FindPath("/A"));
        Assert.Throws<ArgumentException>(() => node.GetOrAddPath("A//B"));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("A/B")]
    [InlineData("A/B/C/D")]
    public void PathTraversal_HandlesEveryDepth(string path)
    {
        var node = new RawConfigValue();
        node.SetPath(path, "value");

        Assert.Equal("value", node.FindPath(path)!.Value);
    }
}
