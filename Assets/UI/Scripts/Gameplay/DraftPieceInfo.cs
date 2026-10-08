using NodeWar.Simulation;
using NodeWar.Lobby;

namespace NodeWar.UI
{
    /// <summary>
    /// What a draft card says about a district: its name, its monogram, and the
    /// tint that stands in for the icon it has not got.
    ///
    /// WHY THE NAME IS NOT A SWITCH STATEMENT HERE. DraftPickUI hard-codes the
    /// twelve names, and the brief calls that out: Camp.asset once carried the
    /// display name "Watchtower", so the lobby and the draft disagreed about
    /// what the player had picked. The DistrictDefinition assets are the lobby's
    /// source for a district's name, so they are this surface's source too, and
    /// a district whose name is wrong is now wrong in exactly one place.
    ///
    /// The enum name is the fallback, not the answer. Four districts - Farm,
    /// Mine, Village, Forge - are base draft nodes that no loadout slot can
    /// hold, so no DistrictDefinition asset exists for them. Their enum name reads
    /// correctly ("Farm"), and inventing four more assets to hold four strings
    /// that already exist would be the worse trade.
    ///
    /// The tint comes from ItemTint, the same hash the Workshop grid uses, so a
    /// district wears one colour in the loadout and the same colour in the
    /// draft. That correspondence is the whole point of the tint existing.
    /// </summary>
    public static class DraftPieceInfo
    {
        /// <summary>
        /// The lobby ID DraftManager.MapDistrictID reads in the other
        /// direction ("node_rampart"), from the explicit table rather than the
        /// enum member name.
        /// </summary>
        public static string DistrictID(DistrictType type)
        {
            return NodeWar.Lobby.LoadoutTypes.LobbyIdForDistrict(type);
        }

        /// <summary>
        /// The district's display name. Prefers the DistrictDefinition the lobby
        /// shows; falls back to the enum name when no asset defines it.
        /// A definition with a blank displayName is treated as no definition -
        /// an empty card is worse than one named from the enum.
        /// </summary>
        public static string DisplayName(DistrictType type, DistrictDefinition[] definitions)
        {
            if (type == DistrictType.Village) return NodeWar.View.DistrictFallback.Describe(type).Name;
            string id = DistrictID(type);

            if (definitions != null)
            {
                for (int i = 0; i < definitions.Length; i++)
                {
                    if (definitions[i] == null) continue;
                    if (definitions[i].districtID != id) continue;
                    if (string.IsNullOrEmpty(definitions[i].displayName)) break;

                    return definitions[i].displayName;
                }
            }

            return NodeWar.View.DistrictFallback.Describe(type).Name ?? "Unknown district";
        }

        /// <summary>
        /// The single letter on the tile. First letter of the display name, so
        /// it follows whatever the definition says rather than the enum - a
        /// renamed district gets a matching monogram for free.
        /// </summary>
        public static string Monogram(string displayName)
        {
            foreach (DistrictType type in System.Enum.GetValues(typeof(DistrictType)))
            {
                if (!DistrictRoster.IsActive(type)) continue;
                var descriptor = NodeWar.View.DistrictFallback.Describe(type);
                if (descriptor.Name == displayName) return descriptor.Monogram;
            }
            if (string.IsNullOrEmpty(displayName)) return "?";
            return displayName.Substring(0, 1).ToUpperInvariant();
        }

        /// <summary>
        /// The .ui-tile-tint--N class for this district, matching the Workshop.
        /// </summary>
        public static string TintClass(DistrictType type)
        {
            return "ui-tile-tint--" + ItemTint.IndexFor(DistrictID(type));
        }
    }
}
