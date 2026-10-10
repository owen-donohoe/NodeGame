using NodeWar.Simulation;
using NodeWar.Tests;
using NUnit.Framework;
namespace NodeWar.Network.Tests
{
    public class BankRulesLockstepTests
    {
        [TestCase(false)] [TestCase(true)]
        public void HourglassBanksGatesAndRecruit_RollbackMatchesReference(bool lossy)
        {
            var expected=BankRulesFixture.Reference(out var hashes,out var populations);
            var s=new LockstepScenario {Seconds=150.1,Board=BankRulesFixture.Board(),Draft=BankRulesFixture.Draft,
                CommandScript=BankRulesFixture.Script,CaptureConfirmedTicks=true,BalanceFactory=() => BankRulesFixture.Balance};
            if(lossy)
            {
                s.ZeroToOne=new LinkProfile {Loss=0.02,Duplicate=0.1,Reorder=0.1}.Cut(90,90.4);
                s.OneToZero=new LinkProfile {Loss=0.02,Duplicate=0.1,Reorder=0.1};
            }
            s.Run(); s.AssertMatchesReference(29);
            foreach(var peer in s.Peers)
            {
                Assert.That(peer.State.tickCount,Is.EqualTo(1500));
                Assert.That(SimulationStateHasher.ComputeHash(peer.State),Is.EqualTo(SimulationStateHasher.ComputeHash(expected)));
                foreach(var pair in peer.ConfirmedHashes) Assert.That(pair.Value,Is.EqualTo(hashes[pair.Key]),"confirmed tick "+pair.Key);
                Assert.That(peer.ConfirmedHashes.Count,Is.GreaterThanOrEqualTo(1451));
                foreach(var pair in peer.ConfirmedPopulations) CollectionAssert.AreEqual(populations[pair.Key],pair.Value,"population tick "+pair.Key);
            }
            if(lossy) {Assert.That(s.Links[0].Lost+s.Links[1].Lost,Is.GreaterThan(0)); Assert.That(s.Peers[0].Rollbacks+s.Peers[1].Rollbacks,Is.GreaterThan(0)); Assert.That(s.Peers[0].DuplicateDeliveries+s.Peers[1].DuplicateDeliveries,Is.GreaterThan(0));}
        }
        [Test] public void HourglassBanksGatesAndRecruit_RollbackMatchesReference_Determinism()
        {
            var a=BankRulesFixture.Reference(out var hashesA,out _); var b=BankRulesFixture.Reference(out var hashesB,out _);
            CollectionAssert.AreEqual(hashesA,hashesB); Assert.That(SimulationStateHasher.ComputeHash(a),Is.EqualTo(SimulationStateHasher.ComputeHash(b)));
        }
    }
}
