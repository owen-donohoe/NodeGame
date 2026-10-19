using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyModeTests
{
    [TestCase(LobbyMode.Ranked1v1, false, LobbyPlayAction.Ranked)]
    [TestCase(LobbyMode.Ranked1v1, true, LobbyPlayAction.PrivateInvite)]
    [TestCase(LobbyMode.Team2v2, false, LobbyPlayAction.TeamChoice)]
    [TestCase(LobbyMode.Team2v2, true, LobbyPlayAction.TeamChoice)]
    [TestCase(LobbyMode.FreeForAll4, false, LobbyPlayAction.FreeForAllChoice)]
    [TestCase(LobbyMode.FreeForAll4, true, LobbyPlayAction.FreeForAllChoice)]
    [TestCase(LobbyMode.Bot, false, LobbyPlayAction.Bot)]
    [TestCase(LobbyMode.Bot, true, LobbyPlayAction.Bot)]
    [TestCase(LobbyMode.Lan, false, LobbyPlayAction.Lan)]
    [TestCase(LobbyMode.Lan, true, LobbyPlayAction.Lan)]
    public void PrivateOnlyChangesOneVsOne(LobbyMode mode, bool privateMode, LobbyPlayAction expected)
    {
        var selection = new LobbyModeSelection(mode, privateMode);
        Assert.AreEqual(expected, selection.PlayAction);
        Assert.IsNotEmpty(new HomeModeDisplay(selection).Name);
    }

    [TestCase(GameMode.OneVsOne, LobbyMode.Ranked1v1, true)]
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
    [TestCase(LobbyMode.Bot)] [TestCase(LobbyMode.Lan)]
    public void SavesNewModesWithAReadableLegacyFallback(LobbyMode mode)
    {
        var selection = new LobbyModeSelection(mode, true);
        var data = new PlayerProfileData();
        selection.Store(ref data);
        Assert.AreEqual(mode, LobbyModeSelection.FromProfile(data).Mode);
        Assert.IsTrue(LobbyModeSelection.FromProfile(data).Private);
        Assert.That(data.selectedGameModeIndex, Is.InRange(0, 3));
    }

    [Test]
    public void UnknownSavedModesFallBackToRanked()
    {
        Assert.AreEqual(LobbyMode.Ranked1v1, LobbyModeSelection.FromProfile(new PlayerProfileData
            { lobbyModeVersion = 1, selectedLobbyModeIndex = 999 }).Mode);
    }
}
