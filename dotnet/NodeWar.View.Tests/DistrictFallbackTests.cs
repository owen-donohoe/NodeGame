using System;
using System.Collections.Generic;
using NodeWar.Simulation;
using NodeWar.View;
using NUnit.Framework;
namespace NodeWar.View.Tests
{
    public class DistrictFallbackTests
    {
        [TestCase(DistrictType.Village,"Recruit","V","Village")]
        [TestCase(DistrictType.Town,"Town","T","Village")]
        [TestCase(DistrictType.Pier,"Pier","P","Crossroads")]
        [TestCase(DistrictType.Barracks,"Barracks","B","Barracks")]
        [TestCase(DistrictType.Infirmary,"Infirmary","I","Shrine")]
        [TestCase(DistrictType.Fortress,"Fortress","F","Rampart")]
        public void MissingArtIsReadableInAllSurfaces(DistrictType type,string name,string monogram,string prefab)
        {
            var d=DistrictFallback.Describe(type);
            Assert.That((d.Name,d.Monogram,d.PrefabKey),Is.EqualTo((name,monogram,prefab)));
            Assert.That(DistrictFallback.ResolveArt(type,_=>false),Is.EqualTo(DistrictType.None));
            Assert.That(DistrictFallback.ResolveArt(type,t=>t==type),Is.EqualTo(type));
        }
        [Test] public void HistoricalThemeKeysStillResolve()
        {
            Assert.That(DistrictFallback.ResolveArt(DistrictType.Infirmary,t=>t==DistrictType.Sanctuary),Is.EqualTo(DistrictType.Sanctuary));
            Assert.That(DistrictFallback.ResolveArt(DistrictType.Fortress,t=>t==DistrictType.Rampart),Is.EqualTo(DistrictType.Rampart));
            Assert.That((int)DistrictType.Village,Is.EqualTo(3)); Assert.That((int)DistrictType.Rampart,Is.EqualTo(12));
        }        [Test]
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
                Assert.IsFalse(string.IsNullOrEmpty(descriptor.PrefabKey));
            }
            string[] requiredNames = { "Recruit", "Town", "Pier", "Barracks", "Infirmary", "Fortress" };
            int[] ids = { 3, 15, 14, 4, 16, 17 };
            for (int i = 0; i < ids.Length; i++)
            {
                var descriptor = DistrictFallback.Describe((DistrictType)ids[i]);
                Assert.AreEqual(requiredNames[i], descriptor.Name);
                Assert.AreEqual(i == 0 ? "V" : requiredNames[i].Substring(0, 1), descriptor.Monogram);
            }
        }
    }
}
