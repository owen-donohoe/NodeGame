using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    public class SimulationStateHasherTests
    {
        // Registration is deliberate: a new field must be hashed and added here,
        // or receive a reviewed exclusion below. Mutation alone cannot register it.
        private static readonly string[] Hashed =
        {
            "SimulationState.nodes", "SimulationState.villagers", "SimulationState.players",
            "SimulationState.tickCount", "SimulationState.gameOver", "SimulationState.winnerID",
            "SimulationState.defaultEdgeWeight",
            "NodeData.nodeID", "NodeData.districtType", "NodeData.claimBar", "NodeData.ownerID",
            "NodeData.materialAllocation", "NodeData.slotType", "NodeData.baseDistrictType",
            "NodeData.districtEra",
            "VillagerData.villagerID", "VillagerData.ownerID", "VillagerData.currentNodeID",
            "VillagerData.targetNodeID", "VillagerData.movePath", "VillagerData.movePathIndex",
            "VillagerData.moveProgress", "VillagerData.previousNodeID", "VillagerData.state",
            "VillagerData.suit", "VillagerData.hp", "VillagerData.maxHP", "VillagerData.attackDamage",
            "VillagerData.moveSpeedTicks", "VillagerData.respawnTicksRemaining",
            "VillagerData.attackCooldownRemaining", "VillagerData.attackCooldownMax",
            "VillagerData.combatTargetID", "VillagerData.fightPriority", "VillagerData.isConsumed",
            "VillagerData.productionTicksRemaining", "VillagerData.productionTicksMax",
            "VillagerData.hasRampartBonus", "VillagerData.rampartBonusEra",
            "PlayerData.playerID", "PlayerData.coreNodeID", "PlayerData.food", "PlayerData.materials",
            "PlayerData.metal", "PlayerData.breachCount", "PlayerData.draftedSuits",
            "PlayerData.draftedNodes", "PlayerData.suitEras", "PlayerData.districtEras"
        };

        private static readonly string[] Excluded =
        {
            // ComputeHash explicitly excludes grid position as view-only data.
            "NodeData.gridX", "NodeData.gridZ",
            // CopyFrom shares edges because board topology is fixed and no tick
            // writes it. The edge fields have the same construction-only lifetime.
            "NodeData.edges", "Edge.toNode", "Edge.travelWeight",
            // SpawnBonusVillagers reads this board-construction setting on claim;
            // no tick changes it. docs/simulation-rules.md explicitly excludes it.
            "NodeData.bonusVillagersOnClaim"
        };

        [Test]
        public void DiscoveredFields_EqualHashedAndExcludedRegistrations()
        {
            var discovered = new List<string>();
            foreach (FieldInfo[] path in DiscoverFields()) discovered.Add(Name(path));
            var registered = new List<string>(Hashed);
            registered.AddRange(Excluded);
            CollectionAssert.AllItemsAreUnique(registered, "Hash and exclusion registrations must not overlap.");
            CollectionAssert.AreEquivalent(registered, discovered,
                "Unknown/stale fields: update ComputeHash and its registration, or justify an exclusion.");
        }

        [TestCaseSource(nameof(HashedCases))]
        public void RegisteredField_MutationChangesHash(string name, FieldInfo[] path)
        {
            AssertMutation(name, path, shouldChange: true);
        }

        [TestCaseSource(nameof(ExcludedCases))]
        public void ExcludedField_MutationLeavesHashUnchanged(string name, FieldInfo[] path)
        {
            AssertMutation(name, path, shouldChange: false);
        }

        private static IEnumerable<TestCaseData> HashedCases() => Cases(Hashed);
        private static IEnumerable<TestCaseData> ExcludedCases() => Cases(Excluded);

        private static IEnumerable<TestCaseData> Cases(string[] registrations)
        {
            foreach (FieldInfo[] path in DiscoverFields())
            {
                string name = Name(path);
                if (Array.IndexOf(registrations, name) >= 0)
                    yield return new TestCaseData(name, path);
            }
        }

        private static string Name(FieldInfo[] path)
        {
            FieldInfo field = path[path.Length - 1];
            return field.DeclaringType.Name + "." + field.Name;
        }

        // Follow array/List<T> element types recursively, even through excluded
        // collections. Include private instance fields, as the copy guard does.
        private static List<FieldInfo[]> DiscoverFields()
        {
            var result = new List<FieldInfo[]>();
            Discover(typeof(SimulationState), new FieldInfo[0], new List<Type>(), result);
            return result;
        }

        private static void Discover(Type type, FieldInfo[] prefix, List<Type> seen, List<FieldInfo[]> result)
        {
            if (seen.Contains(type)) return;
            seen.Add(type);
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var path = new FieldInfo[prefix.Length + 1];
                Array.Copy(prefix, path, prefix.Length);
                path[prefix.Length] = field;
                result.Add(path);
                Type element = ElementType(field.FieldType);
                if (element != null && !element.IsPrimitive && !element.IsEnum)
                    Discover(element, path, seen, result);
            }
        }

        private static Type ElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return type.GetGenericArguments()[0];
            return null;
        }

        private static void AssertMutation(string name, FieldInfo[] path, bool shouldChange)
        {
            SimulationState source = TestBoardFactory.BuildThreeNodeBoard(GameBalanceData.Default());
            var copy = new SimulationState();
            copy.CopyFrom(source);
            int baseline = SimulationStateHasher.ComputeHash(source);
            Assert.AreEqual(baseline, SimulationStateHasher.ComputeHash(copy), "Copy must start identical.");
            ChangeField(copy, path, 0);
            int actual = SimulationStateHasher.ComputeHash(copy);
            if (shouldChange)
                Assert.AreNotEqual(baseline, actual, name + " does not reach ComputeHash.");
            else
                Assert.AreEqual(baseline, actual, name + " is no longer legitimately excluded.");
            Assert.AreEqual(baseline, SimulationStateHasher.ComputeHash(source), "Mutation must not alter source.");
        }

        // Box structs, mutate one field, then write the box back into its cloned
        // collection. In particular, clone shared node edges before touching them.
        private static void ChangeField(object owner, FieldInfo[] path, int depth)
        {
            FieldInfo field = path[depth];
            object value = field.GetValue(owner);
            if (depth == path.Length - 1)
            {
                field.SetValue(owner, ChangedValue(field.FieldType, value));
                return;
            }
            IList collection = CloneCollection(field.FieldType, value);
            Assert.Greater(collection.Count, 0, Name(path) + " requires a populated fixture collection.");
            object element = collection[0];
            ChangeField(element, path, depth + 1);
            collection[0] = element;
            field.SetValue(owner, collection);
        }

        private static object ChangedValue(Type type, object value)
        {
            if (type == typeof(int)) return (int)value + 1;
            if (type == typeof(bool)) return !(bool)value;
            if (type.IsEnum) return Enum.ToObject(type, Convert.ToInt32(value) + 1);
            Type element = ElementType(type);
            if (element != null)
            {
                IList source = value as IList;
                // Integer collections must change content, including a 0 -> 1
                // era entry (zero eras intentionally preserve legacy hashes).
                if (element == typeof(int))
                {
                    IList copy = CloneCollection(type, value, source == null || source.Count == 0 ? 1 : 0);
                    copy[0] = (int)copy[0] + 1;
                    return copy;
                }
                // State entity arrays: append an existing element to change the
                // collection itself; individual entity fields are tested separately.
                Assert.IsNotNull(source, type.Name + " needs a populated fixture.");
                Assert.Greater(source.Count, 0, type.Name + " needs a populated fixture.");
                IList expanded = CloneCollection(type, value, 1);
                expanded[expanded.Count - 1] = source[0];
                return expanded;
            }
            Assert.Fail("No mutation for " + type.Name + "; teach this guard the new field type.");
            return null;
        }

        private static IList CloneCollection(Type type, object value, int extra = 0)
        {
            IList source = value as IList;
            int count = source == null ? 0 : source.Count;
            Type element = ElementType(type);
            IList copy;
            if (type.IsArray) copy = Array.CreateInstance(element, count + extra);
            else
            {
                copy = (IList)Activator.CreateInstance(type);
                for (int i = 0; i < count + extra; i++)
                    copy.Add(element.IsValueType ? Activator.CreateInstance(element) : null);
            }
            for (int i = 0; i < count; i++) copy[i] = source[i];
            return copy;
        }
    }
}
