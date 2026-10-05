using UnityEngine;
using UnityEngine.InputSystem;
using NodeWar.Input;

namespace NodeWar.Debugging
{
    public class DebugPlayerSwitch : MonoBehaviour
    {
        private SelectionSystem selectionSystem;
        private CommandSystem commandSystem;
        private int currentPlayerID = 0;
        private bool isLocked = false;

        private GUIStyle labelStyle;
        private bool styleInitialized = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private System.Func<bool> playtestAllowed;
        private System.Action suddenDeathNow;
        private bool showMetal, showMagic;
#endif

        public void ConfigurePlaytestDebug(System.Func<bool> allowed, System.Action suddenDeath)
        {
            NodeWar.UI.ResourceVisibility.SetDebugOverrides(false, false, false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            playtestAllowed = allowed;
            suddenDeathNow = suddenDeath;
            showMetal = showMagic = false;
#endif
        }

        public System.Action<int> OnPlayerSwitched;

        public void Initialize(SelectionSystem selection, CommandSystem command)
        {
            selectionSystem = selection;
            commandSystem = command;
        }

        /// <summary>
        /// Lock to a fixed player ID. Used in networked mode.
        /// Tab key does nothing while locked.
        /// </summary>
        public void LockToPlayer(int playerID)
        {
            currentPlayerID = playerID;
            isLocked = true;

            if (selectionSystem != null)
                selectionSystem.SetPlayerID(playerID);
            if (commandSystem != null)
                commandSystem.SetPlayerID(playerID);

            OnPlayerSwitched?.Invoke(playerID);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool allowed = playtestAllowed != null && playtestAllowed();
            if (allowed)
            {
                if (keyboard.f6Key.wasPressedThisFrame) suddenDeathNow?.Invoke();
                if (keyboard.f7Key.wasPressedThisFrame) showMetal = !showMetal;
                if (keyboard.f11Key.wasPressedThisFrame) showMagic = !showMagic;
            }
            NodeWar.UI.ResourceVisibility.SetDebugOverrides(allowed, showMetal, showMagic);
#endif

            if (isLocked) return;

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                TogglePlayer();
            }
        }

        private void TogglePlayer()
        {
            currentPlayerID = 1 - currentPlayerID;

            if (selectionSystem != null)
                selectionSystem.SetPlayerID(currentPlayerID);
            if (commandSystem != null)
                commandSystem.SetPlayerID(currentPlayerID);

            OnPlayerSwitched?.Invoke(currentPlayerID);
        }

        public int GetCurrentPlayerID()
        {
            return currentPlayerID;
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (playtestAllowed != null && playtestAllowed())
                GUI.Label(new Rect(10, Screen.height - 24, 570, 22),
                    "[F6] Sudden death in 5s   [F7] Metal override   [F11] Magic override");
#endif
            // Locked matches already identify both players in the HUD. This
            // debug label has no switch to offer and covers the breach bar.
            if (isLocked) return;

            if (!styleInitialized)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 14;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.alignment = TextAnchor.MiddleRight;
                styleInitialized = true;
            }

            string text;
            if (currentPlayerID == 0)
            {
                labelStyle.normal.textColor = new Color(0.3f, 0.5f, 1f);
                text = isLocked ? "P0 (Blue) [ONLINE]" : "P0 (Blue)";
            }
            else
            {
                labelStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
                text = isLocked ? "P1 (Red) [ONLINE]" : "P1 (Red)";
            }

            GUI.Label(new Rect(Screen.width - 210, 10, 200, 25), text, labelStyle);

            if (!isLocked)
                GUI.Label(new Rect(Screen.width - 210, 30, 200, 20), "[Tab] switch", labelStyle);
        }

        private void OnDisable()
        {
            NodeWar.UI.ResourceVisibility.SetDebugOverrides(false, false, false);
        }
    }
}
