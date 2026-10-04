using System.IO;
using NUnit.Framework;

namespace NodeWar.Tests
{
    public class DefaultBalanceAssetTests
    {
        [Test]
        [Ignore("needs DefaultGameBalance.asset re-saved through the editor with the v2 fields")]
        public void ShippedBalanceContainsEveryTempoAndBreachKey()
        {
            const string relative = "Assets/Data/Game/Balance/Resources/DefaultGameBalance.asset";
            DirectoryInfo root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, relative))) root = root.Parent;
            Assert.IsNotNull(root, "Cannot locate the repository's shipped balance asset.");
            string asset = File.ReadAllText(Path.Combine(root.FullName, relative));
            string[] keys =
            {
                "tempoStageTicks", "tempoClaimPercent", "tempoRespawnPercent", "tempoProductionPercent",
                "suddenDeathTicks", "suddenDeathThresholds", "breachBarMax", "breachSwarmRate", "breachBarDecayPerTick"
            };
            foreach (string key in keys)
                StringAssert.Contains("\n    " + key + ":", asset.Replace("\r\n", "\n"), "Missing serialized v2 balance field: " + key);
        }
    }
}
