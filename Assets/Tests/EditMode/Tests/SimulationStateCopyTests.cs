using System;
using System.Reflection;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    /// <summary>
    /// SimulationState.CopyFrom is the rollback point of the speculative window
    /// (8.2e). A field it forgets is state that silently survives a rollback,
    /// which desyncs the two peers, so completeness is checked by reflection:
    /// every field of every state type is set to a distinct value and must come
    /// through the copy. A new field type the filler does not know fails here
    /// on purpose, so the copy and this test are revisited together.
    /// </summary>
    public class SimulationStateCopyTests
    {
        private int next;

        [Test]
        public void CopyFrom_CarriesEveryFieldOfEveryStateType()
        {
            SimulationState source = FilledState();
            var copy = new SimulationState();
            copy.CopyFrom(source);

            AssertSame(source, copy, "state");
        }

        [Test]
        public void CopyFrom_SharesNoMutableArray()
        {
            SimulationState source = FilledState();
            var copy = new SimulationState();
            copy.CopyFrom(source);

            Assert.AreNotSame(source.nodes, copy.nodes);
            Assert.AreNotSame(source.villagers, copy.villagers);
            Assert.AreNotSame(source.players, copy.players);
            for (int i = 0; i < source.villagers.Length; i++)
                Assert.AreNotSame(source.villagers[i].movePath, copy.villagers[i].movePath);
            for (int i = 0; i < source.players.Length; i++)
            {
                Assert.AreNotSame(source.players[i].draftedSuits, copy.players[i].draftedSuits);
                Assert.AreNotSame(source.players[i].draftedNodes, copy.players[i].draftedNodes);
                Assert.AreNotSame(source.players[i].suitEras, copy.players[i].suitEras);
                Assert.AreNotSame(source.players[i].districtEras, copy.players[i].districtEras);
            }

            int movePathBefore = copy.villagers[0].movePath[0];
            int claimBefore = copy.nodes[0].claimBar;
            int eraBefore = copy.players[0].suitEras[0];
            source.villagers[0].movePath[0] += 1000;
            source.nodes[0].claimBar += 1000;
            source.players[0].suitEras[0] += 1000;
            source.villagers = new VillagerData[0];

            Assert.AreEqual(movePathBefore, copy.villagers[0].movePath[0]);
            Assert.AreEqual(claimBefore, copy.nodes[0].claimBar);
            Assert.AreEqual(eraBefore, copy.players[0].suitEras[0]);
        }

        [Test]
        public void CopyFrom_KeepsTheSameInstance_AndHandlesNullArrays()
        {
            var target = new SimulationState();
            SimulationState reference = target;
            target.CopyFrom(new SimulationState());

            Assert.AreSame(reference, target);
            Assert.IsNull(target.nodes);
            Assert.IsNull(target.villagers);
            Assert.IsNull(target.players);
            Assert.Throws<ArgumentNullException>(() => target.CopyFrom(null));
        }

        [Test]
        public void ACopy_HashesAndSimulatesExactlyLikeItsSource()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);

            SimulationState source = TestBoardFactory.BuildThreeNodeBoard(balance);
            CommandProcessor.ProcessCommand(source, Move(0, 0, 1));
            CommandProcessor.ProcessCommand(source, Move(1, 1, 1));
            for (int i = 0; i < 3; i++) GameSimulation.SimulateTick(source);

            var copy = new SimulationState();
            copy.CopyFrom(source);
            Assert.AreEqual(SimulationStateHasher.ComputeHash(source), SimulationStateHasher.ComputeHash(copy));

            for (int i = 0; i < 200; i++)
            {
                if (i == 20)
                {
                    CommandProcessor.ProcessCommand(source, Move(0, 0, 2));
                    CommandProcessor.ProcessCommand(copy, Move(0, 0, 2));
                }
                GameSimulation.SimulateTick(source);
                GameSimulation.SimulateTick(copy);
            }
            Assert.AreEqual(SimulationStateHasher.ComputeHash(source), SimulationStateHasher.ComputeHash(copy));
        }

        [Test]
        public void RollingBackToACopy_ReplaysToTheSameHash()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);

            SimulationState live = TestBoardFactory.BuildThreeNodeBoard(balance);
            SimulationState reference = TestBoardFactory.BuildThreeNodeBoard(balance);
            var confirmed = new SimulationState();
            confirmed.CopyFrom(live);

            // Speculate with the opponent's move missing, then roll back and
            // replay with it: the result must equal a run that had it all along.
            CommandProcessor.ProcessCommand(live, Move(0, 0, 1));
            for (int i = 0; i < 10; i++) GameSimulation.SimulateTick(live);

            live.CopyFrom(confirmed);
            CommandProcessor.ProcessCommand(live, Move(0, 0, 1));
            CommandProcessor.ProcessCommand(live, Move(1, 1, 1));
            for (int i = 0; i < 10; i++) GameSimulation.SimulateTick(live);

            CommandProcessor.ProcessCommand(reference, Move(0, 0, 1));
            CommandProcessor.ProcessCommand(reference, Move(1, 1, 1));
            for (int i = 0; i < 10; i++) GameSimulation.SimulateTick(reference);

            Assert.AreEqual(SimulationStateHasher.ComputeHash(reference), SimulationStateHasher.ComputeHash(live));
        }

        private static GameCommand Move(int player, int villager, int node) => new GameCommand
        {
            type = CommandType.Move, playerID = player, villagerID = villager, targetNodeID = node
        };

        // ===== REFLECTION =====

        private SimulationState FilledState()
        {
            next = 1;
            var state = new SimulationState
            {
                nodes = new NodeData[2],
                villagers = new VillagerData[2],
                players = new PlayerData[2]
            };
            for (int i = 0; i < 2; i++)
            {
                state.nodes[i] = (NodeData)FillStruct(state.nodes[i]);
                state.villagers[i] = (VillagerData)FillStruct(state.villagers[i]);
                state.players[i] = (PlayerData)FillStruct(state.players[i]);
            }
            foreach (FieldInfo field in Fields(typeof(SimulationState)))
            {
                if (field.FieldType.IsArray && !IsIntArray(field.FieldType)) continue; // the three arrays above
                field.SetValue(state, ValueFor(field));
            }
            return state;
        }

        private object FillStruct(object boxed)
        {
            foreach (FieldInfo field in Fields(boxed.GetType()))
                field.SetValue(boxed, ValueFor(field));
            return boxed;
        }

        private object ValueFor(FieldInfo field)
        {
            Type t = field.FieldType;
            if (t == typeof(int)) return next++;
            if (t == typeof(bool)) return true;
            if (t.IsEnum)
            {
                Array values = Enum.GetValues(t);
                return values.GetValue(values.Length - 1);
            }
            if (IsIntArray(t)) return new[] { next++, next++, next++ };
            if (t == typeof(Edge[])) return new[] { new Edge { toNode = next++, travelWeight = next++ } };
            Assert.Fail("SimulationStateCopyTests does not know how to fill " + field.DeclaringType.Name + "." +
                        field.Name + " (" + t.Name + "). Teach it, and check SimulationState.CopyFrom copies it.");
            return null;
        }

        private static bool IsIntArray(Type t) => t == typeof(int[]);

        private static FieldInfo[] Fields(Type t) => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static void AssertSame(object expected, object actual, string path)
        {
            if (expected == null || actual == null)
            {
                Assert.AreEqual(expected, actual, path);
                return;
            }
            Type t = expected.GetType();
            if (t.IsArray)
            {
                var a = (Array)expected;
                var b = (Array)actual;
                Assert.AreEqual(a.Length, b.Length, path + ".Length");
                for (int i = 0; i < a.Length; i++) AssertSame(a.GetValue(i), b.GetValue(i), path + "[" + i + "]");
                return;
            }
            if (t.IsPrimitive || t.IsEnum)
            {
                Assert.AreEqual(expected, actual, path);
                return;
            }
            foreach (FieldInfo field in Fields(t))
                AssertSame(field.GetValue(expected), field.GetValue(actual), path + "." + field.Name);
        }
    }
}
