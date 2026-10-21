using System;
using System.Linq;
using NodeWar.Lobby;
using NUnit.Framework;

public class ModeDefinitionTests
{
    [Test]
    public void TableCoversStableIdsAndRejectsDuplicateOrMissingRows()
    {
        Assert.DoesNotThrow(() => ModeDefinition.Validate(ModeDefinition.All));
        Assert.AreEqual(5, (int)LobbyMode.Unranked1v1);
        Assert.Throws<ArgumentException>(() => ModeDefinition.Validate(ModeDefinition.All.Skip(1).ToArray()));
        var duplicate = ModeDefinition.All.ToArray(); duplicate[1] = duplicate[0];
        Assert.Throws<ArgumentException>(() => ModeDefinition.Validate(duplicate));
    }

    [TestCase(false, LobbyMode.Ranked1v1, ModeLaunchStrategy.Queue)]
    [TestCase(true, LobbyMode.Unranked1v1, ModeLaunchStrategy.Private)]
    public void SavedUnrankedIsGatedExceptForPrivateRoom(bool isPrivate, LobbyMode expected, ModeLaunchStrategy route)
    {
        var data = new PlayerProfileData { lobbyModeVersion = 1, selectedLobbyModeIndex = 5, privateLobbyMode = isPrivate };
        var selected = LobbyModeSelection.FromProfile(data);
        Assert.AreEqual(expected, selected.Mode); Assert.AreEqual(route, selected.LaunchStrategy);
        var definition = ModeDefinition.For(LobbyMode.Unranked1v1);
        Assert.AreEqual(ModeAvailability.ComingSoon, definition.Availability);
        Assert.AreEqual("Coming soon", definition.DisabledReason(false));
        Assert.IsFalse(definition.CanSelect(false)); Assert.IsTrue(definition.CanSelect(true));
        CollectionAssert.Contains(LobbyModeCategories.ModesIn(LobbyModeCategory.Unranked), LobbyMode.Unranked1v1);
    }

    [TestCase(LobbyMode.Bot)] [TestCase(LobbyMode.Lan)] [TestCase(LobbyMode.Ranked1v1)]
    public void InvalidPrivateCardsAreDisabledAndSavedCombinationsBecomePrivateDuel(LobbyMode mode)
    {
        var selected = LobbyModeSelection.FromProfile(new PlayerProfileData {
            lobbyModeVersion = 1, selectedLobbyModeIndex = (int)mode, privateLobbyMode = true });
        Assert.AreEqual(LobbyMode.Unranked1v1, selected.Mode);
        Assert.AreEqual(ModeLaunchStrategy.Private, selected.LaunchStrategy);
        Assert.AreEqual("Off in private", ModeDefinition.For(mode).DisabledReason(true));
        Assert.IsFalse(ModeDefinition.For(mode).CanSelect(true));
    }

    [TestCase(LobbyMode.Lan, ModeLaunchStrategy.Lan)]
    [TestCase(LobbyMode.Bot, ModeLaunchStrategy.Bot)]
    [TestCase(LobbyMode.Ranked1v1, ModeLaunchStrategy.Queue)]
    public void NonPrivateRoutingUsesTheTable(LobbyMode mode, ModeLaunchStrategy expected)
    {
        Assert.AreEqual(expected, new LobbyModeSelection(mode).LaunchStrategy);
    }

    [Test]
    public void PrivateModelAndHomeUseTableLayoutAndNames()
    {
        foreach (var row in ModeDefinition.All)
        {
            if (!row.CanSelect(true)) continue;
            var selected = new LobbyModeSelection(row.Id, true);
            Assert.AreEqual(row.DisplayName, new HomeModeDisplay(selected).Name);
            var model = new PrivateLobbyModel(row.Id, "host", "Host");
            Assert.AreEqual(row.SeatCount, model.Capacity);
            for (int seat = 0; seat < row.SeatCount; seat++)
            { Assert.AreEqual(row.Team(seat), model.Colour(seat)); Assert.AreEqual(row.Position(seat), model.Position(seat)); }
        }
    }
}
