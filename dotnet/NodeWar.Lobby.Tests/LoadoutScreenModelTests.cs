using NodeWar.Lobby;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class LoadoutScreenModelTests
    {
        [TestCase(false)] [TestCase(true)]
        public void RemoveChosenPersistsOnlyActivePresetAndCannotRemoveLocks(bool suit)
        {
            var source = LoadoutData.CreateEmpty(0);
            var model = new LoadoutScreenModel(source, 0);
            model.Inspect(suit ? "suit_guardian" : "node_barracks", suit);
            Assert.IsTrue(model.Use(true, false));
            var profile = new PlayerProfileData();
            LoadoutPresets.Store(ref profile, model.ToData());
            var other = profile.loadoutPresets[1];
            var chosen = suit ? model.ToData().suitIDs : model.ToData().districtIDs;
            Assert.IsTrue(model.ClearChosen(suit, System.Array.IndexOf(chosen, model.PreviewId)));
            Assert.IsFalse(model.ClearChosen(suit, 0), "starter is fixed in era zero");
            LoadoutPresets.Store(ref profile, model.ToData());
            CollectionAssert.AreEqual(source.suitIDs, profile.loadout.ToLoadout().suitIDs);
            CollectionAssert.AreEqual(source.districtIDs, profile.loadout.ToLoadout().districtIDs);
            CollectionAssert.AreEqual(other.suitIDs, profile.loadoutPresets[1].suitIDs);
            CollectionAssert.AreEqual(other.nodeIDs, profile.loadoutPresets[1].nodeIDs);
        }
        [TestCase(1)] [TestCase(2)]
        public void Preview_DoesNotMutateChosen(int arena)
        {
            var source = LoadoutData.CreateEmpty(arena);
            var model = new LoadoutScreenModel(source, 2);
            model.Preview("suit_archer");
            Assert.That(model.PreviewId, Is.EqualTo("suit_archer"));
            Assert.That(model.ToData().suitIDs, Is.EqualTo(source.suitIDs));
            Assert.That(model.ToData().districtIDs, Is.EqualTo(LoadoutData.Normalized(source).districtIDs));
        }

        [TestCase(false, 1)] [TestCase(true, 1)]
        [TestCase(false, 2)] [TestCase(true, 2)]
        public void Use_FillsFirstEmptyOrRefusesFull(bool suit, int arena)
        {
            var model = new LoadoutScreenModel(new LoadoutData { suitIDs = new string[3], districtIDs = new string[5] }, 2);
            int capacity = suit ? model.ToData().suitIDs.Length : model.ToData().districtIDs.Length;
            for (int i = 0; i < capacity; i++)
            {
                model.Preview("item" + i);
                Assert.That(model.UsePreview(true, suit), Is.True);
                Assert.That((suit ? model.ToData().suitIDs : model.ToData().districtIDs)[i], Is.EqualTo("item" + i));
            }
            model.Preview("extra");
            Assert.That(model.UsePreview(true, suit), Is.False);
            Assert.That(model.ClearChosen(suit, 0), Is.True);
            Assert.That(model.UsePreview(true, suit), Is.True);
            Assert.That((suit ? model.ToData().suitIDs : model.ToData().districtIDs)[0], Is.EqualTo("extra"));
        }

        [TestCase(false)] [TestCase(true)]
        public void Selected_DoesNotUnequip(bool suit)
        {
            var model = new LoadoutScreenModel(LoadoutData.CreateEmpty());
            model.Preview("selected");
            Assert.That(model.UsePreview(true, suit), Is.True);
            var before = model.ToData();
            Assert.That(model.UsePreview(true, suit), Is.False);
            Assert.That(model.ToData().suitIDs, Is.EqualTo(before.suitIDs));
            Assert.That(model.ToData().districtIDs, Is.EqualTo(before.districtIDs));
        }

        [Test] public void Clear_AffectsChosenOnly()
        {
            var source = LoadoutData.CreateEmpty(2);
            source.suitIDs[0] = "suit_archer";
            source.districtIDs[0] = "node_town";
            source.suitEras[1] = 1;
            source.districtEras[1] = 1;
            source.skinIDs = new[] { "suit.warrior.skin.default" };
            var model = new LoadoutScreenModel(source, 2);
            Assert.That(model.ClearChosen(true, -1), Is.False);
            Assert.That(model.ClearChosen(true, 3), Is.False);
            Assert.That(model.ClearChosen(true, 0), Is.True);
            Assert.That(model.ClearChosen(true, 0), Is.False);
            Assert.That(model.ToData().districtIDs, Is.EqualTo(source.districtIDs));
            Assert.That(model.ToData().suitEras, Is.EqualTo(source.suitEras));
            Assert.That(model.ToData().districtEras, Is.EqualTo(source.districtEras));
            Assert.That(model.ToData().skinIDs, Is.EqualTo(source.skinIDs));
            Assert.That(source.suitIDs[0], Is.EqualTo("suit_archer"));
        }

        [Test] public void DeniedUse_PreservesData()
        {
            var model = new LoadoutScreenModel(LoadoutData.CreateEmpty());
            Assert.That(model.UsePreview(true, true), Is.False);
            model.Preview("suit_archer");
            Assert.That(model.UsePreview(false, true), Is.False);
            Assert.That(model.ToData().suitIDs, Is.EqualTo(LoadoutData.CreateEmpty().suitIDs));
        }
    }
}
