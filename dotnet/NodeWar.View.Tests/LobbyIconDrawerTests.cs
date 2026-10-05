using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyIconDrawerTests
{
    private static string Source => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
        "Sources", "LobbyIcon.cs.txt"));

    // Check the actual painter dispatch, rather than a second list of supported kinds.
    private static Dictionary<string, string> Drawers()
    {
        var drawers = new Dictionary<string, string>();
        foreach (Match group in Regex.Matches(Source,
            @"(?<cases>(?:case LobbyIconKind\.\w+:\s*)+)(?<draw>[^\r\n]+)"))
            foreach (Match label in Regex.Matches(group.Groups["cases"].Value, @"LobbyIconKind\.(\w+)"))
                drawers.Add(label.Groups[1].Value, group.Groups["draw"].Value.Trim());
        return drawers;
    }

    [Test]
    public void EveryKindHasExplicitDrawingOrIntentionalNone()
    {
        var drawers = Drawers();
        StringAssert.Contains("if (kind == LobbyIconKind.None) return null;", Source);
        foreach (LobbyIconKind kind in Enum.GetValues(typeof(LobbyIconKind)))
            if (kind != LobbyIconKind.None)
            {
                Assert.That(drawers.ContainsKey(kind.ToString()), Is.True, kind + " must not fall to the placeholder");
                StringAssert.Contains("break;", drawers[kind.ToString()]);
            }
    }

    [Test]
    public void PersistedLegacyKindValuesStayInTheirOriginalOrder()
    {
        string[] original = { "None", "Shop", "Spark", "Tools", "Smile", "Gear", "Envelope", "Mouth",
            "Tv", "Back", "Flag", "Hat", "Diamond", "District", "Suit", "Lock", "Pip", "Close",
            "Alert", "Swords", "Capture", "Sleep", "Respawn", "Pointer", "Frown", "Angry", "Speaker",
            "Food", "Materials", "Metal" };
        for (int i = 0; i < original.Length; i++)
            Assert.AreEqual(original[i], Enum.GetName(typeof(LobbyIconKind), i), "Persisted value " + i);
    }

    [TestCase(LobbyIconKind.NavBarHome, LobbyIconKind.Spark)]
    [TestCase(LobbyIconKind.NavBarSocial, LobbyIconKind.Smile)]
    [TestCase(LobbyIconKind.NavBarShop, LobbyIconKind.Shop)]
    [TestCase(LobbyIconKind.DailyBox, LobbyIconKind.Spark)]
    [TestCase(LobbyIconKind.VictoryBox, LobbyIconKind.Envelope)]
    [TestCase(LobbyIconKind.ShopBundle, LobbyIconKind.Envelope)]
    [TestCase(LobbyIconKind.GoldLeaf, LobbyIconKind.Diamond)]
    [TestCase(LobbyIconKind.MagicResource, LobbyIconKind.Spark)]
    [TestCase(LobbyIconKind.SuitTreeAvailable, LobbyIconKind.Spark)]
    [TestCase(LobbyIconKind.SuitTreeOwned, LobbyIconKind.Pip)]
    [TestCase(LobbyIconKind.SuitTreeEquipped, LobbyIconKind.Diamond)]
    [TestCase(LobbyIconKind.SuitTreeLocked, LobbyIconKind.Lock)]
    [TestCase(LobbyIconKind.ProfileYouAreHere, LobbyIconKind.Pip)]
    [TestCase(LobbyIconKind.IndicatorEffect, LobbyIconKind.Spark)]
    [TestCase(LobbyIconKind.IndicatorThreatToCore, LobbyIconKind.Alert)]
    [TestCase(LobbyIconKind.IndicatorThreatToTerritory, LobbyIconKind.Alert)]
    [TestCase(LobbyIconKind.IndicatorNodeUnderAttack, LobbyIconKind.Capture)]
    [TestCase(LobbyIconKind.IndicatorNodeContested, LobbyIconKind.Capture)]
    [TestCase(LobbyIconKind.EmoteHappy, LobbyIconKind.Smile)]
    [TestCase(LobbyIconKind.EmoteWhiteFlag, LobbyIconKind.Flag)]
    [TestCase(LobbyIconKind.CosmeticTinRoof, LobbyIconKind.Shop)]
    [TestCase(LobbyIconKind.CosmeticPaperBanner, LobbyIconKind.Flag)]
    [TestCase(LobbyIconKind.CosmeticStrawHat, LobbyIconKind.Hat)]
    public void SplitKindUsesExactlyItsOriginalDrawing(LobbyIconKind split, LobbyIconKind original)
    {
        var drawers = Drawers();
        Assert.AreEqual(drawers[original.ToString()], drawers[split.ToString()]);
    }
}
