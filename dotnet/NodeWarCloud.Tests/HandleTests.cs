using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class HandleTests
    {
        [TestCase("Ab_c-09", true)]
        [TestCase("ab", false)]
        [TestCase("abcdefghijklmnop", true)]
        [TestCase("abcdefghijklmnopq", false)]
        [TestCase("abc def", false)]
        [TestCase("éclair", false)]
        [TestCase(null, false)]
        public void NameFormat(string name, bool valid) => Assert.That(HandleRules.IsValidName(name), Is.EqualTo(valid));

        [TestCase("a1", "A1", true)]
        [TestCase("1234", "1234", true)]
        [TestCase("12345", "12345", false)]
        [TestCase("a_", "A_", false)]
        [TestCase("", "", false)]
        public void TagFormatAndNormalization(string input, string normalized, bool valid)
        {
            Assert.That(HandleRules.NormalizeTag(input), Is.EqualTo(normalized));
            Assert.That(HandleRules.IsValidTag(normalized), Is.EqualTo(valid));
            Assert.That(HandleRules.NormalizeName("Player-One"), Is.EqualTo("player-one"));
            Assert.That(HandleRules.Display(new ProfileRecord { Name = "Player", Tag = "A1" }), Is.EqualTo("Player#A1"));
        }

        [TestCase("xx4d_m1nxx")]
        [TestCase("m0d3r470r")]
        [TestCase("n0d3-w4r")]
        [TestCase("0ff1c14l")]
        [TestCase("5upp0r7")]
        public void BlockedNamesFoldLeetAndSeparators(string name) =>
            Assert.That(HandleRules.IsBlocked(name, EmbeddedBlockedNames.Names), Is.True);

        [Test]
        public void BlocklistParsesCommentsAndBlankLines()
        {
            Assert.That(EmbeddedBlockedNames.Parse(" # comment\r\n\nAdmin\n support \n"),
                Is.EqualTo(new[] { "admin", "support" }));
            Assert.That(HandleRules.IsBlocked("player", EmbeddedBlockedNames.Names), Is.False);
        }

        [Test]
        public void ShardingUsesFnv1aAndLowercaseHex()
        {
            Assert.That(HandleRegistry.Shard("hello"), Is.EqualTo("2b")); // FNV-1a 0x4f9f2cab
            for (int i = 0; i < 100; i++)
                Assert.That(Convert.ToInt32(HandleRegistry.Shard("player" + i), 16), Is.InRange(0, 63));
            Assert.That(HandleRegistry.Key("player", "AB"), Is.EqualTo("player#AB"));
        }
    }
}
