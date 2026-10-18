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
