using System;
using System.Linq;
using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyModeCategoryTests
{
    [TestCase(LobbyMode.Ranked1v1, false, LobbyModeCategory.Ranked)]
    [TestCase(LobbyMode.Ranked1v1, true, LobbyModeCategory.Other)]
    [TestCase(LobbyMode.Team2v2, false, LobbyModeCategory.Unranked)]
    [TestCase(LobbyMode.FreeForAll4, false, LobbyModeCategory.Unranked)]
    [TestCase(LobbyMode.Bot, false, LobbyModeCategory.Other)]
    [TestCase(LobbyMode.Lan, false, LobbyModeCategory.Other)]
    public void MapsSelectionsToCategories(LobbyMode mode, bool isPrivate, LobbyModeCategory expected)
    {
        Assert.AreEqual(expected, LobbyModeCategories.Of(new LobbyModeSelection(mode, isPrivate)));
    }

    [Test]
    public void EveryModeAppearsInExactlyOneCategoryAndAgreesWithTheSelectionMapping()
    {
        foreach (LobbyMode mode in Enum.GetValues(typeof(LobbyMode)))
        {
            var homes = LobbyModeCategories.All.Where(c => LobbyModeCategories.ModesIn(c).Contains(mode)).ToArray();
            Assert.AreEqual(1, homes.Length, mode.ToString());
            Assert.AreEqual(homes[0], LobbyModeCategories.Of(new LobbyModeSelection(mode)), mode.ToString());
        }
    }

    [Test]
    public void CategoriesAreOrderedRankedUnrankedOther()
    {
        CollectionAssert.AreEqual(new[] { LobbyModeCategory.Ranked, LobbyModeCategory.Unranked, LobbyModeCategory.Other }, LobbyModeCategories.All);
        Assert.AreEqual(2, LobbyModeCategories.IndexOf(LobbyModeCategory.Other));
    }
}
