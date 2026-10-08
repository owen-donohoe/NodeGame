using System;
using System.Collections.Generic;
using NodeWar.Simulation;
using NodeWar.View;
using NUnit.Framework;
namespace NodeWar.View.Tests
{
    public class DistrictFallbackTests
    {
        [Test]
        public void EveryActiveDistrictHasReadableFallback()
        {
            var names = new HashSet<string>();
            var monograms = new HashSet<string>();
            foreach (DistrictType district in Enum.GetValues(typeof(DistrictType)))
            {
                if (!DistrictRoster.IsActive(district)) continue;
                var descriptor = DistrictFallback.Describe(district);
                Assert.IsFalse(string.IsNullOrEmpty(descriptor.Name));
                Assert.IsTrue(names.Add(descriptor.Name));
                Assert.IsTrue(monograms.Add(descriptor.Monogram));
                Assert.AreEqual("Crossroads", descriptor.PrefabKey);
            }
            string[] requiredNames = { "Village", "Town", "Pier", "Barracks", "Infirmary", "Fortress" };
            int[] ids = { 3, 15, 14, 4, 16, 17 };
            for (int i = 0; i < ids.Length; i++)
            {
                var descriptor = DistrictFallback.Describe((DistrictType)ids[i]);
                Assert.AreEqual(requiredNames[i], descriptor.Name);
                Assert.AreEqual(requiredNames[i].Substring(0, 1), descriptor.Monogram);
            }
        }
    }
}
