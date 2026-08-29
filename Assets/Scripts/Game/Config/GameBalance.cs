using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.Config
{
    [CreateAssetMenu(fileName = "GameBalance", menuName = "NodeWar/Game Balance")]
    public class GameBalance : ScriptableObject
    {
        [SerializeField] private GameBalanceData data = GameBalanceData.Default();

        public GameBalanceData Data => data;
    }
}
