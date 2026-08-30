using System.Collections.Generic;

namespace NodeWar.Simulation
{
    public enum DraftPhase
    {
        WaitingForReady,
        InitialReveal,
        ActiveDraft,
        Complete
    }

    [System.Serializable]
    public struct DraftSlot
    {
        public DistrictType districtType;
        public bool isConsumed;
        public bool isFromLoadout;
    }

    [System.Serializable]
    public struct DraftPlacement
    {
        public int playerID;
        public DistrictType districtType;
        public int gridX;
        public int gridZ;
        public bool wasTimeout;
    }

    /// <summary>
    /// Complete authoritative state of the draft phase.
    /// Maintained identically on both clients from the sequence of placement events.
    /// </summary>
    public class DraftState
    {
        public DraftPhase phase;
        public int currentTurnPlayerID;
        public int turnNumber;

        public DraftSlot[] player0Slots;
        public DraftSlot[] player1Slots;

        public List<DraftPlacement> confirmedPlacements;

        // Grid occupancy (true = occupied by initial or drafted node)
        public bool[,] occupiedCells;
        public int gridCols;
        public int gridRows;

        // Timeout tracking per player
        public int[] consecutiveTimeouts;

        public DraftState(int cols, int rows)
        {
            gridCols = cols;
            gridRows = rows;
            phase = DraftPhase.WaitingForReady;
            currentTurnPlayerID = 0;
            turnNumber = 0;
            confirmedPlacements = new List<DraftPlacement>();
            occupiedCells = new bool[rows, cols];
            consecutiveTimeouts = new int[2];
        }

        /// <summary>
        /// Returns true if the specified grid cell is available for placement.
        /// </summary>
        public bool IsCellAvailable(int gridX, int gridZ)
        {
            if (gridX < 0 || gridX >= gridCols) return false;
            if (gridZ < 0 || gridZ >= gridRows) return false;
            return !occupiedCells[gridZ, gridX];
        }

        /// <summary>
        /// Marks a cell as occupied. Called after confirming a placement.
        /// </summary>
        public void OccupyCell(int gridX, int gridZ)
        {
            occupiedCells[gridZ, gridX] = true;
        }

        /// <summary>
        /// Returns true if there are any available cells remaining.
        /// </summary>
        public bool HasAvailableCells()
        {
            for (int z = 0; z < gridRows; z++)
                for (int x = 0; x < gridCols; x++)
                    if (!occupiedCells[z, x]) return true;
            return false;
        }

        /// <summary>
        /// Returns the slots array for the specified player.
        /// </summary>
        public DraftSlot[] GetPlayerSlots(int playerID)
        {
            return playerID == 0 ? player0Slots : player1Slots;
        }

        /// <summary>
        /// Returns true if the specified player has any unconsumed slots remaining.
        /// </summary>
        public bool PlayerHasRemainingNodes(int playerID)
        {
            DraftSlot[] slots = GetPlayerSlots(playerID);
            for (int i = 0; i < slots.Length; i++)
                if (!slots[i].isConsumed) return true;
            return false;
        }

        /// <summary>
        /// Returns true if the draft should end:
        /// both players have placed all nodes, or no valid space remains.
        /// </summary>
        public bool IsDraftFinished()
        {
            if (!HasAvailableCells()) return true;
            if (!PlayerHasRemainingNodes(0) && !PlayerHasRemainingNodes(1)) return true;
            return false;
        }

        /// <summary>
        /// Finds a random available cell. Returns false if none exist.
        /// Uses a simple deterministic scan with offset for variety.
        /// </summary>
        public bool FindRandomAvailableCell(int seed, out int outX, out int outZ)
        {
            // Collect available cells
            List<int> availableX = new List<int>();
            List<int> availableZ = new List<int>();

            for (int z = 0; z < gridRows; z++)
            {
                for (int x = 0; x < gridCols; x++)
                {
                    if (!occupiedCells[z, x])
                    {
                        availableX.Add(x);
                        availableZ.Add(z);
                    }
                }
            }

            if (availableX.Count == 0)
            {
                outX = 0;
                outZ = 0;
                return false;
            }

            // Deterministic pseudo-random selection using seed
            int index = ((seed * 1103515245 + 12345) & 0x7FFFFFFF) % availableX.Count;
            outX = availableX[index];
            outZ = availableZ[index];
            return true;
        }

        /// <summary>
        /// Gets the first unconsumed slot index for a player. -1 if none.
        /// </summary>
        public int GetFirstUnconsumedSlotIndex(int playerID)
        {
            DraftSlot[] slots = GetPlayerSlots(playerID);
            for (int i = 0; i < slots.Length; i++)
                if (!slots[i].isConsumed) return i;
            return -1;
        }
    }

    /// <summary>
    /// Result of the draft phase. Consumed by GameManager to build the final board.
    /// </summary>
    public struct DraftResult
    {
        public DraftPlacement[] placements;
    }
}