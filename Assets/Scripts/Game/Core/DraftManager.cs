using UnityEngine;
using NodeWar.Simulation;
using NodeWar.Config;
using NodeWar.Network;
using System.Collections.Generic;
using static NodeWar.Config.BoardConfig;

namespace NodeWar.Core
{
    /// <summary>
    /// Orchestrates the pre-game node drafting phase.
    /// Created at runtime by GameManager. Runs a turn-based state machine in Update().
    /// Produces a DraftResult on completion and invokes OnDraftComplete.
    /// 
    /// Lifecycle:
    ///   1. GameManager creates GameObject, adds this component
    ///   2. GameManager calls Initialize() with match configuration
    ///   3. DraftManager runs phases: WaitingForReady ? InitialReveal ? ActiveDraft ? Complete
    ///   4. On completion, fires OnDraftComplete with placement results
    ///   5. GameManager destroys this GameObject
    /// 
    /// Networking: uses the same NetworkManager/packet system as gameplay.
    /// Bot matches: bot places after a configurable delay, choosing closest-to-core cells.
    /// </summary>
    public class DraftManager : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Duration of the initial reveal phase (cores dropping in) before turns begin.")]
        [SerializeField] private float initialRevealDuration = 2.0f;
        [Tooltip("Seconds into the bot's turn before it places. Prevents instant feel.")]
        [SerializeField] private float botTurnPlacementDelay = 1.0f;

        [Header("Networking")]
        [Tooltip("Seconds between heartbeat packets during draft.")]
        [SerializeField] private float heartbeatInterval = 0.5f;
        [Tooltip("Seconds without receiving data before declaring opponent disconnected.")]
        [SerializeField] private float disconnectTimeout = 5.0f;

        [Header("Grid Markers")]
        [Tooltip("Y offset for placement grid markers. Slightly below ground to avoid z-fighting with previews.")]
        [SerializeField] private float gridMarkerYOffset = -0.05f;

        // Configuration (set via Initialize)
        private BoardConfig boardConfig;
        private NetworkManager networkManager;
        private int localPlayerID;
        private bool isNetworked;
        private bool isBotMatch;
        private CameraController cameraController;
        private GameObject placementGridCellPrefab;

        // Loadout exchange
        private NodeWar.Lobby.LoadoutData localLoadout;
        private NodeWar.Lobby.LoadoutData remoteLoadout;
        private bool remoteLoadoutReceived;

        // State
        private DraftState draftState;
        private float turnTimer;
        private bool localReady;
        private bool remoteReady;
        private float lastHeartbeatTime;
        private float lastReceiveTime;

        // Reveal
        private float revealTimer;

        // Bot
        private bool botTurnHandled = false;

        // UI
        private NodeWar.UI.DraftUI draftUI;

        // Grid markers
        private List<GameObject> gridMarkers = new List<GameObject>();

        // Events
        public System.Action<DraftResult> OnDraftComplete;
        public System.Action OnDraftDisconnect;

        public DraftState State => draftState;

        // ===== INITIALIZATION =====

