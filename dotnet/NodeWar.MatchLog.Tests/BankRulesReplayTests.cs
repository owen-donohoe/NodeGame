using NodeWar.Simulation;
using NodeWar.Tests;
using NUnit.Framework;
namespace NodeWar.MatchLog
{
    public class BankRulesReplayTests
    {
        private static MatchLog Record(out SimulationState reference)
        {
            reference=BankRulesFixture.Reference(out var hashes,out _);
            var balance=BankRulesFixture.Balance; var board=BankRulesFixture.Board();
            var header=new MatchLogHeader {protocol=5,sim=(ushort)SimulationVersion.Current,content=BalanceHasher.Hash(balance),
                matchId="bank-rules",playerIds=new[]{"",""},kind=MatchKind.Bot};
            var setup=new MatchSetup(BankRulesFixture.MapId,BoardHasher.Hash(board),header.sim,header.content);
            var players=CoreRulesFixture.Players(); var loadouts=new[]{new PlayerLoadout {suits=players[0].suits,districts=players[0].districts},new PlayerLoadout {suits=players[1].suits,districts=players[1].districts}};
            var recorder=new MatchRecorder(header,setup,board,loadouts,BankRulesFixture.Draft);
            for(int tick=0;tick<1500;tick++) {recorder.RecordTick(tick,BankRulesFixture.Commands(tick)); recorder.RecordHash(tick+1,hashes[tick+1]);}
            recorder.Finish(new MatchResult {reason=MatchEndReason.Abandoned,winner=-1,endTick=1500,finalHash=SimulationStateHasher.ComputeHash(reference),firstDesyncTick=-1});
            Assert.That(MatchLogFormat.TryRead(MatchLogFormat.Write(recorder.Log),out var log,out var error),Is.True,error);
            return log;
        }
        private static MatchLog RoundTrip(MatchLog log)
        { Assert.That(MatchLogFormat.TryRead(MatchLogFormat.Write(log),out var copy,out var error),Is.True,error); return copy; }
        private static void Advance(SimulationState s,int end)
        {while(s.tickCount<end) {foreach(var c in BankRulesFixture.Commands(s.tickCount)) CommandProcessor.ProcessCommand(s,c); GameSimulation.SimulateTick(s);}}

        [Test] public void Version4Script_ReplaysAllCommands()
        {
            var log=Record(out var reference); var balance=BankRulesFixture.Balance;
            Assert.That(log.header.sim,Is.EqualTo(4));
            int logged=0; foreach(var tick in log.ticks) logged+=tick.commands.Length;
            int scripted=0; for(int t=0;t<1500;t++) scripted+=BankRulesFixture.Commands(t).Length;
            Assert.That(logged,Is.EqualTo(scripted)); Assert.That(logged,Is.GreaterThan(0));
            var types=new System.Collections.Generic.HashSet<CommandType>();
            foreach(var tick in log.ticks) foreach(var c in tick.commands) types.Add(c.type);
            CollectionAssert.IsSupersetOf(types,new[]{CommandType.ForgeMinion,CommandType.Collect,CommandType.Recruit,CommandType.UpgradeFortress,CommandType.Equip,CommandType.Move});
            Assert.That(log.hashes.Count,Is.EqualTo(1500));
            var outcome=MatchReplay.Run(log,balance); Assert.That(outcome.ok,Is.True,outcome.error);
            Assert.That(outcome.finalHash,Is.EqualTo(SimulationStateHasher.ComputeHash(reference)));

            // A version-3 log is refused before it runs.
            var v3=RoundTrip(log); v3.header.sim=3;
            var refused=MatchReplay.Run(v3,balance); Assert.That(refused.ok,Is.False); StringAssert.Contains("simulation version",refused.error);

            // A checkpoint during the first health raid, and the final hash, are independent rejection oracles.
            var tampered=RoundTrip(log); int index=tampered.hashes.FindIndex(h => h.tick>=120);
            var cp=tampered.hashes[index]; tampered.hashes[index]=new HashCheckpoint {tick=cp.tick,hash=cp.hash+1};
            var bad=MatchReplay.Run(tampered,balance); Assert.That(bad.ok,Is.False); Assert.That(bad.firstMismatchTick,Is.EqualTo(cp.tick));
            var badFinal=RoundTrip(log); badFinal.result.finalHash++; Assert.That(MatchReplay.Run(badFinal,balance).ok,Is.False);

            // A midpoint copy, replayed to the end, is the same full state.
            var s=BankRulesFixture.NewState(); Advance(s,750); var copy=new SimulationState(); copy.CopyFrom(s);
            int midpointHash=SimulationStateHasher.ComputeHash(copy);
            Advance(s,770); Assert.That(SimulationStateHasher.ComputeHash(copy),Is.EqualTo(midpointHash),"The saved snapshot is independent.");
            s.CopyFrom(copy); Assert.That(SimulationStateHasher.ComputeHash(s),Is.EqualTo(midpointHash)); Advance(s,1500);
            Assert.That(SimulationStateHasher.ComputeHash(s),Is.EqualTo(outcome.finalHash));
            for(int i=0;i<s.nodes.Length;i++) Assert.That((s.nodes[i].bankFood,s.nodes[i].bankMaterials,s.nodes[i].bankMetal,s.nodes[i].districtHealth,
                s.nodes[i].bankProductionRemaining,s.nodes[i].ownerID,s.nodes[i].collectProgress,s.nodes[i].collectRequested),
                Is.EqualTo((reference.nodes[i].bankFood,reference.nodes[i].bankMaterials,reference.nodes[i].bankMetal,reference.nodes[i].districtHealth,
                reference.nodes[i].bankProductionRemaining,reference.nodes[i].ownerID,reference.nodes[i].collectProgress,reference.nodes[i].collectRequested)));
            Assert.That((s.villagers[BankRulesFixture.Minion].suit,s.villagers[BankRulesFixture.Minion].hp,s.villagers[BankRulesFixture.Minion].isConsumed),
                Is.EqualTo((SuitType.Minion,0,true)));
        }
        [Test] public void Version4Script_ReplaysAllCommands_Determinism()
        {BankRulesFixture.Reference(out var a,out _); BankRulesFixture.Reference(out var b,out _); CollectionAssert.AreEqual(a,b);}
    }
}
