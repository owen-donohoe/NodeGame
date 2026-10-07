using NodeWar.View;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class BoardArtRulesTests
    {
        private sealed class Prefab
        {
            public readonly string Name;
            public readonly bool Alive;
            public Prefab(string name, bool alive = true) { Name = name; Alive = alive; }
        }

        private static bool Present(Prefab p) { return p != null && p.Alive; }

        [Test]
        public void TableEntryWinsOverTheSlot()
        {
            var table = new Prefab("table");
            var slot = new Prefab("slot");
            var def = new Prefab("default");
            Assert.AreSame(table, BoardArtRules.ChoosePrefab(table, slot, def, Present));
        }

        [Test]
        public void WithNoTableEntryTheSlotIsUsed()
        {
            var slot = new Prefab("slot");
            var def = new Prefab("default");
            Assert.AreSame(slot, BoardArtRules.ChoosePrefab(null, slot, def, Present));
        }

        [Test]
        public void WithNeitherTheDefaultIsUsed_SoNothingChangesUntilAnEntryIsFilled()
        {
            var def = new Prefab("default");
            Assert.AreSame(def, BoardArtRules.ChoosePrefab(null, null, def, Present));
        }

        [Test]
        public void ADeletedAssetCountsAsMissingAndFallsThrough()
        {
            var dead = new Prefab("dead", alive: false);
            var slot = new Prefab("slot");
            var def = new Prefab("default");
            Assert.AreSame(slot, BoardArtRules.ChoosePrefab(dead, slot, def, Present));
            Assert.AreSame(def, BoardArtRules.ChoosePrefab(dead, new Prefab("gone", false), def, Present));
        }

        [Test]
        public void DefaultsAreIdentity_AndAnySingleChangeIsNot()
        {
            Assert.IsTrue(BoardArtRules.IsIdentity(0, 0, 0, 0, 0, 0, 1));
            Assert.IsFalse(BoardArtRules.IsIdentity(0.1f, 0, 0, 0, 0, 0, 1));
            Assert.IsFalse(BoardArtRules.IsIdentity(0, -0.5f, 0, 0, 0, 0, 1));
            Assert.IsFalse(BoardArtRules.IsIdentity(0, 0, 0.2f, 0, 0, 0, 1));
            Assert.IsFalse(BoardArtRules.IsIdentity(0, 0, 0, 5, 0, 0, 1));
            Assert.IsFalse(BoardArtRules.IsIdentity(0, 0, 0, 0, 90, 0, 1));
            Assert.IsFalse(BoardArtRules.IsIdentity(0, 0, 0, 0, 0, -3, 1));
            Assert.IsFalse(BoardArtRules.IsIdentity(0, 0, 0, 0, 0, 0, 1.2f));
        }

        [TestCase(1f, 1f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(3f, 3f)]
        [TestCase(0f, 1f)]
        [TestCase(-2f, 1f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void ABadScaleMeansLeaveItAlone(float scale, float expected)
        {
            Assert.AreEqual(expected, BoardArtRules.SafeScale(scale));
        }

        [Test]
        public void AZeroScaleFromTheInspectorIsIdentityOnceSanitised()
        {
            // An unset float field on an old asset reads 0; it must not collapse the art.
            Assert.IsTrue(BoardArtRules.IsIdentity(0, 0, 0, 0, 0, 0, BoardArtRules.SafeScale(0f)));
        }

        // Prefabs name their villager positions with the older "WorkSlot"
        // prefix and are not re-authored by a code rename. Positions are given
        // out in the order the hierarchy lists them, whatever the suffix.
        [Test]
        public void LegacyWorkSlotNames_AreSupported()
        {
            string[] children =
            {
                "Model", "WORKPOINTS", "WorkSlot_1", "IdleCenter", "WorkSlot_0", "ClaimCenter", "WorkSlot", "workslot_9", null
            };

            CollectionAssert.AreEqual(new[] { 2, 4, 6 }, WorkPositionNames.Order(children));
            Assert.IsTrue(WorkPositionNames.IsWorkPosition("WorkSlot_0"));
            Assert.IsFalse(WorkPositionNames.IsWorkPosition("IdleCenter"));
            Assert.IsFalse(WorkPositionNames.IsWorkPosition(null));
            Assert.AreEqual("IdleCenter", WorkPositionNames.IdleCenter);
            Assert.AreEqual("ClaimCenter", WorkPositionNames.ClaimCenter);
        }
    }
}