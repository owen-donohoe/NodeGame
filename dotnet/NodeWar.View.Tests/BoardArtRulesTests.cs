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
    }
}
