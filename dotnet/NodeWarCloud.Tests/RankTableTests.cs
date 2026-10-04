using NodeWar.Backend;
using NodeWar.Progression;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class RankTableTests
    {
        [Test]
        public void SharedThresholds_MatchDefaultProgressionConfig()
        {
            Assert.That(RankTable.Thresholds, Is.EqualTo(new ArenaConfig().Thresholds));
            Assert.That(RankTable.Names, Is.EqualTo(new[]
                { "Arena 1", "Arena 2", "Arena 3", "Arena 4", "Arena 5", "Arena 6" }));
            Assert.That(RankTable.Names.Count, Is.EqualTo(RankTable.Thresholds.Count));
        }
    }
}