        public void Initialize(BoardConfig config, NetworkManager netManager,
                    int playerID, bool networked, bool botMatch,
                    CameraController camController, GameObject gridCellPrefab,
                    NodeWar.Lobby.LoadoutData loadout)
        {
            boardConfig = config;
            networkManager = netManager;
            localPlayerID = playerID;
            isNetworked = networked;
            isBotMatch = botMatch;
            cameraController = camController;
            placementGridCellPrefab = gridCellPrefab;
            localLoadout = loadout;
            remoteLoadout = new NodeWar.Lobby.LoadoutData();
            remoteLoadoutReceived = false;

            float now = Time.time;
            lastHeartbeatTime = now;
            lastReceiveTime = now;

            // Build draft state and mark initial placements as occupied
            draftState = new DraftState(config.Data.gridCols, config.Data.gridRows);

            if (config.Data.initialPlacements != null)
            {
                for (int i = 0; i < config.Data.initialPlacements.Length; i++)
                {
                    var ip = config.Data.initialPlacements[i];
                    draftState.OccupyCell(ip.gridX, ip.gridZ);
                }
            }

            if (cameraController != null)
                cameraController.SetDraftMode(true);

            // Networked matches require ready handshake; local/bot skip straight to reveal
            if (!isNetworked || isBotMatch)
            {
                localReady = true;
                remoteReady = true;
                remoteLoadoutReceived = true;
                // Bot gets no loadout nodes (only base draft nodes)
                BeginInitialReveal();
            }
            else
            {
                draftState.phase = DraftPhase.WaitingForReady;
                localReady = false;
                remoteReady = false;
                remoteLoadoutReceived = false;
                SendDraftReady();
                SendDraftLoadout();
                localReady = true;
            }

            SpawnPlacementGrid();
        }

        public void SetDraftUI(NodeWar.UI.DraftUI ui)
        {
            draftUI = ui;
        }

        // ===== UPDATE LOOP =====

        private void Update()
        {
            if (isNetworked && !isBotMatch)
            {
                ProcessIncomingPackets();
                SendHeartbeatIfNeeded();
                if (CheckDisconnect()) return;
            }

            switch (draftState.phase)
            {
                case DraftPhase.WaitingForReady:
                    if (localReady && remoteReady && (remoteLoadoutReceived || !isNetworked || isBotMatch))
                        BeginInitialReveal();
                    break;
                case DraftPhase.InitialReveal:
                    UpdateInitialReveal();
                    break;
                case DraftPhase.ActiveDraft:
                    UpdateActiveDraft();
                    break;
            }
        }

        // ===== PHASE: INITIAL REVEAL =====

        private void BeginInitialReveal()
        {
            draftState.phase = DraftPhase.InitialReveal;
            revealTimer = 0f;

            // Both loadouts are now known - rebuild slots with full information
            draftState.player0Slots = BuildPlayerSlots(0);
            draftState.player1Slots = BuildPlayerSlots(1);

            if (draftUI != null)
                draftUI.ShowInitialReveal(boardConfig.Data.initialPlacements);
        }

        private void UpdateInitialReveal()
        {
            revealTimer += Time.deltaTime;
            if (revealTimer >= initialRevealDuration)
                BeginActiveDraft();
        }

        // ===== PHASE: ACTIVE DRAFT =====

        private void BeginActiveDraft()
        {
            draftState.phase = DraftPhase.ActiveDraft;
            draftState.currentTurnPlayerID = 0; // P0 always drafts first
            draftState.turnNumber = 0;

            if (draftState.IsDraftFinished())
            {
                CompleteDraft();
                return;
            }

            AdvanceToNextValidTurn();
            turnTimer = boardConfig.draftTurnDuration;

            if (draftUI != null)
                draftUI.SweepIn(draftState, localPlayerID);
        }

        private void UpdateActiveDraft()
        {
            turnTimer -= Time.deltaTime;

            if (draftUI != null)
                draftUI.UpdateTimer(turnTimer, boardConfig.draftTurnDuration);

            bool isMyTurn = (draftState.currentTurnPlayerID == localPlayerID);

            if (isMyTurn)
            {
                if (turnTimer <= 0f)
                    HandleTimeout();
            }
            else if (isBotMatch && !botTurnHandled)
            {
                // Bot places after configured delay into its turn
                float elapsed = boardConfig.draftTurnDuration - turnTimer;
                if (elapsed >= botTurnPlacementDelay)
                {
                    botTurnHandled = true;
                    HandleBotTurn();
                }
            }
            else if (!isBotMatch && !isMyTurn)
            {
                // Waiting for remote — clamp timer display at zero
                if (turnTimer <= 0f)
                    turnTimer = 0f;
            }
        }

        // ===== PLACEMENT LOGIC =====

