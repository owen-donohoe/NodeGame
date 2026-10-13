using System.IO;
using System.Linq;
using System.Xml.Linq;
using NodeWar.UI;
using NUnit.Framework;

public class UiRequiredNamesTests
{
    [Test]
    public void SelectionCounterHasOneCenteredNonPickingDock()
    {
        var document = XDocument.Parse(File.ReadAllText(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "Layouts", "GameplayHUD.uxml")));
        var docks = document.Descendants().Where(e => (string)e.Attribute("name") == "hud-selbar-dock").ToArray();
        Assert.That(docks, Has.Length.EqualTo(1));
        var dock = docks.Single();
        Assert.That((string)dock.Attribute("picking-mode"), Is.EqualTo("Ignore"));
        Assert.That(((string)dock.Attribute("class")).Split(' '), Does.Contain("hud__selbar-dock"));

        var counters = document.Descendants().Where(e => (string)e.Attribute("name") == "hud-selbar").ToArray();
        Assert.That(counters, Has.Length.EqualTo(1));
        var counter = counters.Single();
        Assert.That(counter.Parent, Is.SameAs(dock), "The counter must stay in the dock's flow.");
        Assert.That((string)counter.Attribute("picking-mode"), Is.EqualTo("Position"));

        var counts = document.Descendants().Where(e => (string)e.Attribute("name") == "hud-selbar-count").ToArray();
        Assert.That(counts, Has.Length.EqualTo(1));
        Assert.That(counts.Single().Parent, Is.SameAs(counter));
        Assert.That((string)counts.Single().Attribute("picking-mode"), Is.EqualTo("Ignore"));
        // XML guards the authored hierarchy; USS centering and touch delivery need Unity checks.
    }

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
