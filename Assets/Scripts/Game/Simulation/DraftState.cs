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
    public struct DraftPick
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
    ///
    /// Where a district may go is never decided here. Every path that places or
    /// previews one - the local confirm, a received packet, a parked piece the
    /// clock confirms, the timeout's own choice, the bot, the highlight - asks
    /// <see cref="PlacementLegality"/> through <see cref="CanPlace"/>, so the
    /// paths cannot disagree about the map. Whose turn it is and which pick is
    /// being spent are checked by the callers around it.
    /// </summary>
    public class DraftState
    {
        public DraftPhase phase;
        public int currentTurnPlayerID;
        public int turnNumber;

        public DraftPick[] player0Picks;
        public DraftPick[] player1Picks;

        public List<DraftPlacement> confirmedPlacements;

        /// <summary>The board being drafted on. Immutable for the draft's life.</summary>
        public BoardConfigData board;

        /// <summary>Row-major (cell = z * gridCols + x), true where a district already stands.</summary>
        public bool[] occupied;

        public int gridCols;
        public int gridRows;

        // Timeout tracking per player
        public int[] consecutiveTimeouts;

        /// <summary>
        /// A draft on <paramref name="board"/>, with its fixed placements (the Cores)
        /// already standing.
        /// </summary>
        public DraftState(BoardConfigData board)
        {
            this.board = board;
            gridCols = board.gridCols;
            gridRows = board.gridRows;
            phase = DraftPhase.WaitingForReady;
            currentTurnPlayerID = 0;
            turnNumber = 0;
            confirmedPlacements = new List<DraftPlacement>();
            occupied = new bool[gridCols * gridRows];
            consecutiveTimeouts = new int[2];

            if (board.initialPlacements != null)
                for (int i = 0; i < board.initialPlacements.Length; i++)
                    OccupyCell(board.initialPlacements[i].gridX, board.initialPlacements[i].gridZ);
        }

        // ===== LEGALITY =====

        /// <summary>Marks a cell as occupied. Called after confirming a placement.</summary>
        public void OccupyCell(int gridX, int gridZ)
        {
            if (gridX < 0 || gridX >= gridCols || gridZ < 0 || gridZ >= gridRows) return;
            occupied[gridZ * gridCols + gridX] = true;
        }

        /// <summary>May this district be built here now? The one spatial rule.</summary>
        public bool CanPlace(DistrictType district, int gridX, int gridZ)
        {
            return PlacementLegality.CanPlace(board, occupied, district, gridX, gridZ);
        }

        /// <summary>Does the district have any legal cell left on the board?</summary>
        public bool HasLegalCell(DistrictType district)
        {
            for (int z = 0; z < gridRows; z++)
                for (int x = 0; x < gridCols; x++)
                    if (CanPlace(district, x, z)) return true;
            return false;
        }

        /// <summary>
        /// Every cell the district may be placed on, row-major: the highlight, and the
        /// set the timeout and the bot choose from.
        /// </summary>
        public bool[] LegalCells(DistrictType district)
        {
            bool[] legal = new bool[gridCols * gridRows];
            for (int z = 0; z < gridRows; z++)
                for (int x = 0; x < gridCols; x++)
                    legal[z * gridCols + x] = CanPlace(district, x, z);
            return legal;
        }

        // ===== PICKS =====

        public DraftPick[] GetPlayerPicks(int playerID)
        {
            return playerID == 0 ? player0Picks : player1Picks;
        }

        /// <summary>
        /// A pick can be played if it is unspent and its district has somewhere left to
        /// stand. A pick with nowhere to go (a Pier once the pier slot is taken) is
        /// skipped, not forced: it stays in the hand, unspent and unrefunded, and costs
        /// its player nothing, not even a timeout.
        /// </summary>
        public bool IsPlayable(DraftPick pick)
        {
            return !pick.isConsumed && HasLegalCell(pick.districtType);
        }

        public bool PlayerHasPlayablePick(int playerID)
        {
            return GetLowestPlayablePickIndex(playerID) >= 0;
        }

        /// <summary>The lowest-index pick the player can play, or -1.</summary>
        public int GetLowestPlayablePickIndex(int playerID)
        {
            DraftPick[] picks = GetPlayerPicks(playerID);
            if (picks == null) return -1;
            for (int i = 0; i < picks.Length; i++)
                if (IsPlayable(picks[i])) return i;
            return -1;
        }

        /// <summary>
        /// The draft ends when neither player has a playable pick. No replacement
        /// district is invented for a pick that could not be placed.
        /// </summary>
        public bool IsDraftFinished()
        {
            return !PlayerHasPlayablePick(0) && !PlayerHasPlayablePick(1);
        }

        // ===== ENTRY PATHS =====
        // Each returns the pick it would spend, or -1 and changes nothing.

        /// <summary>The local player confirms pick <paramref name="slotIndex"/> at a cell.</summary>
        public int AcceptLocal(int playerID, int slotIndex, int gridX, int gridZ)
        {
            DraftPick[] picks = GetPlayerPicks(playerID);
            if (picks == null || slotIndex < 0 || slotIndex >= picks.Length) return -1;
            if (picks[slotIndex].isConsumed) return -1;
            return CanPlace(picks[slotIndex].districtType, gridX, gridZ) ? slotIndex : -1;
        }

        /// <summary>
        /// A placement packet names a district, not a pick: spend the first unspent pick
        /// of that type, if the cell is legal for it. A refused packet spends nothing.
        /// </summary>
        public int AcceptReceived(int playerID, DistrictType district, int gridX, int gridZ)
        {
            DraftPick[] picks = GetPlayerPicks(playerID);
            if (picks == null) return -1;
            for (int i = 0; i < picks.Length; i++)
            {
                if (picks[i].isConsumed || picks[i].districtType != district) continue;
                return CanPlace(district, gridX, gridZ) ? i : -1;
            }
            return -1;
        }

        /// <summary>
        /// What happens when the turn clock runs out. A piece the player parked on a cell
        /// is taken as it stands if it is still legal. Otherwise the lowest playable pick
        /// goes to a legal cell chosen from a seed derived only from replicated state
        /// (turn number and player), so it is never random in a way the peers could
        /// disagree about, and never lands on water or ocean. False when the player has
        /// nothing playable.
        /// </summary>
        public bool ChooseTimeout(int playerID, int parkedSlot, int parkedX, int parkedZ,
            out int slotIndex, out int gridX, out int gridZ)
        {
            if (AcceptLocal(playerID, parkedSlot, parkedX, parkedZ) >= 0)
            {
                slotIndex = parkedSlot;
                gridX = parkedX;
                gridZ = parkedZ;
                return true;
            }

            slotIndex = GetLowestPlayablePickIndex(playerID);
            gridX = -1;
            gridZ = -1;
            if (slotIndex < 0) return false;

            DistrictType district = GetPlayerPicks(playerID)[slotIndex].districtType;
            int seed = turnNumber * 7919 + playerID * 31;
            return FindRandomLegalCell(seed, district, out gridX, out gridZ);
        }

        // ===== APPLYING =====

        /// <summary>
        /// Records a placement that has already been accepted: occupies the cell, spends
        /// the pick, and counts or clears the player's consecutive timeouts.
        /// </summary>
        public DraftPlacement Apply(int playerID, int slotIndex, int gridX, int gridZ, bool wasTimeout)
        {
            DraftPick[] picks = GetPlayerPicks(playerID);
            DraftPlacement placement = new DraftPlacement
            {
                playerID = playerID,
                districtType = picks[slotIndex].districtType,
                gridX = gridX,
                gridZ = gridZ,
                wasTimeout = wasTimeout
            };

            OccupyCell(gridX, gridZ);
            confirmedPlacements.Add(placement);
            picks[slotIndex].isConsumed = true;

            if (wasTimeout) consecutiveTimeouts[playerID]++;
            else consecutiveTimeouts[playerID] = 0;
            return placement;
        }

        /// <summary>
        /// From the current player, moves the turn to the first player with a playable
        /// pick, so a player with nothing to place is passed without a timeout. False
        /// when neither has one, which is the end of the draft.
        /// </summary>
        public bool AdvanceToNextValidTurn()
        {
            for (int i = 0; i < 2; i++)
            {
                if (PlayerHasPlayablePick(currentTurnPlayerID)) return true;
                currentTurnPlayerID = 1 - currentTurnPlayerID;
            }
            return false;
        }

        // ===== CHOOSING A CELL =====

        /// <summary>
        /// Picks a legal cell for the district from a seed. Deterministic: the same seed
        /// on the same state is the same cell. False when there is none.
        /// </summary>
        public bool FindRandomLegalCell(int seed, DistrictType district, out int outX, out int outZ)
        {
            bool[] legal = LegalCells(district);
            int count = 0;
            for (int i = 0; i < legal.Length; i++) if (legal[i]) count++;
            if (count == 0)
            {
                outX = 0;
                outZ = 0;
                return false;
            }

            // Deterministic pseudo-random selection using seed.
            int index = ((seed * 1103515245 + 12345) & 0x7FFFFFFF) % count;
            for (int i = 0; i < legal.Length; i++)
            {
                if (!legal[i]) continue;
                if (index-- == 0)
                {
                    outX = i % gridCols;
                    outZ = i / gridCols;
                    return true;
                }
            }
            outX = 0;
            outZ = 0;
            return false;
        }

        /// <summary>
        /// The legal cell nearest (Manhattan) to a point, the lower cell index winning a
        /// tie. How the bot, and the testing draft, choose.
        /// </summary>
        public bool FindNearestLegalCell(DistrictType district, int fromX, int fromZ, out int outX, out int outZ)
        {
            int best = int.MaxValue;
            outX = -1;
            outZ = -1;
            for (int z = 0; z < gridRows; z++)
                for (int x = 0; x < gridCols; x++)
                {
                    if (!CanPlace(district, x, z)) continue;
                    int distance = System.Math.Abs(x - fromX) + System.Math.Abs(z - fromZ);
                    if (distance < best)
                    {
                        best = distance;
                        outX = x;
                        outZ = z;
                    }
                }
            return outX >= 0;
        }
    }

    /// <summary>
    /// A draft decided without anyone playing it, for the skip-draft testing mode: the
    /// same legality, the same picks, no randomness.
    /// </summary>
    public static class DraftPlanner
    {
        /// <summary>
        /// Both players' base picks, P0 first, alternating, each on the legal cell nearest
        /// their own Core. A pick with no legal cell is skipped.
        /// </summary>
        public static DraftPlacement[] TestingPlacements(BoardConfigData board)
        {
            if (!MapAuthoringRules.ValidateBoard(board, out string error))
                throw new System.ArgumentException(error, nameof(board));

            var state = new DraftState(board)
            {
                player0Picks = PicksOf(board.baseDraftDistrictsP0),
                player1Picks = PicksOf(board.baseDraftDistrictsP1),
                phase = DraftPhase.ActiveDraft
            };
            BoardConfigData.InitialDistrictPlacement[] cores =
            {
                MapAuthoringRules.HighCore(board), MapAuthoringRules.LowCore(board)
            };

            while (state.AdvanceToNextValidTurn())
            {
                int player = state.currentTurnPlayerID;
                int slot = state.GetLowestPlayablePickIndex(player);
                DistrictType district = state.GetPlayerPicks(player)[slot].districtType;
                if (!state.FindNearestLegalCell(district, cores[player].gridX, cores[player].gridZ, out int x, out int z))
                    break;
                state.Apply(player, slot, x, z, false);
                state.turnNumber++;
                state.currentTurnPlayerID = 1 - player;
            }
            return state.confirmedPlacements.ToArray();
        }

        private static DraftPick[] PicksOf(DistrictType[] pool)
        {
            var picks = new DraftPick[pool == null ? 0 : pool.Length];
            for (int i = 0; i < picks.Length; i++) picks[i] = new DraftPick { districtType = pool[i] };
            return picks;
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
