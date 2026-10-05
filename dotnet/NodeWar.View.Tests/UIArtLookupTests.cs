using NodeWar.Lobby;
using NUnit.Framework;

public class UIArtLookupTests
{
    [Test]
    public void ExactContextBeatsAnywhereEvenWhenAnywhereAppearsFirst()
    {
        var entries = new[] { new Entry(1, true), new Entry(1, true, LobbyIconContext.TopBar) };
        Assert.AreEqual(1, Resolve(entries, 1, LobbyIconContext.TopBar));
    }

    [Test]
    public void AnywhereCoversEveryContext()
    {
        foreach (LobbyIconContext context in System.Enum.GetValues(typeof(LobbyIconContext)))
            Assert.AreEqual(0, Resolve(new[] { new Entry(1, true) }, 1, context), context.ToString());
    }

    [Test]
    public void OtherContextDoesNotMatchOrOverrideAnywhere()
    {
        var specific = new Entry(1, true, LobbyIconContext.TopBar);
        Assert.AreEqual(-1, Resolve(new[] { specific }, 1, LobbyIconContext.PageHeader));
        Assert.AreEqual(-1, Resolve(new[] { specific }, 1));
        Assert.AreEqual(1, Resolve(new[] { specific, new Entry(1, true) }, 1, LobbyIconContext.PageHeader));
    }

    [Test]
    public void FirstDuplicateWinsWithinEachPairIndependently()
    {
        var entries = new[] { new Entry(1, true), new Entry(1, true, LobbyIconContext.TopBar),
            new Entry(1, true, LobbyIconContext.TopBar), new Entry(1, true) };
        Assert.AreEqual(1, Resolve(entries, 1, LobbyIconContext.TopBar));
        Assert.AreEqual(0, Resolve(entries, 1, LobbyIconContext.PageHeader));
    }

    [Test]
    public void EmptyFirstExactFallsBackToAnywhereWithoutUsingLaterExactDuplicate()
    {
        var entries = new[] { new Entry(1, false, LobbyIconContext.TopBar),
            new Entry(1, true, LobbyIconContext.TopBar), new Entry(1, true) };
        Assert.AreEqual(2, Resolve(entries, 1, LobbyIconContext.TopBar));
        Assert.AreEqual(-1, Resolve(new[] { entries[0], entries[1] }, 1, LobbyIconContext.TopBar));
    }

    [Test]
    public void MissingSerializedContextUsesAnywhereZero()
    {
        Assert.AreEqual(0, (int)LobbyIconContext.Anywhere);
        Assert.AreEqual(LobbyIconContext.Anywhere, default(Entry).Context);
        Assert.AreEqual(LobbyIconContext.Anywhere, default(LobbyIconContext));
    }

    private struct Entry
    {
        public int Kind;
        public bool HasSprite;
        public LobbyIconContext Context;
        public Entry(int kind, bool hasSprite, LobbyIconContext context = LobbyIconContext.Anywhere)
        { Kind = kind; HasSprite = hasSprite; Context = context; }
    }

    private static int Resolve(Entry[] entries, int kind, LobbyIconContext context = LobbyIconContext.Anywhere) =>
        UIArtLookup.SpriteIndex(entries, kind, context, e => e.Kind, e => e.Context, e => e.HasSprite);

    [Test]
    public void AuthoredSpriteWinsOverGeneratedFallback()
    {
        Assert.AreEqual(1, Resolve(new[] { new Entry(2, true), new Entry(1, true) }, 1));
    }

    [Test]
    public void MissingThemeEntryOrSpriteUsesGeneratedArt()
    {
        Assert.AreEqual(-1, Resolve(null, 1));
        Assert.AreEqual(-1, Resolve(new Entry[0], 1));
        Assert.AreEqual(-1, Resolve(new[] { new Entry(2, true) }, 1));
        Assert.AreEqual(-1, Resolve(new[] { new Entry(1, false) }, 1));
    }

    [Test]
    public void FirstDuplicateWins()
    {
        Assert.AreEqual(0, Resolve(new[] { new Entry(1, true), new Entry(1, true) }, 1));
    }

    [Test]
    public void EmptyFirstDuplicateStillWinsAndUsesGeneratedArt()
    {
        Assert.AreEqual(-1, Resolve(new[] { new Entry(1, false), new Entry(1, true) }, 1));
    }
}
