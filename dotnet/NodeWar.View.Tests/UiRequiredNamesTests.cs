using System.IO;
using System.Linq;
using System.Xml.Linq;
using NodeWar.UI;
using NUnit.Framework;

public class UiRequiredNamesTests
{
    [TestCase("GameplayHUD.uxml", "GameplayHud")]
    [TestCase("SettingsPage.uxml", "Settings")]
    [TestCase("GameplayHUD.uxml", "MatchSettings")]
    public void BehaviourBearingNamesExistInAuthoredLayout(string file, string screen)
    {
        string[] required = screen == "GameplayHud" ? UiRequiredNames.GameplayHud.All
            : screen == "Settings" ? UiRequiredNames.Settings.All : UiRequiredNames.MatchSettings.All;
        var document = XDocument.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Layouts", file)));
        var authored = document.Descendants().Attributes("name").Select(a => a.Value).ToArray();
        Assert.That(required, Is.Unique, "Required names must be unambiguous.");
        foreach (string name in required)
            Assert.That(authored.Count(n => n == name), Is.EqualTo(1), screen + " requires exactly one '" + name + "' in " + file);
    }
}
