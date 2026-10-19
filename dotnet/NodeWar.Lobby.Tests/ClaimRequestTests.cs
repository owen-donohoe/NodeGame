using System;
using NodeWar.Lobby;
using NUnit.Framework;

public class ClaimRequestTests
{
    [TestCase("Premium", "50 Laurels")]
    [TestCase("premium", "50 Laurels")]
    [TestCase("Laurels", "50 Laurels")]
    [TestCase("Coins", "50 Coins")]
    [TestCase("USD cents", "50 USD cents")]
    public void PricesUseTheSharedCurrencyLabelWithoutChangingTheIdentifier(string kind, string label)
    {
        var action = ClaimAction.Price(kind, 50);
        Assert.AreEqual(label, action.PrimaryLabel());
        Assert.AreEqual(kind, action.CurrencyKind);
        Assert.AreEqual("Laurels", LobbyCurrencyNames.Premium);
    }

    [Test]
    public void LabelsAndOptionalDisclosure()
    {
        Assert.AreEqual("OK", new ClaimRequest("Title", "Description", ClaimAction.Ok).PrimaryLabel());
        Assert.AreEqual("Claim", ClaimAction.Claim.PrimaryLabel());
        Assert.AreEqual("50 Laurels", ClaimAction.Price("Laurels", 50).PrimaryLabel());
        Assert.AreEqual("0 Coins", ClaimAction.Price("Coins", 0).PrimaryLabel());
        Assert.IsNull(new ClaimRequest(null, null, ClaimAction.Ok).Disclosure);
    }

    [Test]
    public void DisclosureIsPlainTextAndCopied()
    {
        string[] lines = { "Rare: 10%", "<b>Common: 90%</b>" };
        var request = new ClaimRequest("Crate", "Description", ClaimAction.Claim, lines);
        lines[0] = "Changed";
        Assert.AreEqual("Description", request.Description);
        Assert.AreEqual("Crate", request.Title);
        CollectionAssert.AreEqual(new[] { "Rare: 10%", "<b>Common: 90%</b>" }, request.Disclosure);
    }

    [Test]
    public void InvalidPriceIsRejected()
    {
        Assert.Throws<ArgumentException>(() => ClaimAction.Price("", 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClaimAction.Price("Coins", -1));
    }
}
