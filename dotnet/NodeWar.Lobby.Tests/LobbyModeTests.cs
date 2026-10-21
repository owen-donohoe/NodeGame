using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyModeTests
{
    [TestCase(LobbyMode.Ranked1v1, false, ModeLaunchStrategy.Queue)]
    [TestCase(LobbyMode.Ranked1v1, true, ModeLaunchStrategy.Private)]
    [TestCase(LobbyMode.Team2v2, false, ModeLaunchStrategy.Queue)]
    [TestCase(LobbyMode.Team2v2, true, ModeLaunchStrategy.Private)]
    [TestCase(LobbyMode.FreeForAll4, false, ModeLaunchStrategy.Queue)]
    [TestCase(LobbyMode.FreeForAll4, true, ModeLaunchStrategy.Private)]
    [TestCase(LobbyMode.Bot, false, ModeLaunchStrategy.Bot)]
    [TestCase(LobbyMode.Bot, true, ModeLaunchStrategy.Private)]
    [TestCase(LobbyMode.Lan, false, ModeLaunchStrategy.Lan)]
    [TestCase(LobbyMode.Lan, true, ModeLaunchStrategy.Private)]
    [TestCase(LobbyMode.Unranked1v1, false, ModeLaunchStrategy.Queue)]
    [TestCase(LobbyMode.Unranked1v1, true, ModeLaunchStrategy.Private)]
    public void RoutesNormalizedSelectionThroughModeCapabilities(LobbyMode mode, bool privateMode, ModeLaunchStrategy expected)
    {
        var selection = new LobbyModeSelection(mode, privateMode);
        Assert.AreEqual(expected, selection.LaunchStrategy);
        Assert.IsNotEmpty(new HomeModeDisplay(selection).Name);
    }

    [TestCase(GameMode.OneVsOne, LobbyMode.Unranked1v1, true)]
    [TestCase(GameMode.Bot, LobbyMode.Bot, false)]
    [TestCase(GameMode.Locked, LobbyMode.Ranked1v1, false)]
    [TestCase(GameMode.Testing, LobbyMode.Ranked1v1, false)]
    public void ReadsLegacySelectionsWithoutReusingTheirValues(GameMode legacy, LobbyMode mode, bool privateMode)
    {
        var data = new PlayerProfileData { selectedGameModeIndex = (int)legacy };
        var selection = LobbyModeSelection.FromProfile(data);
        Assert.AreEqual(mode, selection.Mode);
        Assert.AreEqual(privateMode, selection.Private);
        Assert.AreEqual((int)legacy, data.selectedGameModeIndex);
    }

    [TestCase(LobbyMode.Ranked1v1)] [TestCase(LobbyMode.Team2v2)] [TestCase(LobbyMode.FreeForAll4)]
    [TestCase(LobbyMode.Bot)] [TestCase(LobbyMode.Lan)] [TestCase(LobbyMode.Unranked1v1)]
    public void SavesNewModesWithAReadableLegacyFallback(LobbyMode mode)
    {
        var selection = new LobbyModeSelection(mode, true);
        var data = new PlayerProfileData();
        selection.Store(ref data);
        Assert.AreEqual(mode == LobbyMode.Ranked1v1 || mode == LobbyMode.Bot || mode == LobbyMode.Lan ? LobbyMode.Unranked1v1 : mode, LobbyModeSelection.FromProfile(data).Mode);
        Assert.IsTrue(LobbyModeSelection.FromProfile(data).Private);
        Assert.That(data.selectedGameModeIndex, Is.InRange(0, 3));
    }

    [Test]
    public void UnknownSavedModesFallBackToRanked()
    {
        Assert.AreEqual(LobbyMode.Ranked1v1, LobbyModeSelection.FromProfile(new PlayerProfileData
            { lobbyModeVersion = 1, selectedLobbyModeIndex = 999 }).Mode);
    }

    [Test]
    public void PrivateRankedPreferencesMigrateAndFallBackToRankedWhenPrivateTurnsOff()
    {
        var privateMode = LobbyModeSelection.FromProfile(new PlayerProfileData
            { lobbyModeVersion = 1, selectedLobbyModeIndex = 0, privateLobbyMode = true });
        Assert.AreEqual(LobbyMode.Unranked1v1, privateMode.Mode);
        Assert.AreEqual(LobbyMode.Ranked1v1, new LobbyModeSelection(privateMode.Mode, false).Mode);
        Assert.AreEqual("1v1", new HomeModeDisplay(privateMode).Name);
        Assert.AreEqual(5, (int)LobbyMode.Unranked1v1, "Existing persisted mode IDs must not move");
    }
}
