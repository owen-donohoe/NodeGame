using NodeWar.Simulation;
using NodeWar.View;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class DisconnectPresentationTests
    {
        [TestCase(0)]
        [TestCase(1)]
        public void ConsecutiveDraftTimeoutsHaveNoMatchTally(int timedOutPlayer)
        {
            var draft = new DraftState(PremadeMaps.Hourglass01());
            var picks = new[] { new DraftPick { districtType = DistrictType.Farm },
                new DraftPick { districtType = DistrictType.Mine } };
            draft.player0Picks = picks;
            draft.player1Picks = (DraftPick[])picks.Clone();
            draft.Apply(timedOutPlayer, 0, 1, 1, true);
            draft.Apply(timedOutPlayer, 1, 1, 2, true);
            Assert.AreEqual(2, draft.consecutiveTimeouts[timedOutPlayer]);
            Assert.IsFalse(DisconnectPresentation.HasMatchTally(new SimulationState()));
            Assert.IsFalse(DisconnectPresentation.HasMatchTally(null));
        }

        [Test]
        public void BuiltMatchKeepsTheDisconnectTally()
        {
            var state = new SimulationState { players = new PlayerData[2] };
            Assert.IsTrue(DisconnectPresentation.HasMatchTally(state));
        }
    }
}
