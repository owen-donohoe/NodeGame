using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Settings: a push page opened from the gear.
    ///
    /// Layout pass. Every row from the prototype is here and moves - sliders
    /// drag, switches flip, Interface size cycles - but no value is read or
    /// saved, and the page says so in a footnote. The settings system is the
    /// next pass: it should extend PlayerProfile's save path rather than add a
    /// second one, and the footnote goes in the same change.
    /// </summary>
    public class SettingsPage : LobbyPushPage
    {
        private static readonly string[] InterfaceSizes = { "Small", "Default", "Large" };

        private readonly Label sizeLabel;
        private int sizeIndex = 1;

        public SettingsPage(VisualTreeAsset layout)
            : base("settings-page", layout, "Settings layout missing - assign SettingsPage.uxml")
        {
            BindSwitchRow("settings-row-colourblind", "settings-colourblind");
            BindSwitchRow("settings-row-motion", "settings-motion");
            BindSwitchRow("settings-row-confirm", "settings-confirm");
            BindSwitchRow("settings-row-haptics", "settings-haptics");
            BindSwitchRow("settings-row-battery", "settings-battery");

            sizeLabel = Root.Q<Label>("settings-size");

            // TODO(settings): Interface size should drive the panel scale.
            Button sizeRow = Root.Q<Button>("settings-row-size");
            if (sizeRow != null)
            {
                sizeRow.clicked += () =>
                {
                    sizeIndex = (sizeIndex + 1) % InterfaceSizes.Length;
                    if (sizeLabel != null) sizeLabel.text = InterfaceSizes[sizeIndex];
                };
            }
        }

        /// <summary>The whole row is the tap target for its switch, as in the prototype.</summary>
        private void BindSwitchRow(string rowName, string switchName)
        {
            Button row = Root.Q<Button>(rowName);
            LobbySwitch toggle = Root.Q<LobbySwitch>(switchName);

            if (row != null && toggle != null) row.clicked += toggle.Flip;
        }
    }
}
