using System;
using System.Reflection;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.View.Tests
{
    public class OrderPresentationTests
    {
        // Reflection lets the tests run RED before the new helper exists.
        private static object Call(string method, params object[] args)
        {
            var type = typeof(RouteReveal).Assembly.GetType("NodeWar.View.OrderPresentation");
            Assert.IsNotNull(type, "Sticky intent presentation helper must exist");
            return type.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        }
        private static T Field<T>(object result, string name) =>
            (T)result.GetType().GetField(name).GetValue(result);
        private static SimulationState Board()
        {
            var s = new SimulationState();
            s.players = new[] { new PlayerData { coreNodeID = 0 }, new PlayerData { coreNodeID = 4 } };
            s.nodes = new NodeData[5];
            for (int i = 0; i < 5; i++)
                s.nodes[i] = new NodeData { nodeID = i, ownerID = 0,
                    links = i == 4 ? new Link[0] : new[] { new Link { toNodeID = i + 1, travelWeight = 1 } } };
            s.villagers = new[] { new VillagerData { villagerID = 0, ownerID = 0,
                currentNodeID = 0, targetNodeID = 2, state = VillagerState.Fighting,
                movePath = new[] { 0, 3 }, movePathIndex = 0 } };
            return s;
        }
        private static object Route(SimulationState s, bool mine = true) =>
            Call("BuildRoute", s, s.villagers[0], mine, 1);

        [Test]
        public void InterruptedRoute_RemainsAtQuarterAlpha()
        {
            var s = Board();
            var style = Call("Style", s.villagers[0], 0.35f, false);
            Assert.AreEqual(0.25f, Field<float>(style, "alpha"), 0.0001f);
            Assert.IsTrue(Field<bool>(style, "visible"));
            Assert.IsTrue(Field<bool>(style, "amber"));
            Assert.IsTrue(Field<bool>(style, "dashed"));
            var later = Call("Style", s.villagers[0], 10f, false);
            Assert.AreEqual(0.25f, Field<float>(later, "alpha"), 0.0001f);
            var calm = Call("Style", s.villagers[0], 0f, true);
            Assert.AreEqual(0.25f, Field<float>(calm, "alpha"), 0.0001f);
        }
        [Test]
        public void Rollback_RebuildsIntentFromState()
        {
            var s = Board(); var saved = new SimulationState(); saved.CopyFrom(s);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Field<int[]>(Route(s), "nodes"));
            s.villagers[0].targetNodeID = 3;
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, Field<int[]>(Route(s), "nodes"));
            s.CopyFrom(saved);
            var restored = Field<int[]>(Route(s), "nodes");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, restored);
            CollectionAssert.DoesNotContain(restored, 3);
        }
        [Test]
        public void UnreachableRoute_IsIntentConnector()
        {
            var s = Board(); s.nodes[0].links = new Link[0]; s.villagers[0].state = VillagerState.Idle;
            var route = Route(s);
            Assert.IsTrue(Field<bool>(route, "intentConnector"));
            CollectionAssert.AreEqual(new[] { 0, 2 }, Field<int[]>(route, "nodes"));
            Assert.IsEmpty(s.nodes[0].links, "Presentation must not invent traversable links");
            var style = Call("Style", s.villagers[0], 1f, false);
            Assert.IsTrue(Field<bool>(style, "amber"));
        }
        [Test]
        public void OpponentInterruptedRoute_StillTruncated()
        {
            var s = Board(); s.villagers[0].targetNodeID = 3;
            var route = Route(s, false);
            CollectionAssert.AreEqual(new[] { 0, 1 }, Field<int[]>(route, "nodes"));
            CollectionAssert.DoesNotContain(Field<int[]>(route, "nodes"), 3);
            s.nodes[0].links = new Link[0];
            Assert.IsEmpty(Field<int[]>(Route(s, false), "nodes"), "No enemy intent connector may expose its destination");
        }
    }
}
