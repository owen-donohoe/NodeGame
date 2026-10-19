using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class StageShotsTests
    {
        [TestCase("Market", StageShot.Market)]
        [TestCase("Shop", StageShot.Market)]
        [TestCase("Workshop", StageShot.Loadout)]
        [TestCase("Loadout", StageShot.Loadout)]
        [TestCase("Home", StageShot.Home)]
        [TestCase("Social", StageShot.Social)]
        [TestCase("Guild", StageShot.Guild)]
        [TestCase("unknown", StageShot.Home)]
        [TestCase("", StageShot.Home)]
        [TestCase(null, StageShot.Home)]
        public void Page_MapsToShot(string page, StageShot expected)
        {
            Assert.AreEqual(expected, StageShots.ForPage(page));
        }
    }
}