        /// <summary>
        /// Called by DraftUI when the local player confirms a placement.
        /// </summary>
        public void ConfirmLocalPlacement(int slotIndex, int gridX, int gridZ)
        {
            if (draftState.currentTurnPlayerID != localPlayerID) return;
            if (!draftState.IsCellAvailable(gridX, gridZ)) return;

            DraftSlot[] slots = draftState.GetPlayerSlots(localPlayerID);
            if (slotIndex < 0 || slotIndex >= slots.Length) return;
            if (slots[slotIndex].isConsumed) return;

            ApplyPlacement(localPlayerID, slots[slotIndex].districtType,
                gridX, gridZ, slotIndex, false);

            if (isNetworked && !isBotMatch)
            {
                byte[] packet = DraftSerializer.SerializeDraftPlacement(
                    localPlayerID, (int)slots[slotIndex].districtType,
                    gridX, gridZ, false);
                networkManager.Send(packet);
            }
        }

        private void HandleTimeout()
        {
            int activePlayer = draftState.currentTurnPlayerID;

            int slotIndex = draftState.GetFirstUnconsumedSlotIndex(activePlayer);
            if (slotIndex < 0)
            {
                AdvanceTurn();
                return;
            }

            DraftSlot[] slots = draftState.GetPlayerSlots(activePlayer);
            DistrictType district = slots[slotIndex].districtType;

            // Deterministic random cell selection using turn-based seed
            int seed = draftState.turnNumber * 7919 + activePlayer * 31;
            if (!draftState.FindRandomAvailableCell(seed, out int gridX, out int gridZ))
            {
                CompleteDraft();
                return;
            }

            ApplyPlacement(activePlayer, district, gridX, gridZ, slotIndex, true);

            if (isNetworked && !isBotMatch && activePlayer == localPlayerID)
            {
                byte[] packet = DraftSerializer.SerializeDraftPlacement(
                    localPlayerID, (int)district, gridX, gridZ, true);
                networkManager.Send(packet);
            }
        }

        private void HandleBotTurn()
        {
            int botPlayer = 1 - localPlayerID;
            int slotIndex = draftState.GetFirstUnconsumedSlotIndex(botPlayer);
            if (slotIndex < 0) { AdvanceTurn(); return; }

            DraftSlot[] slots = draftState.GetPlayerSlots(botPlayer);

            // Find bot's core position for proximity heuristic
            int coreX = -1, coreZ = -1;
            if (boardConfig.Data.initialPlacements != null)
            {
                for (int i = 0; i < boardConfig.Data.initialPlacements.Length; i++)
                {
                    if (boardConfig.Data.initialPlacements[i].ownerID == botPlayer)
                    {
                        coreX = boardConfig.Data.initialPlacements[i].gridX;
                        coreZ = boardConfig.Data.initialPlacements[i].gridZ;
                        break;
                    }
                }
            }

            // Pick closest available cell to bot's core (manhattan distance)
            int bestX = -1, bestZ = -1;
            int bestDist = int.MaxValue;
            for (int z = 0; z < draftState.gridRows; z++)
            {
                for (int x = 0; x < draftState.gridCols; x++)
                {
                    if (!draftState.IsCellAvailable(x, z)) continue;
                    int dist = Mathf.Abs(x - coreX) + Mathf.Abs(z - coreZ);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestX = x;
                        bestZ = z;
                    }
                }
            }

            if (bestX < 0)
            {
                CompleteDraft();
                return;
            }

