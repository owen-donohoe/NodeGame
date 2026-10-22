using NodeWar.Lobby;
using NUnit.Framework;

public class HomeModeDisplayTests
{
    [TestCase(GameMode.Locked, "1v1 Ranked", LobbyIconKind.Swords)]
    [TestCase(GameMode.OneVsOne, "1v1 Private", LobbyIconKind.Invite)]
    [TestCase(GameMode.Bot, "Bot", LobbyIconKind.Suit)]
    [TestCase(GameMode.Testing, "Testing", LobbyIconKind.Tools)]
    [TestCase((GameMode)999, "1v1 Ranked", LobbyIconKind.Swords)]
    public void SelectedModesHaveReadableLabelsAndSemanticIcons(GameMode mode, string name, LobbyIconKind icon)
    {
        var display = new HomeModeDisplay(mode);
        Assert.AreEqual(name, display.Name);
        Assert.AreEqual(icon, display.Icon);
        StringAssert.IsMatch("^#[0-9A-F]{6}$", display.Colour);
    }
}
