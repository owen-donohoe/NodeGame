using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NodeWar.Simulation;
namespace NodeWar.Backend.Editor
{
    /// <summary>Serialization and release checks shared with headless shape tests.
    /// Always receives actual client Data; never fills missing serialized tuning from defaults.</summary>
    public static class BalanceExportData
    {
        public static bool ReleaseValid(GameBalanceData data,out string reason)
        {
            if(!data.CoreRulesValid(out reason)) return false;
            if(!data.TryRecruitCostAndCooldown(0,0,out _,out _))
            {reason="Recruit base cost, increment and tick rate must be positive and overflow-safe."; return false;}
            foreach(int type in CatalogKeys.CatalogDistrictTypes)
                for(int era=0;era<GameBalanceData.EraCount;era++)
                {
                    bool found=false; DistrictStats stats=default;
                    if(data.districtStats!=null)
                        foreach(var d in data.districtStats)
                            if((int)d.districtType==type && d.era==era) {stats=d; found=true; break;}
                    if(!found) {reason="Missing active district "+type+" era "+era+"."; return false;}
                    if(stats.districtType==DistrictType.Town && stats.townBonusVillagers<=0)
                    {reason="Town entitlement must be positive in every era."; return false;}
                    if(stats.districtType==DistrictType.Fortress && !GameBalanceData.FortressStatsValid(stats))
                    {reason="Fortress costs/resistance must be present and valid in every era."; return false;}
                }
            reason=null; return true;
        }
        public static string Serialize(GameBalanceData data)
        {
            var serializer=JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver=new DefaultContractResolver(), Formatting=Formatting.Indented,
                Culture=CultureInfo.InvariantCulture, TypeNameHandling=TypeNameHandling.None
            });
            using(var writer=new StringWriter(CultureInfo.InvariantCulture))
            using(var json=new JsonTextWriter(writer)) {serializer.Serialize(json,data); return writer.ToString();}
        }
    }
}
