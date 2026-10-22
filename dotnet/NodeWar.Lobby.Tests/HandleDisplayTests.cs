using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class HandleDisplayTests
    {
        [Test]
        public void DisplayUsesServerHandle()
        {
            Assert.That(HandleDisplay.Display(new ProfileRecord { Name = "Node_War", Tag = "A7" }, "local"),
                Is.EqualTo("Node_War#A7"));
        }

        [TestCase("local")]
        [TestCase("")]
        [TestCase(null)]
        public void DisplayWithoutProfileKeepsFallback(string fallback)
        {
            Assert.That(HandleDisplay.Display(null, fallback), Is.EqualTo(fallback));
        }

        [TestCase("1234567890123456#ABCD", "1234567890123456#ABCD")]
        [TestCase("12345678901234567#ABCD", "1234567890123456\u2026#ABCD")]
        [TestCase("12345678901234567", "1234567890123456\u2026")]
        [TestCase("Short#LONGTAG", "Short#LONGTAG")]
        [TestCase("", "")]
        [TestCase(null, null)]
        [TestCase("123456789012345e\u0301xy#NW", "123456789012345e\u0301\u2026#NW")]
        [TestCase("123456789012345\U0001F600xy#NW", "123456789012345\U0001F600\u2026#NW")]
        public void ConstrainedHeaderCapsOnlyTheNameWithoutSplittingVisibleCharacters(string handle, string expected)
            => Assert.That(HandleDisplay.Constrained(handle), Is.EqualTo(expected));

        [TestCase("Ab_9-z", "Ab_9-z")]
        [TestCase(" A!b @C# ", "AbC")]
        [TestCase("", "Player")]
        [TestCase(null, "Player")]
        [TestCase("!é<> ", "Player")]
        [TestCase("x", "xPlayer")]
        [TestCase("12", "12Player")]
        [TestCase("12345678901234567", "1234567890123456")]
        public void FirstRunNameProducesValidServerName(string localName, string expected)
        {
            Assert.That(HandleDisplay.FirstRunName(localName), Is.EqualTo(expected));
        }
    }
}
