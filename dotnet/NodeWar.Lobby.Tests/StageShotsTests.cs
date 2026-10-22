using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class StageShotsTests
    {
        [TestCase("Market", StageShot.Market)]
        [TestCase("Shop", StageShot.Market)]
        [TestCase("Workshop", StageShot.Districts)]
        [TestCase("Loadout", StageShot.Districts)]
        [TestCase("Home", StageShot.Districts)]
        [TestCase("Searching", StageShot.Queue)]
        [TestCase("Social", StageShot.Ocean)]
        [TestCase("Guild", StageShot.Ocean)]
        [TestCase("unknown", StageShot.Home)]
        [TestCase("", StageShot.Home)]
        [TestCase(null, StageShot.Home)]
        public void Page_MapsToShot(string page, StageShot expected)
        {
            Assert.AreEqual(expected, StageShots.ForPage(page));
        }
    }
}
