using System;
using NodeWar.Lobby;
using NUnit.Framework;

public class ClaimRequestTests
{
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
