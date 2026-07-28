using UnityEngine;
using UnityEngine.InputSystem;
using NodeWar.Input;

namespace NodeWar.Debugging
{
    /// <summary>
    /// Debug tool: Press Tab to toggle which player the local input controls.
    /// Displays current controlled player in top-left corner.
    /// Allows testing combat and breach with a single keyboard/mouse.
    /// </summary>
    public class DebugPlayerSwitch : MonoBehaviour
    {
        private SelectionSystem selectionSystem;
        private CommandSystem commandSystem;
        private int currentPlayerID = 0;

        // UI
        private GUIStyle labelStyle;
        private bool styleInitialized = false;

        public void Initialize(SelectionSystem selection, CommandSystem command)
        {
            selectionSystem = selection;
            commandSystem = command;
        }

        private void Update()
        {
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
                text = "CONTROLLING: P0 (Blue)";
            }
            else
            {
                labelStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
                text = "CONTROLLING: P1 (Red)";
            }

            GUI.Label(new Rect(10, 10, 400, 40), text, labelStyle);
            GUI.Label(new Rect(10, 40, 400, 30), "[Tab] to switch", GUI.skin.label);
        }
    }
}