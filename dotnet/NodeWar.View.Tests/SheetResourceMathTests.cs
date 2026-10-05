using NUnit.Framework;
using NodeWar.UI;

public class SheetResourceMathTests
{
    [TestCase(ResourceKind.Food, ResourceKind.Food, true, true)]
    [TestCase(ResourceKind.Materials, ResourceKind.Materials | ResourceKind.Metal, true, true)]
    [TestCase(ResourceKind.Metal, ResourceKind.Materials | ResourceKind.Metal, true, true)]
    [TestCase(ResourceKind.Food, ResourceKind.Materials | ResourceKind.Metal, true, false)]
    [TestCase(ResourceKind.Metal, ResourceKind.Metal, false, false)]
    [TestCase(ResourceKind.Magic, ResourceKind.Magic, true, true)]
    [TestCase(ResourceKind.Magic, ResourceKind.Food, true, false)]
    [TestCase(ResourceKind.None, ResourceKind.Food, true, false)]
    public void OnlyVisibleInvolvedResourcesExpand(ResourceKind resource, ResourceKind involved, bool visible, bool expected)
    {
        Assert.AreEqual(expected, SheetResourceMath.Expanded(resource, involved, visible));
    }

    [TestCase(4, 5, true, false, false, true)]
    [TestCase(5, 4, true, false, false, true)]
    [TestCase(5, 5, true, false, false, false)]
    [TestCase(0, 5, false, false, false, false)]
    [TestCase(4, 5, true, true, false, false)]
    [TestCase(4, 5, true, false, true, false)]
    public void BounceForGainsAndSpendsButNotInitializationOrViewerSwitch(int before, int after, bool hasValue,
        bool reduced, bool viewerChanged, bool expected)
    {
        Assert.AreEqual(expected, SheetResourceMath.Bounce(before, after, hasValue, reduced, viewerChanged));
    }
}
