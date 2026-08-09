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
        }

        private void Update()
        {
            if (isLocked) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

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
        }

        public int GetCurrentPlayerID()
        {
            return currentPlayerID;
        }

        private void OnGUI()
        {
            if (!styleInitialized)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 24;
                labelStyle.fontStyle = FontStyle.Bold;
                styleInitialized = true;
            }

            string text;
            if (currentPlayerID == 0)
            {
                labelStyle.normal.textColor = new Color(0.3f, 0.5f, 1f);
                text = isLocked ? "PLAYER 0 (Blue) [ONLINE]" : "CONTROLLING: P0 (Blue)";
            }
            else
            {
                labelStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
                text = isLocked ? "PLAYER 1 (Red) [ONLINE]" : "CONTROLLING: P1 (Red)";
            }

            GUI.Label(new Rect(10, 10, 400, 40), text, labelStyle);

            if (!isLocked)
                GUI.Label(new Rect(10, 40, 400, 30), "[Tab] to switch", GUI.skin.label);
        }
    }
}