            ApplyPlacement(botPlayer, slots[slotIndex].districtType,
                bestX, bestZ, slotIndex, false);
        }

        /// <summary>
        /// Central placement application. Called for local, remote, bot, and timeout placements.
        /// Updates state, consumes slot, checks timeout disconnect rules, notifies UI, advances turn.
        /// </summary>
        private void ApplyPlacement(int playerID, DistrictType district,
            int gridX, int gridZ, int slotIndex, bool wasTimeout)
        {
            draftState.OccupyCell(gridX, gridZ);

            DraftPlacement placement = new DraftPlacement
            {
                playerID = playerID,
                districtType = district,
                gridX = gridX,
                gridZ = gridZ,
                wasTimeout = wasTimeout
            };
            draftState.confirmedPlacements.Add(placement);

            // Consume the used slot
            if (playerID == 0)
                draftState.player0Slots[slotIndex].isConsumed = true;
            else
                draftState.player1Slots[slotIndex].isConsumed = true;

            if (wasTimeout)
            {
                draftState.consecutiveTimeouts[playerID]++;

                // Only disconnect for consecutive timeouts in networked non-bot matches
                if (isNetworked && !isBotMatch &&
                    draftState.consecutiveTimeouts[playerID] >= boardConfig.maxConsecutiveTimeouts)
                {
                    OnDraftDisconnect?.Invoke();
                    enabled = false;
                    return;
                }
            }
            else
            {
                draftState.consecutiveTimeouts[playerID] = 0;
            }

            if (draftUI != null)
                draftUI.OnPlacementConfirmed(placement);

            AdvanceTurn();
        }

        // ===== TURN MANAGEMENT =====

        private void AdvanceTurn()
        {
            draftState.turnNumber++;
            botTurnHandled = false;

            if (draftState.IsDraftFinished())
            {
                CompleteDraft();
                return;
            }

            draftState.currentTurnPlayerID = 1 - draftState.currentTurnPlayerID;
            AdvanceToNextValidTurn();
            turnTimer = boardConfig.draftTurnDuration;

            if (draftUI != null)
                draftUI.OnTurnChanged(draftState, localPlayerID);
        }

        /// <summary>
        /// Skips players with no remaining nodes. Completes draft if neither has nodes.
        /// </summary>
        private void AdvanceToNextValidTurn()
        {
            for (int i = 0; i < 2; i++)
            {
                if (draftState.PlayerHasRemainingNodes(draftState.currentTurnPlayerID))
                    return;
                draftState.currentTurnPlayerID = 1 - draftState.currentTurnPlayerID;
            }
            CompleteDraft();
        }

        private void CompleteDraft()
        {
            draftState.phase = DraftPhase.Complete;

            DraftResult result = new DraftResult
            {
                placements = draftState.confirmedPlacements.ToArray()
            };

            if (draftUI != null)
                draftUI.SweepOut();

            // Stop this component before handing off. ReceiveAll() drains the
            // shared inbound queue destructively, so if this Update() keeps
            // running alongside LockstepRunner (created by the OnDraftComplete
            // handler) whichever runs first that frame swallows the other's
            // packets -- and TickInput/Heartbeat are not handled here, so they
            // would be silently discarded until LockstepRunner times out.
            enabled = false;

            OnDraftComplete?.Invoke(result);
        }

        // ===== NETWORKING =====

        private void ProcessIncomingPackets()
        {
            if (networkManager == null) return;

            byte[][] packets = networkManager.ReceiveAll();
            for (int i = 0; i < packets.Length; i++)
            {
                if (packets[i] == null || packets[i].Length == 0) continue;
                lastReceiveTime = Time.time;

                PacketType type = InputSerializer.ReadPacketType(packets[i]);

                switch (type)
                {
                    case PacketType.DraftReady:
                        remoteReady = true;
                        break;
                    case PacketType.DraftPlacement:
                        HandleRemotePlacement(packets[i]);
                        break;
                    case PacketType.Heartbeat:
                        break;
                    case PacketType.DraftLoadout:
                        HandleRemoteLoadout(packets[i]);
                        break;
                }
            }
        }

        private void HandleRemoteLoadout(byte[] data)
        {
            DraftSerializer.DeserializeDraftLoadout(data, out int playerID, out NodeWar.Lobby.LoadoutData loadout);
            if (playerID == localPlayerID) return; // ignore our own loadout echoed back
            remoteLoadout = loadout;
            remoteLoadoutReceived = true;
        }
        private void SendDraftLoadout()
        {
            if (networkManager == null) return;
            byte[] packet = DraftSerializer.SerializeDraftLoadout(localPlayerID, localLoadout);
            networkManager.Send(packet);
        }

        private void HandleRemotePlacement(byte[] data)
        {
            DraftSerializer.DeserializeDraftPlacement(data,
                out int playerID, out int districtType, out int gridX, out int gridZ, out bool wasTimeout);

            if (playerID != draftState.currentTurnPlayerID) return;
            if (playerID == localPlayerID) return;

            // Find first unconsumed slot matching the district type
            DraftSlot[] slots = draftState.GetPlayerSlots(playerID);
            int slotIndex = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].isConsumed && (int)slots[i].districtType == districtType)
                {
                    slotIndex = i;
                    break;
                }
            }

            if (slotIndex < 0) return;
            if (!draftState.IsCellAvailable(gridX, gridZ)) return;

            ApplyPlacement(playerID, (DistrictType)districtType,
                gridX, gridZ, slotIndex, wasTimeout);
        }

        private void SendDraftReady()
        {
            if (networkManager == null) return;
            byte[] packet = DraftSerializer.SerializeDraftReady(localPlayerID);
            networkManager.Send(packet);
        }

        private void SendHeartbeatIfNeeded()
        {
            if (Time.time - lastHeartbeatTime < heartbeatInterval) return;
            if (networkManager == null) return;
            networkManager.Send(InputSerializer.SerializeHeartbeat());
            lastHeartbeatTime = Time.time;
        }

        private bool CheckDisconnect()
        {
            if (Time.time - lastReceiveTime > disconnectTimeout)
            {
                Debug.LogError("[DraftManager] Opponent disconnected during draft.");
                OnDraftDisconnect?.Invoke();
                enabled = false;
                return true;
            }
            return false;
        }

        // ===== PLACEMENT GRID =====

        private void SpawnPlacementGrid()
        {
            if (placementGridCellPrefab == null) return;

            for (int z = 0; z < boardConfig.Data.gridRows; z++)
            {
                for (int x = 0; x < boardConfig.Data.gridCols; x++)
                {
                    if (draftState.occupiedCells[z, x]) continue;

                    Vector3 pos = GridToWorld(x, z);
                    pos.y = gridMarkerYOffset;

                    GameObject marker = Instantiate(placementGridCellPrefab);
                    marker.transform.position = pos;
                    gridMarkers.Add(marker);
                }
            }
        }

        private void DestroyPlacementGrid()
        {
            for (int i = 0; i < gridMarkers.Count; i++)
            {
                if (gridMarkers[i] != null)
                    Destroy(gridMarkers[i]);
            }
            gridMarkers.Clear();
        }

        // ===== SLOT BUILDING =====

        private DraftSlot[] BuildPlayerSlots(int playerID)
        {
            List<DraftSlot> slots = new List<DraftSlot>();

            DraftNodeEntry[] baseNodes = (playerID == 0)
                ? boardConfig.baseDraftNodesP0
                : boardConfig.baseDraftNodesP1;

            if (baseNodes != null)
            {
                for (int i = 0; i < baseNodes.Length; i++)
                {
                    if (baseNodes[i].districtType == DistrictType.None) continue;
                    slots.Add(new DraftSlot
                    {
                        districtType = baseNodes[i].districtType,
                        isConsumed = false,
                        isFromLoadout = false
                    });
                }
            }
            else
            {
                Debug.LogWarning("[DraftManager] baseDraftNodes null for P" + playerID);
            }

            // Add loadout nodes
            if (isBotMatch && playerID != localPlayerID)
            {
                // Bot player: use nodes configured directly in BoardConfig
                if (boardConfig.botLoadoutNodes != null)
                {
                    for (int i = 0; i < boardConfig.botLoadoutNodes.Length; i++)
                    {
                        if (boardConfig.botLoadoutNodes[i].districtType == DistrictType.None) continue;
                        slots.Add(new DraftSlot
                        {
                            districtType = boardConfig.botLoadoutNodes[i].districtType,
                            isConsumed = false,
                            isFromLoadout = true
                        });
                    }
                }
            }
            else
            {
                // Human player: use lobby loadout
                NodeWar.Lobby.LoadoutData loadout =
                    NodeWar.Lobby.LoadoutData.Normalized(GetLoadoutForPlayer(playerID));

                for (int i = 0; i < loadout.nodeIDs.Length; i++)
                    AddLoadoutNode(slots, loadout.nodeIDs[i]);
            }

            return slots.ToArray();
        }

        private NodeWar.Lobby.LoadoutData GetLoadoutForPlayer(int playerID)
        {
            if (playerID == localPlayerID)
                return localLoadout;
            else
                return remoteLoadout;
        }

        private void AddLoadoutNode(List<DraftSlot> slots, string nodeID)
        {
            if (string.IsNullOrEmpty(nodeID)) return;

            DistrictType type = MapNodeIDToDistrict(nodeID);
            if (type == DistrictType.None) return;

            slots.Add(new DraftSlot
            {
                districtType = type,
                isConsumed = false,
                isFromLoadout = true
            });
        }

        /// <summary>
        /// Convention: nodeID format is "node_[lowercase district name]".
        /// Informal string matching — a registry would be more robust long-term.
        /// </summary>
        private DistrictType MapNodeIDToDistrict(string nodeID)
        {
            if (nodeID == null) return DistrictType.None;
            string lower = nodeID.ToLower();

            if (lower.Contains("farm")) return DistrictType.Farm;
            if (lower.Contains("mine")) return DistrictType.Mine;
            if (lower.Contains("village")) return DistrictType.Village;
            if (lower.Contains("barracks")) return DistrictType.Barracks;
            if (lower.Contains("forge")) return DistrictType.Forge;
            if (lower.Contains("camp")) return DistrictType.Camp;
            if (lower.Contains("shrine")) return DistrictType.Shrine;
            if (lower.Contains("arsenal")) return DistrictType.Arsenal;
            if (lower.Contains("sanctuary")) return DistrictType.Sanctuary;
            if (lower.Contains("watchtower")) return DistrictType.Watchtower;
            if (lower.Contains("rampart")) return DistrictType.Rampart;
            if (lower.Contains("market")) return DistrictType.Market;

            return DistrictType.None;
        }

        // ===== PUBLIC API FOR UI =====

        public bool IsLocalPlayerTurn()
        {
            return draftState.phase == DraftPhase.ActiveDraft &&
                   draftState.currentTurnPlayerID == localPlayerID;
        }

        public Vector3 GridToWorld(int gridX, int gridZ)
        {
            return new Vector3(
                gridX * boardConfig.nodeScale,
                0f,
                gridZ * boardConfig.nodeScale);
        }

        /// <summary>
        /// Snaps world position to nearest grid cell. Returns false if out of bounds.
        /// </summary>
        public bool WorldToGrid(Vector3 worldPos, out int gridX, out int gridZ)
        {
            gridX = Mathf.RoundToInt(worldPos.x / boardConfig.nodeScale);
            gridZ = Mathf.RoundToInt(worldPos.z / boardConfig.nodeScale);

            return gridX >= 0 && gridX < boardConfig.Data.gridCols &&
                   gridZ >= 0 && gridZ < boardConfig.Data.gridRows;
        }

        public float NodeScale => boardConfig.nodeScale;
        public NodeWar.Lobby.LoadoutData GetRemoteLoadout() => remoteLoadout;
        public NodeWar.Lobby.LoadoutData GetLocalLoadout() => localLoadout;
    }
}