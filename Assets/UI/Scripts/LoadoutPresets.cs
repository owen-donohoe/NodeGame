using System;

namespace NodeWar.Lobby
{
    /// <summary>Local chosen-slot presets; equipment remains inventory-owned.</summary>
    public static class LoadoutPresets
    {
        public const int Count = 3;

        public static bool Migrate(ref PlayerProfileData profile)
        {
            if (profile.loadoutPresets != null && profile.loadoutPresets.Length == Count)
            {
                bool changed = false;
                for (int i = 0; i < Count; i++)
                {
                    var saved = profile.loadoutPresets[i];
                    var normalized = LoadoutRecord.From(saved.ToLoadout());
                    if (!NodeWar.Backend.DistrictMigration.Equal(saved.suitIDs, normalized.suitIDs) ||
                        !NodeWar.Backend.DistrictMigration.Equal(saved.nodeIDs, normalized.nodeIDs))
                    { profile.loadoutPresets[i] = normalized; changed = true; }
                }
                changed |= profile.activeLoadoutPreset != Clamp(profile.activeLoadoutPreset);
                profile.activeLoadoutPreset = Clamp(profile.activeLoadoutPreset);
                return changed;
            }
            profile.loadoutPresets = new LoadoutRecord[Count];
            for (int i = 0; i < Count; i++)
                profile.loadoutPresets[i] = LoadoutRecord.From(i == 0 ? profile.loadout.ToLoadout() : LoadoutData.CreateEmpty());
            profile.activeLoadoutPreset = 0;
            return true;
        }

        public static void Store(ref PlayerProfileData profile, LoadoutData current)
        {
            Migrate(ref profile);
            profile.loadout = LoadoutRecord.From(current);
            profile.loadoutPresets[profile.activeLoadoutPreset] = LoadoutRecord.From(current);
        }

        public static LoadoutData Switch(ref PlayerProfileData profile, int index, int arena,
            Func<string, bool> suitAllowed, Func<string, bool> districtAllowed)
        {
            Migrate(ref profile);
            Store(ref profile, profile.loadout.ToLoadout());
            var source = profile.loadoutPresets[Clamp(index)].ToLoadout();
            var equipment = profile.loadout.ToLoadout();
            source.suitEras = equipment.suitEras;
            source.districtEras = equipment.districtEras;
            source.skinIDs = equipment.skinIDs;
            var editor = LoadoutRules.LocalEditor(source, arena);
            editor.DropUnavailable(suitAllowed, districtAllowed);
            profile.activeLoadoutPreset = Clamp(index);
            Store(ref profile, editor.ToLoadout());
            return profile.loadout.ToLoadout();
        }

        private static int Clamp(int index) => Math.Max(0, Math.Min(Count - 1, index));
    }
}
