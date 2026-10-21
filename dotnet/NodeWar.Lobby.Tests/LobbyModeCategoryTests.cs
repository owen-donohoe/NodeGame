using System;
using System.Linq;
using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyModeCategoryTests
{
    [TestCase(LobbyMode.Ranked1v1, false, LobbyModeCategory.Ranked)]
    [TestCase(LobbyMode.Ranked1v1, true, LobbyModeCategory.Unranked)]
    [TestCase(LobbyMode.Unranked1v1, false, LobbyModeCategory.Ranked)]
    [TestCase(LobbyMode.Unranked1v1, true, LobbyModeCategory.Unranked)]
    [TestCase(LobbyMode.Team2v2, true, LobbyModeCategory.Unranked)]
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
            Assert.AreEqual(homes[0], ModeDefinition.For(mode).Category, mode.ToString());
        }
    }

    [Test]
    public void CategoriesAreOrderedRankedUnrankedOther()
    {
        CollectionAssert.AreEqual(new[] { LobbyModeCategory.Ranked, LobbyModeCategory.Unranked, LobbyModeCategory.Other }, LobbyModeCategories.All);
        Assert.AreEqual(2, LobbyModeCategories.IndexOf(LobbyModeCategory.Other));
    }

    [TestCase(LobbyModeCategory.Ranked, 1, 1)]
    [TestCase(LobbyModeCategory.Unranked, 2, 1)]
    [TestCase(LobbyModeCategory.Other, 1, 0)]
    public void GroupsHaveTwoRowsAndExplicitEmptySlots(LobbyModeCategory category, int columns, int emptySlots)
    {
        var modes = LobbyModeCategories.ModesIn(category);
        Assert.AreEqual(columns, LobbyModeLayout.Columns(modes.Length));
        Assert.AreEqual(columns * 2, LobbyModeLayout.Slots(modes.Length));
        int empty = 0;
        for (int c = 0; c < columns; c++)
            for (int r = 0; r < 2; r++)
                if (LobbyModeLayout.IsEmpty(modes.Length, c, r)) empty++;
        Assert.AreEqual(emptySlots, empty);
    }

    [Test]
    public void UnrankedColumnsFillTopThenBottomBeforeNextColumn()
    {
        var modes = LobbyModeCategories.ModesIn(LobbyModeCategory.Unranked);
        Assert.AreEqual(LobbyMode.Unranked1v1, modes[LobbyModeLayout.ModeIndex(0, 0)]);
        Assert.AreEqual(LobbyMode.Team2v2, modes[LobbyModeLayout.ModeIndex(0, 1)]);
        Assert.AreEqual(LobbyMode.FreeForAll4, modes[LobbyModeLayout.ModeIndex(1, 0)]);
        Assert.IsTrue(LobbyModeLayout.IsEmpty(modes.Length, 1, 1));
        Assert.IsTrue(LobbyModeLayout.IsEmpty(1, 0, 1));
    }

    [Test]
    public void GroupStartsIncludeColumnAndGroupGapsAndSnapToNearestStart()
    {
        Assert.AreEqual(160, LobbyModeLayout.GroupWidth(1, 160, 12));
        Assert.AreEqual(332, LobbyModeLayout.GroupWidth(3, 160, 12));
        Assert.AreEqual(184, LobbyModeLayout.GroupStart(1, 160, 12, 24));
        Assert.AreEqual(540, LobbyModeLayout.GroupStart(2, 160, 12, 24));
        Assert.AreEqual(0, LobbyModeLayout.NearestGroup(-20, 160, 12, 24));
        Assert.AreEqual(0, LobbyModeLayout.NearestGroup(92, 160, 12, 24), "Ties prefer the earlier group");
        Assert.AreEqual(1, LobbyModeLayout.NearestGroup(93, 160, 12, 24));
        Assert.AreEqual(2, LobbyModeLayout.NearestGroup(800, 160, 12, 24));
    }
}
