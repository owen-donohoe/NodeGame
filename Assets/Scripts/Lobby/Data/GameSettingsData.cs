namespace NodeWar.Lobby
{
    /// <summary>
    /// The player's settings, as the Settings page presents them.
    ///
    /// Free of UnityEngine on purpose, like <see cref="LoadoutData"/> and for
    /// the same reason: it can then be linked into NodeWar.Lobby.Tests and
    /// have its clamping and migration actually tested, rather than trusted.
    ///
    /// Persisted inside PlayerProfileData through the one save path
    /// PlayerProfile already owns. It does not cross the wire — none of these
    /// values may reach the simulation, because a setting that changed a
    /// simulation result would desync the match the moment two players chose
    /// differently. They are presentation and input preferences only.
    ///
    /// Because it is a struct, `new GameSettingsData()` is all zeroes — which
    /// is indistinguishable from a player who turned every volume down and
    /// every switch off. <see cref="Version"/> is what separates the two, so
    /// read nothing without going through <see cref="Normalized"/> first.
    /// </summary>
    [System.Serializable]
    public struct GameSettingsData
    {
        /// <summary>
        /// Bumped when the meaning of a field changes in a way an older save
        /// cannot be read as. A save written before settings existed has 0
        /// here, which is how <see cref="Normalized"/> tells "absent" from
        /// "deliberately silent".
        /// </summary>
        /// <remarks>
        /// 1 - the lobby Settings page's ten values.
        /// 2 - adds <see cref="opponentRoutes"/>, whose default is true. A
        ///     version 1 save has the field as false simply because that is
        ///     what a bool deserialises to, which is why 1 cannot just be read
        ///     as 2 and why <see cref="Normalized"/> migrates rather than
        ///     resetting: resetting would throw away settings the player set.
        /// </remarks>
        public const int CurrentVersion = 2;

        /// <summary>Small, Default, Large. The page owns what they are called.</summary>
        public const int InterfaceSizeCount = 3;

        /// <summary>The middle entry, and what an unset profile opens on.</summary>
        public const int DefaultInterfaceSize = 1;

        public int version;

        // ---- Audio. Nothing consumes these yet: the project has no
        // AudioMixer, AudioSource or AudioListener anywhere. They are saved so
        // the page is honest about remembering them, and so the audio pass
        // inherits a stored value instead of inventing a second one.
        public float masterVolume;
        public float musicVolume;
        public float effectsVolume;

        // ---- Accessibility.
        public bool colourblindMarks;
        public bool reducedMotion;
        public int interfaceSize;

        // ---- Gameplay.
        public float cameraSpeed;
        public bool confirmEachCommand;

        /// <summary>
        /// Draw opponent movement routes. Backs the in-match panel's only
        /// toggle and feeds OpponentRouteSettings.show, which is a real
        /// information gate rather than a cosmetic one - see that class for
        /// what it does and does not reveal. Added in version 2.
        /// </summary>
        public bool opponentRoutes;

        // ---- Mobile.
        public bool haptics;
        public bool batterySaver;

        /// <summary>
        /// The values the Settings page is authored with in SettingsPage.uxml.
        /// They are duplicated there as the controls' initial state so the page
        /// looks right before a profile exists; if either side moves, move both.
        /// </summary>
        public static GameSettingsData CreateDefault()
        {
            return new GameSettingsData
            {
                version = CurrentVersion,

                masterVolume = 0.64f,
                musicVolume = 0.40f,
                effectsVolume = 0.78f,

                colourblindMarks = true,
                reducedMotion = false,
                interfaceSize = DefaultInterfaceSize,

                cameraSpeed = 0.52f,
                confirmEachCommand = false,
                opponentRoutes = true,

                haptics = true,
                batterySaver = false
            };
        }

        /// <summary>
        /// A copy safe to read: every slider inside 0..1 and the interface size
        /// inside its range.
        ///
        /// A struct carrying version 0 is a save written before settings
        /// existed — JsonUtility leaves the whole block zeroed rather than
        /// failing — so it becomes the defaults wholesale. That is the only
        /// case where stored values are discarded.
        ///
        /// Every later version is migrated rather than reset: the fields it
        /// did have are carried across, and fields added since take their
        /// default. Resetting instead would be a silent wipe of settings the
        /// player chose, which is the failure this whole version scheme exists
        /// to avoid. A new version adds a step here, not another save path.
        ///
        /// Call this at every boundary rather than trusting the fields.
        /// </summary>
        public static GameSettingsData Normalized(GameSettingsData source)
        {
            // Written before settings existed: nothing to carry.
            if (source.version <= 0) return CreateDefault();

            // 1 -> 2. The field did not exist, so its stored false means
            // "absent", not "off"; everything else the player set is kept.
            if (source.version < 2) source.opponentRoutes = true;

            return new GameSettingsData
            {
                version = CurrentVersion,

                masterVolume = Clamp01(source.masterVolume),
                musicVolume = Clamp01(source.musicVolume),
                effectsVolume = Clamp01(source.effectsVolume),

                colourblindMarks = source.colourblindMarks,
                reducedMotion = source.reducedMotion,
                interfaceSize = ClampInterfaceSize(source.interfaceSize),

                cameraSpeed = Clamp01(source.cameraSpeed),
                confirmEachCommand = source.confirmEachCommand,
                opponentRoutes = source.opponentRoutes,

                haptics = source.haptics,
                batterySaver = source.batterySaver
            };
        }

        /// <summary>
        /// True when the two differ in any field a player can set. Used to skip
        /// a disk write when a control was touched and left where it was.
        /// </summary>
        public static bool Differ(GameSettingsData a, GameSettingsData b)
        {
            return a.masterVolume != b.masterVolume
                || a.musicVolume != b.musicVolume
                || a.effectsVolume != b.effectsVolume
                || a.colourblindMarks != b.colourblindMarks
                || a.reducedMotion != b.reducedMotion
                || a.interfaceSize != b.interfaceSize
                || a.cameraSpeed != b.cameraSpeed
                || a.confirmEachCommand != b.confirmEachCommand
                || a.opponentRoutes != b.opponentRoutes
                || a.haptics != b.haptics
                || a.batterySaver != b.batterySaver;
        }

        /// <summary>
        /// Wraps forward through the interface sizes, which is what the row
        /// does when tapped. Written here rather than in the page so the wrap
        /// is covered by the same tests as the clamp.
        /// </summary>
        public static int NextInterfaceSize(int current)
        {
            return (ClampInterfaceSize(current) + 1) % InterfaceSizeCount;
        }

        private static int ClampInterfaceSize(int value)
        {
            if (value < 0) return 0;
            if (value >= InterfaceSizeCount) return InterfaceSizeCount - 1;
            return value;
        }

        // No Mathf here: this file must stay free of UnityEngine so the test
        // project can compile it.
        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;

            // NaN fails both comparisons above and would otherwise survive into
            // a slider, which then renders at an undefined position.
            if (float.IsNaN(value)) return 0f;

            return value;
        }
    }
}
