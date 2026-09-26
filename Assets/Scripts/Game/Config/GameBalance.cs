using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.Config
{
    [CreateAssetMenu(fileName = "GameBalance", menuName = "NodeWar/Game Balance")]
    public class GameBalance : ScriptableObject
    {
        /// <summary>
        /// The asset's name under a Resources folder
        /// (Assets/Data/Game/Balance/Resources/). It lives there so the lobby can
        /// hash it for the handshake before the Gameplay scene, where GameManager
        /// references the same asset, is loaded.
        /// </summary>
        public const string SharedResourceName = "DefaultGameBalance";

        [SerializeField] private GameBalanceData data = GameBalanceData.Default();

        public GameBalanceData Data => data;

        /// <summary>The balance every match is played with. Null only if the asset has gone missing.</summary>
        public static GameBalance LoadShared()
        {
            return Resources.Load<GameBalance>(SharedResourceName);
        }
    }
}
