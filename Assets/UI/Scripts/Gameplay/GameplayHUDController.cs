using UnityEngine;
using UnityEngine.UIElements;
using NodeWar.Simulation;
using NodeWar.Debugging;

// SafeAreaBinder lives in NodeWar.Lobby because that is where it was first
// needed. It is a general utility with nothing lobby-specific in it, and moving
// it is worth doing once the migration is finished and the namespaces settle -
// not mid-flight, while both stacks are live.
using NodeWar.Lobby;

namespace NodeWar.UI
{
    /// <summary>
    /// The UI Toolkit in-match HUD. Counterpart to HUDManager, and live only
    /// when GameManager.useUIToolkitHUD is on - the same one-checkbox switch the
    /// lobby migration used, for the same reason: the old HUD stays one tick
    /// away for as long as the new one is unproven.
    ///
    /// Namespace NodeWar.UI rather than NodeWar.Lobby, unlike everything else
    /// under Assets/UI/Scripts, because this is gameplay UI and belongs beside
    /// HUDManager in the layer it replaces. The folder says which stack it is
    /// in; the namespace says which layer.
    ///
    /// WHAT IT SHOWS is settled, not preference: three resources and both
    /// breach bars are always visible, and only the villager count collapses.
    /// Breach is the win condition, so it is first and it never hides.
    ///
    /// READ-ONLY, ABSOLUTELY. It reads SimulationState and writes nothing -
    /// not through GameSimulation, not through CommandProcessor. The four
    /// commands belong to the node panel in S7, and they will go through
    /// InputBuffer like everything else. See .claude/rules/view-ui.md.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameplayHUDController : MonoBehaviour
    {
        [Tooltip("The HUD layout. Assign GameplayHUD.uxml.")]
        [SerializeField] private VisualTreeAsset hudLayout;

        [Tooltip("How long the bump on a changed number lasts. Matches HUD.uss.")]
        [SerializeField] private long bumpMilliseconds = 180;

        private UIDocument document;
        private SafeAreaBinder safeArea;

        private SimulationState state;
        private DebugPlayerSwitch playerSwitch;
        private int breachThreshold = 3;
        private bool initialized;

        private Label foodLabel;
        private Label materialsLabel;
        private Label metalLabel;

        private VisualElement breachFillP0;
        private VisualElement breachFillP1;
        private Label breachCountP0;
        private Label breachCountP1;

        private Button villagerToggle;
        private VisualElement villagerPanel;
        private Label villagersP0;
        private Label villagersP1;
        private bool villagersOpen;

        // Totals never change during a match - villagers are consumed, not
        // created - so they are counted once rather than every frame.
        private int totalP0;
        private int totalP1;

        // Change detection, so the bump fires on a real change rather than
        // every frame, and so no label is rewritten for nothing.
        private int lastControlledPID = -1;
        private int lastFood = int.MinValue;
        private int lastMaterials = int.MinValue;
        private int lastMetal = int.MinValue;
        private int lastBreachP0 = -1;
        private int lastBreachP1 = -1;
        private int lastAliveP0 = -1;
        private int lastAliveP1 = -1;

        private void OnEnable()
        {
            document = GetComponent<UIDocument>();

            if (document.panelSettings == null)
            {
                Debug.LogError("[HUD] UIDocument has no PanelSettings; nothing will render. " +
                               "Run Tools > Node War > Set Up UI Toolkit HUD.");
                return;
            }

            VisualElement root = document.rootVisualElement;
            if (root == null) return;

            root.Clear();

            VisualTreeAsset layout = hudLayout != null ? hudLayout : document.visualTreeAsset;
            if (layout == null)
            {
                Debug.LogError("[HUD] No GameplayHUD.uxml assigned, on either this component " +
                               "or the UIDocument.");
                return;
            }

            layout.CloneTree(root);

            // The panel covers the whole screen and the board is underneath it.
            // Without this the HUD eats every tap before the game sees one.
            root.pickingMode = PickingMode.Ignore;

            Bind(root);
        }

        private void OnDisable()
        {
            safeArea = null;
            initialized = false;
        }

        /// <summary>
        /// Called by GameManager once the simulation exists. Mirrors
        /// HUDManager.Initialize, deliberately - the two are interchangeable
        /// and GameManager should not care which it got.
        /// </summary>
        public void Initialize(SimulationState simulationState, DebugPlayerSwitch debugSwitch,
                               int breachThresholdValue)
        {
            state = simulationState;
            playerSwitch = debugSwitch;
            breachThreshold = breachThresholdValue > 0 ? breachThresholdValue : 1;

            CountVillagerTotals();

            initialized = true;

            RefreshAll(true);
        }

        private void Update()
        {
            if (safeArea != null) safeArea.Update();

            if (!initialized || state == null) return;

            RefreshAll(false);
        }

        // ===== BINDING =====

        private void Bind(VisualElement root)
        {
            VisualElement safeAreaElement = root.Q<VisualElement>("hud-safe-area");
            if (safeAreaElement != null) safeArea = new SafeAreaBinder(safeAreaElement);

            foodLabel = root.Q<Label>("hud-food");
            materialsLabel = root.Q<Label>("hud-materials");
            metalLabel = root.Q<Label>("hud-metal");

            breachFillP0 = root.Q<VisualElement>("hud-breach-fill-p0");
            breachFillP1 = root.Q<VisualElement>("hud-breach-fill-p1");
            breachCountP0 = root.Q<Label>("hud-breach-count-p0");
            breachCountP1 = root.Q<Label>("hud-breach-count-p1");

            villagerToggle = root.Q<Button>("hud-villager-toggle");
            villagerPanel = root.Q<VisualElement>("hud-villagers");
            villagersP0 = root.Q<Label>("hud-villagers-p0");
            villagersP1 = root.Q<Label>("hud-villagers-p1");

            if (villagerToggle != null)
                villagerToggle.clicked += ToggleVillagers;

            ApplyVillagerPanelState();
        }

        private void ToggleVillagers()
        {
            villagersOpen = !villagersOpen;
            ApplyVillagerPanelState();
        }

        private void ApplyVillagerPanelState()
        {
            if (villagerPanel != null)
                villagerPanel.EnableInClassList("hud__villagers--open", villagersOpen);

            if (villagerToggle != null)
                villagerToggle.EnableInClassList("hud__villager-toggle--open", villagersOpen);
        }

        // ===== REFRESH =====

        private void RefreshAll(bool snap)
        {
            RefreshResources(snap);
            RefreshBreaches(snap);
            RefreshVillagers();
        }

        /// <summary>
        /// Resources belong to whichever player is being controlled, which the
        /// debug switch can change mid-match. A switch snaps rather than bumps:
        /// the numbers did not change, the player looking at them did, and
        /// flashing three counters would report an event that never happened.
        /// </summary>
        private void RefreshResources(bool snap)
        {
            int pid = playerSwitch != null ? playerSwitch.GetCurrentPlayerID() : 0;
            if (pid < 0 || pid >= state.players.Length) pid = 0;

            bool switched = pid != lastControlledPID;
            lastControlledPID = pid;

            PlayerData player = state.players[pid];
            bool quiet = snap || switched;

            SetNumber(foodLabel, player.food, ref lastFood, quiet);
            SetNumber(materialsLabel, player.materials, ref lastMaterials, quiet);
            SetNumber(metalLabel, player.metal, ref lastMetal, quiet);
        }

        private void SetNumber(Label label, int value, ref int last, bool quiet)
        {
            if (value == last) return;

            bool changed = last != int.MinValue && !quiet;
            last = value;

            if (label == null) return;

            label.text = value.ToString();

            if (changed) Bump(label);
        }

        /// <summary>
        /// Adds the bump class, then takes it off again. USS transitions do the
        /// rest, so there is no per-frame animation code and nothing to kill on
        /// destroy - unlike the DOTween chains in WheelDisplay and
        /// BreachDisplay, which both need an OnDestroy to stay safe.
        /// </summary>
        private void Bump(VisualElement element)
        {
            element.AddToClassList("hud__res-value--bump");
            element.schedule
                   .Execute(() => element.RemoveFromClassList("hud__res-value--bump"))
                   .StartingIn(bumpMilliseconds);
        }

        private void RefreshBreaches(bool snap)
        {
            RefreshBreach(0, state.players[0].breachCount, breachFillP0, breachCountP0,
                          ref lastBreachP0, snap);
            RefreshBreach(1, state.players[1].breachCount, breachFillP1, breachCountP1,
                          ref lastBreachP1, snap);
        }

        private void RefreshBreach(int playerID, int breachCount, VisualElement fill, Label count,
                                   ref int last, bool snap)
        {
            if (breachCount == last) return;
            last = breachCount;

            // Remaining wall, not damage taken. Clamped because a breach count
            // past the threshold is a won match still being rendered for a
            // frame, not a reason to draw a negative-width bar.
            int remaining = breachThreshold - breachCount;
            if (remaining < 0) remaining = 0;

            float fraction = remaining / (float)breachThreshold;

            if (fill != null) fill.style.width = Length.Percent(fraction * 100f);
            if (count != null) count.text = breachCount + "/" + breachThreshold;
        }

        /// <summary>
        /// Alive villagers per player. Counted every frame because there is no
        /// event to hang it on - the simulation consumes a villager inside
        /// SimulateTick and nothing announces it - but the labels are only
        /// rewritten when a count actually moves.
        /// </summary>
        private void RefreshVillagers()
        {
            int aliveP0 = 0;
            int aliveP1 = 0;

            for (int i = 0; i < state.villagers.Length; i++)
            {
                if (state.villagers[i].isConsumed) continue;

                if (state.villagers[i].ownerID == 0) aliveP0++;
                else aliveP1++;
            }

            if (aliveP0 != lastAliveP0)
            {
                lastAliveP0 = aliveP0;
                if (villagersP0 != null) villagersP0.text = aliveP0 + " / " + totalP0;
            }

            if (aliveP1 != lastAliveP1)
            {
                lastAliveP1 = aliveP1;
                if (villagersP1 != null) villagersP1.text = aliveP1 + " / " + totalP1;
            }

            // Collapsed, the button still carries the controlled player's own
            // count - a tap should reveal detail, not the only number worth
            // having.
            if (villagerToggle != null)
                villagerToggle.text = (lastControlledPID == 1 ? aliveP1 : aliveP0).ToString();
        }

        /// <summary>
        /// Per-player totals, taken once. The old HUD prints "/25" as a literal;
        /// counting the array instead means a board with a different villager
        /// count does not quietly display a lie.
        /// </summary>
        private void CountVillagerTotals()
        {
            totalP0 = 0;
            totalP1 = 0;

            if (state == null || state.villagers == null) return;

            for (int i = 0; i < state.villagers.Length; i++)
            {
                if (state.villagers[i].ownerID == 0) totalP0++;
                else totalP1++;
            }
        }
    }
}
