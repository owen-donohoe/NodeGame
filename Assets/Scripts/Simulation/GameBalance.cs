using UnityEngine;

namespace NodeWar.Simulation
{
    [CreateAssetMenu(fileName = "GameBalance", menuName = "NodeWar/Game Balance")]
    public class GameBalance : ScriptableObject
    {
        [Header("Tick Rate")]
        public int ticksPerSecond = 10;

        [Header("Claiming")]
        public int baseClaimPerTick = 17;
        public int decrementMultiplier = 4;
        public int claimThreshold = 10000;
        public int maxClaimersPerNode = 4;

        [Header("Combat")]
        public int respawnTicks = 50;
        public int healIntervalTicks = 30;
        public int breachThreshold = 3;

        [Header("Production")]
        public int foodProductionTicks = 30;
        public int materialProductionTicks = 40;
        public int metalProductionTicks = 50;
        public int maxWorkersPerNode = 2;
        public int maxVillagersPerPlayer = 25;

        [Header("Costs")]
        public int respawnCostFood = 1;
        public int soldierCostFood = 2;
        public int soldierCostMaterial = 1;

        [Header("Villager Base Stats")]
        public int baseHP = 5;
        public int baseAttackDamage = 1;
        public int baseMoveSpeedTicks = 4;
        public int baseAttackCooldownMax = 20;

        [Header("Suit: Soldier")]
        public int soldierBonusHP = 0;
        public int soldierAttackDamage = 2;
        public int soldierMoveSpeedTicks = 5;
        public int soldierAttackCooldownMax = 10;

        [Header("Suit: Farmer")]
        public int farmerBonusHP = 0;
        public int farmerAttackDamage = 1;
        public int farmerMoveSpeedTicks = 4;

        [Header("Suit: Miner")]
        public int minerBonusHP = 0;
        public int minerAttackDamage = 1;
        public int minerMoveSpeedTicks = 4;

        [Header("Suit: Smelter")]
        public int smelterBonusHP = 0;
        public int smelterAttackDamage = 1;
        public int smelterMoveSpeedTicks = 4;

        [Header("Village Bonus")]
        public int bonusVillagersOnVillageClaim = 2;
    }
}