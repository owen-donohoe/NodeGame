using System;
using System.Linq;
using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyIconUsageTests
{
    [TestCase(LobbyIconKind.Burger, LobbyIconContext.TopBar)]
    [TestCase(LobbyIconKind.Bell, LobbyIconContext.TopBar)]
    [TestCase(LobbyIconKind.Funnel, LobbyIconContext.PageHeader)]
    [TestCase(LobbyIconKind.Swap, LobbyIconContext.Home)]
    [TestCase(LobbyIconKind.CrateSmall, LobbyIconContext.Home)]
    [TestCase(LobbyIconKind.CrateMedium, LobbyIconContext.ShopCard)]
    [TestCase(LobbyIconKind.Info, LobbyIconContext.PageHeader)]
    [TestCase(LobbyIconKind.Spectate, LobbyIconContext.Profile)]
    [TestCase(LobbyIconKind.Chat, LobbyIconContext.PageHeader)]
    [TestCase(LobbyIconKind.Guild, LobbyIconContext.NavBar)]
    [TestCase(LobbyIconKind.Refresh, LobbyIconContext.PageHeader)]
    [TestCase(LobbyIconKind.AddFriend, LobbyIconContext.Profile)]
    [TestCase(LobbyIconKind.Invite, LobbyIconContext.Home)]
    [TestCase(LobbyIconKind.Search, LobbyIconContext.PageHeader)]
    [TestCase(LobbyIconKind.Laurel, LobbyIconContext.TopBar)]
    public void RedesignKindsHaveSemanticLocations(LobbyIconKind kind, LobbyIconContext context)
    {
        Assert.IsTrue(LobbyIconUsage.IsUsed(kind, context));
        Assert.IsFalse(LobbyIconUsage.For(kind).Retired);
    }

    private static readonly LobbyIconKind[] Retired =
    {
        LobbyIconKind.Shop, LobbyIconKind.Spark, LobbyIconKind.Smile, LobbyIconKind.Envelope,
        LobbyIconKind.Flag, LobbyIconKind.Hat, LobbyIconKind.Diamond, LobbyIconKind.Pip,
        LobbyIconKind.Alert, LobbyIconKind.Capture,
        LobbyIconKind.SuitTreeAvailable, LobbyIconKind.SuitTreeOwned, LobbyIconKind.SuitTreeEquipped, LobbyIconKind.SuitTreeLocked
    };

    [Test]
    public void EveryKindHasOneMetadataEntryAndEveryActiveKindHasLocations()
    {
        foreach (LobbyIconKind kind in Enum.GetValues(typeof(LobbyIconKind)))
        {
            Assert.AreEqual(1, LobbyIconUsage.All.Count(i => i.Kind == kind), kind.ToString());
            if (kind != LobbyIconKind.None && !Retired.Contains(kind))
                Assert.That(LobbyIconUsage.For(kind).Locations.Count, Is.GreaterThan(0), kind.ToString());
        }
        Assert.That(LobbyIconUsage.All.All(i => Enum.IsDefined(typeof(LobbyIconKind), i.Kind)), Is.True);
    }

    [Test]
    public void RetiredSetIsExactlyTheUnusedGenericAndTreeKinds()
    {
        var retired = LobbyIconUsage.All.Where(i => i.Retired).ToArray();
        CollectionAssert.AreEquivalent(Retired, retired.Select(i => i.Kind));
        Assert.That(retired.All(i => i.Locations.Count == 0), Is.True);
    }

    [Test]
    public void EveryUsageContextIsValidSpecificAndUniqueWithinItsKind()
    {
        foreach (var icon in LobbyIconUsage.All)
        {
            foreach (var location in icon.Locations)
            {
                Assert.That(Enum.IsDefined(typeof(LobbyIconContext), location.Context), Is.True, icon.Path);
                Assert.That(location.Context, Is.Not.EqualTo(LobbyIconContext.Anywhere), icon.Path);
            }
            Assert.That(icon.Locations.Select(l => l.Context), Is.Unique, icon.Path);
        }
    }

    [Test]
    public void LocationLabelsAreNonEmptyAndUniquePerKind()
    {
        foreach (var icon in LobbyIconUsage.All)
        {
            Assert.That(icon.Locations.All(l => !string.IsNullOrWhiteSpace(l.Label)), Is.True, icon.Path);
            Assert.AreEqual(icon.Locations.Count,
                icon.Locations.Select(l => l.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count(), icon.Path);
        }
    }

    [Test]
    public void KindNamesAndGroupsAreNonEmptyWithUniqueMenuPaths()
    {
        foreach (var icon in LobbyIconUsage.All)
        {
            Assert.That(string.IsNullOrWhiteSpace(icon.Group), Is.False);
            Assert.That(string.IsNullOrWhiteSpace(icon.FriendlyName), Is.False);
        }
        Assert.That(LobbyIconUsage.All.Select(i => i.Path), Is.Unique);
    }

    [TestCase(LobbyIconKind.NavBarHome)]
    [TestCase(LobbyIconKind.Gear)]
    [TestCase(LobbyIconKind.Swords)]
    public void SingleLocationKindsOfferOnlyAnywhere(LobbyIconKind kind)
    {
        var choices = LobbyIconUsage.WhereChoices(kind);
        Assert.AreEqual(1, choices.Length);
        Assert.AreEqual(LobbyIconContext.Anywhere, choices[0].Context);
        Assert.AreEqual("Anywhere", choices[0].Label);
        Assert.That(LobbyIconUsage.IsValidThemePair(kind, LobbyIconUsage.For(kind).Locations[0].Context), Is.True,
            "Previously saved specific overrides remain valid");
    }

    [Test]
    public void MultipleLocationChoicesStartWithAnywhereThenOnlyActualLocations()
    {
        var choices = LobbyIconUsage.WhereChoices(LobbyIconKind.Food);
        Assert.AreEqual(LobbyIconContext.Anywhere, choices[0].Context);
        CollectionAssert.AreEqual(LobbyIconUsage.For(LobbyIconKind.Food).Locations.Select(l => l.Context),
            choices.Skip(1).Select(l => l.Context));
        Assert.That(choices.Select(l => l.Label), Is.Unique);
    }

    [Test]
    public void ThemeWildcardIsValidButIsNotAnActualRuntimeLocation()
    {
        Assert.That(LobbyIconUsage.IsValidThemePair(LobbyIconKind.Tv, LobbyIconContext.Anywhere), Is.True);
        Assert.That(LobbyIconUsage.IsUsed(LobbyIconKind.Tv, LobbyIconContext.Anywhere), Is.False);
        Assert.That(LobbyIconUsage.IsUsed(LobbyIconKind.Tv, LobbyIconContext.PageHeader), Is.True);
    }

    [Test]
    public void IncompatibleLocationIsRejectedWhenChangingKind()
    {
        Assert.That(LobbyIconUsage.IsValidThemePair(LobbyIconKind.Food, LobbyIconContext.EmoteBubble), Is.False);
        Assert.That(LobbyIconUsage.IsValidThemePair(LobbyIconKind.EmoteHappy, LobbyIconContext.EmoteBubble), Is.True);
        Assert.That(LobbyIconUsage.IsValidThemePair(LobbyIconKind.Food, LobbyIconContext.Anywhere), Is.True);
    }

    [Test]
    public void RetiredNoneAndUnknownPairsHaveNoActiveMatch()
    {
        foreach (var kind in Retired.Concat(new[] { LobbyIconKind.None, (LobbyIconKind)999 }))
            Assert.That(LobbyIconUsage.IsValidThemePair(kind, LobbyIconContext.Anywhere), Is.False, kind.ToString());
        Assert.That(LobbyIconUsage.IsUsed(LobbyIconKind.Food, (LobbyIconContext)999), Is.False);
    }
}
