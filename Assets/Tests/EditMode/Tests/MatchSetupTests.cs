using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    /// <summary>
    /// MatchSetup is what two peers, a log and the server compare to agree on
    /// the map and the rules before anything else happens. The shipped catalog
    /// is the only authority; a hash alone proves nothing about the board.
    /// </summary>
    public class MatchSetupTests
    {
        private const int Balance = 123456;
        private static readonly ushort Sim = (ushort)SimulationVersion.Current;

        [Test]
        public void ForShippedMap_DescribesTheHourglassFromTheCatalog()
        {
            MatchSetup setup = MatchSetup.ForShippedMap(PremadeMaps.Hourglass01Id, Balance);

            Assert.AreEqual("hourglass-01", setup.MapId);
            Assert.AreEqual(BoardHasher.Hash(PremadeMaps.Hourglass01()), setup.BoardHash);
            Assert.AreEqual(SimulationVersion.Current, setup.SimulationVersion);
            Assert.AreEqual(Balance, setup.BalanceHash);
        }

        [Test]
        public void ForShippedMap_RefusesAnUnknownMap()
        {
            Assert.Throws<System.ArgumentException>(() => MatchSetup.ForShippedMap("no-such-map", Balance));
            Assert.Throws<System.ArgumentException>(() => MatchSetup.ForShippedMap(null, Balance));
        }

        [Test]
        public void Equality_IsByEveryField()
        {
            MatchSetup a = MatchSetup.ForShippedMap(PremadeMaps.Hourglass01Id, Balance);
            MatchSetup same = new MatchSetup(a.MapId, a.BoardHash, a.SimulationVersion, a.BalanceHash);
            Assert.IsTrue(a.Equals(same));
            Assert.AreEqual(a.GetHashCode(), same.GetHashCode());

            Assert.IsFalse(a.Equals(new MatchSetup("other", a.BoardHash, a.SimulationVersion, a.BalanceHash)));
            Assert.IsFalse(a.Equals(new MatchSetup(a.MapId, a.BoardHash + 1, a.SimulationVersion, a.BalanceHash)));
            Assert.IsFalse(a.Equals(new MatchSetup(a.MapId, a.BoardHash, (ushort)(a.SimulationVersion + 1), a.BalanceHash)));
            Assert.IsFalse(a.Equals(new MatchSetup(a.MapId, a.BoardHash, a.SimulationVersion, a.BalanceHash + 1)));
            Assert.IsFalse(a.Equals(null));
        }

        [Test]
        public void Verify_AcceptsTheCatalogMapUnderTheSameRules()
        {
            MatchSetup received = MatchSetup.ForShippedMap(PremadeMaps.Hourglass01Id, Balance);
            Assert.IsTrue(MatchSetup.Verify(received, PremadeMaps.Catalog, Sim, Balance, out string error), error);
        }

        [Test]
        public void Verify_RefusesWhatTheCatalogDoesNotVouchFor()
        {
            MatchSetup good = MatchSetup.ForShippedMap(PremadeMaps.Hourglass01Id, Balance);

            Assert.IsFalse(MatchSetup.Verify(null, PremadeMaps.Catalog, Sim, Balance, out string error));
            StringAssert.Contains("setup", error);

            Assert.IsFalse(MatchSetup.Verify(new MatchSetup("unknown-map", good.BoardHash, Sim, Balance),
                PremadeMaps.Catalog, Sim, Balance, out error));
            StringAssert.Contains("map", error);

            Assert.IsFalse(MatchSetup.Verify(new MatchSetup(good.MapId, good.BoardHash + 1, Sim, Balance),
                PremadeMaps.Catalog, Sim, Balance, out error));
            StringAssert.Contains("board", error);

            Assert.IsFalse(MatchSetup.Verify(new MatchSetup(good.MapId, good.BoardHash, (ushort)(Sim + 1), Balance),
                PremadeMaps.Catalog, Sim, Balance, out error));
            StringAssert.Contains("simulation", error);

            Assert.IsFalse(MatchSetup.Verify(new MatchSetup(good.MapId, good.BoardHash, Sim, Balance + 1),
                PremadeMaps.Catalog, Sim, Balance, out error));
            StringAssert.Contains("balance", error);
        }

        [Test]
        public void Catalog_ListsTheShippedMapAndNothingElse()
        {
            Assert.IsTrue(PremadeMaps.Catalog.TryGet("hourglass-01", out BoardConfigData board));
            Assert.AreEqual(BoardHasher.Hash(PremadeMaps.Hourglass01()), BoardHasher.Hash(board));
            Assert.IsFalse(PremadeMaps.Catalog.TryGet("hourglass-02", out _));
            Assert.IsFalse(PremadeMaps.Catalog.TryGet(null, out _));
            Assert.IsFalse(PremadeMaps.Catalog.TryGet("", out _));
            Assert.IsTrue(MapAuthoringRules.ValidateAuthoredMap(board, out string error), error);

            // A caller may mutate what it is handed without touching the catalog.
            board.terrain[0] = TerrainType.Land;
            PremadeMaps.Catalog.TryGet("hourglass-01", out BoardConfigData again);
            Assert.AreEqual(TerrainType.Ocean, again.terrain[0]);
        }
    }
}
