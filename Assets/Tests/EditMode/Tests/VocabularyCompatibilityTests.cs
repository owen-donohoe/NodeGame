using System;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    /// <summary>
    /// What a mechanical rename must not move: the numbers behind the names.
    /// District types and upgrade categories are written to match logs, hashed
    /// into the state and saved in assets by value.
    /// </summary>
    public class VocabularyCompatibilityTests
    {
        [Test]
        public void EnumNumbers_AreStable()
        {
            string[] districts =
            {
                "None", "Farm", "Mine", "Village", "Barracks", "Core", "Forge",
                "Camp", "Shrine", "Arsenal", "Sanctuary", "Watchtower", "Rampart", "Market", "Pier", "Town", "Infirmary", "Fortress"
            };
            Assert.AreEqual(18, Enum.GetValues(typeof(DistrictType)).Length);
            for (int i = 0; i < districts.Length; i++)
            {
                Assert.AreEqual(districts[i], ((DistrictType)i).ToString(), "DistrictType " + i);
                Assert.AreEqual(i, (int)Enum.Parse(typeof(DistrictType), districts[i]), districts[i]);
            }

            // Suits are stored and keyed by number too (catalog bases, wire loadouts, logs).
            string[] suits =
            {
                "None", "Farmer", "Miner", "Warrior", "Smelter", "Guardian", "Scout",
                "Berserker", "Medic", "Merchant", "Acolyte", "Watcher"
            };
            Assert.AreEqual(12, Enum.GetValues(typeof(SuitType)).Length);
            for (int i = 0; i < suits.Length; i++)
            {
                Assert.AreEqual(suits[i], ((SuitType)i).ToString(), "SuitType " + i);
                Assert.AreEqual(i, (int)Enum.Parse(typeof(SuitType), suits[i]), suits[i]);
            }

            string[] categories = { "Fixed", "Army", "Healing", "Affect", "ResourceSpecial" };
            Assert.AreEqual(5, Enum.GetValues(typeof(DistrictUpgradeCategory)).Length);
            for (int i = 0; i < categories.Length; i++)
            {
                Assert.AreEqual(categories[i], ((DistrictUpgradeCategory)i).ToString(), "upgrade category " + i);
                Assert.AreEqual(i, (int)Enum.Parse(typeof(DistrictUpgradeCategory), categories[i]), categories[i]);
            }
        }
    }
}
