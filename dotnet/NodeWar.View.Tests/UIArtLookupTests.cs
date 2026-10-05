using NodeWar.Lobby;
using NUnit.Framework;

public class UIArtLookupTests
{
    private struct Entry
    {
        public int Kind;
        public bool HasSprite;
        public Entry(int kind, bool hasSprite) { Kind = kind; HasSprite = hasSprite; }
    }

    private static int Resolve(Entry[] entries, int kind) =>
        UIArtLookup.SpriteIndex(entries, kind, e => e.Kind, e => e.HasSprite);

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
