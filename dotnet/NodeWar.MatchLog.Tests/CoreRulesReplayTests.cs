using System.Collections.Generic;
using NodeWar.Simulation;
using NodeWar.Tests;
using NUnit.Framework;
namespace NodeWar.MatchLog
{
    public class CoreRulesReplayTests
    {
        [Test] public void NewRosterAndCommands_ReplayExactly()
        {
            var reference=CoreRulesFixture.Reference(out var hashes,out _);
            var balance=CoreRulesFixture.Balance; var board=CoreRulesFixture.Board();
            var header=new MatchLogHeader {protocol=5,sim=(ushort)SimulationVersion.Current,content=BalanceHasher.Hash(balance),
                matchId="core-rules",playerIds=new[]{"",""},kind=MatchKind.Bot};
            var setup=new MatchSetup(CoreRulesFixture.MapId,BoardHasher.Hash(board),header.sim,header.content);
            var players=CoreRulesFixture.Players(); var loadouts=new[]{new PlayerLoadout {suits=players[0].suits,districts=players[0].districts},new PlayerLoadout {suits=players[1].suits,districts=players[1].districts}};
            var recorder=new MatchRecorder(header,setup,board,loadouts,CoreRulesFixture.Draft);
            for(int tick=0;tick<1500;tick++) {recorder.RecordTick(tick,CoreRulesFixture.Commands(tick)); recorder.RecordHash(tick+1,hashes[tick+1]);}
            recorder.Finish(new MatchResult {reason=MatchEndReason.Abandoned,winner=-1,endTick=1500,finalHash=SimulationStateHasher.ComputeHash(reference),firstDesyncTick=-1});
            Assert.That(MatchLogFormat.TryRead(MatchLogFormat.Write(recorder.Log),out var log,out var error),Is.True,error);
            foreach(var tick in log.ticks)
            {
                var commands=CoreRulesFixture.Commands(tick.tick); Assert.That(tick.commands.Length,Is.EqualTo(commands.Length));
                for(int i=0;i<commands.Length;i++)
                {var a=commands[i]; var b=tick.commands[i]; Assert.That((b.type,b.playerID,b.targetNodeID,b.villagerID,b.issuedOnTick,b.value),Is.EqualTo((a.type,a.playerID,a.targetNodeID,a.villagerID,a.issuedOnTick,a.value)));}
            }
            Assert.That(log.hashes.Count,Is.EqualTo(1500)); var outcome=MatchReplay.Run(log,balance); Assert.That(outcome.ok,Is.True,outcome.error);
            Assert.That(outcome.finalHash,Is.EqualTo(SimulationStateHasher.ComputeHash(reference)));
            var s=CoreRulesFixture.NewState(); Advance(s,750); var copy=new SimulationState(); copy.CopyFrom(s);
            Advance(s,770); s.CopyFrom(copy); Advance(s,1500);
            Assert.That(SimulationStateHasher.ComputeHash(s),Is.EqualTo(outcome.finalHash));
        }
        private static void Advance(SimulationState s,int end)
        {while(s.tickCount<end) {foreach(var c in CoreRulesFixture.Commands(s.tickCount)) CommandProcessor.ProcessCommand(s,c); GameSimulation.SimulateTick(s);}}
        [Test] public void NewRosterAndCommands_ReplayExactly_Determinism()
        {CoreRulesFixture.Reference(out var a,out _); CoreRulesFixture.Reference(out var b,out _); CollectionAssert.AreEqual(a,b);}
    }
}
