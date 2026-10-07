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
                "Camp", "Shrine", "Arsenal", "Sanctuary", "Watchtower", "Rampart", "Market"
            };
            Assert.AreEqual(14, Enum.GetValues(typeof(DistrictType)).Length);
            for (int i = 0; i < districts.Length; i++)
            {
                Assert.AreEqual(districts[i], ((DistrictType)i).ToString(), "DistrictType " + i);
                Assert.AreEqual(i, (int)Enum.Parse(typeof(DistrictType), districts[i]), districts[i]);
            }

            string[] categories = { "Fixed", "Army", "Healing", "Affect", "ResourceSpecial" };
            Assert.AreEqual(5, Enum.GetValues(typeof(NodeSlotType)).Length);
            for (int i = 0; i < categories.Length; i++)
            {
                Assert.AreEqual(categories[i], ((NodeSlotType)i).ToString(), "upgrade category " + i);
                Assert.AreEqual(i, (int)Enum.Parse(typeof(NodeSlotType), categories[i]), categories[i]);
            }
        }
    }
}
