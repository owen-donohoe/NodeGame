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
        /// 3 - adds <see cref="opponentEmotes"/>, default true. Earlier saves
        ///     enable emotes while keeping all existing preferences.
        /// 4 - adds <see cref="frameCap"/>, an index into <see cref="FrameCapRates"/>.
        ///     Its zero is a real choice (30 fps), so an older save's stored 0
        ///     is not read as that: <see cref="Normalized"/> gives it
        ///     <see cref="DefaultFrameCap"/>.
        /// 5 - adds input bindings and the controls preferences.
        /// </remarks>
        public const int CurrentVersion = 5;

        public const float DefaultHoldTime = 0.3f;
        public const float MinHoldTime = 0.15f;
        public const float MaxHoldTime = 1f;

        /// <summary>Small, Default, Large. The page owns what they are called.</summary>
        public const int InterfaceSizeCount = 3;

        /// <summary>The middle entry, and what an unset profile opens on.</summary>
        public const int DefaultInterfaceSize = 1;

        /// <summary>
        /// The frame caps a player can pick, as Application.targetFrameRate
        /// values: 30, 60, 120, and -1 for uncapped. The setting stores an index
        /// rather than the rate, so a hand-edited or corrupt save can only land
        /// on one of these. A cap only applies while vSync is off; with vSync
        /// on, as it usually is on a phone, the display rate takes over.
        /// </summary>
        public static readonly int[] FrameCapRates = { 30, 60, 120, -1 };

        private static readonly string[] FrameCapNames = { "30", "60", "120", "Uncapped" };

        public const int FrameCapCount = 4;

        /// <summary>60, what the build capped to before this was a setting.</summary>
        public const int DefaultFrameCap = 1;

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

        // ---- Display.
        /// <summary>Index into <see cref="FrameCapRates"/>. Presentation only; added in version 4.</summary>
        public int frameCap;

        // ---- Gameplay.
        public float cameraSpeed;
        public bool confirmEachCommand;

        /// <summary>
        /// Draw opponent movement routes. Backs the in-match panel's routes
        /// toggle and feeds OpponentRouteSettings.show, which is a real
        /// information gate rather than a cosmetic one - see that class for
        /// what it does and does not reveal. Added in version 2.
        /// </summary>
        public bool opponentRoutes;

        /// <summary>Allow incoming and outgoing emotes. Presentation only; added in version 3.</summary>
        public bool opponentEmotes;

        // ---- Mobile.
        public bool haptics;
        public bool batterySaver;

        // ---- Controls. PlayerProfile is a local JSON file under
        // persistentDataPath, not synced across devices; no per-device key needed.
        public InputBinding[] inputBindings;
        public float holdTime;
        public bool showCameraButton;
        public bool cameraButtonZoom;
        public int controlsSide; // 0 Right, 1 Left.
        public bool showSelectionBar; // The selection counter button; the field kept its first name.

        /// <summary>Hints such as "Tap a node to move". Added to version 5 before it shipped.</summary>
        public bool tooltips;

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
                frameCap = DefaultFrameCap,

                cameraSpeed = 0.52f,
                confirmEachCommand = false,
                opponentRoutes = true,
                opponentEmotes = true,

                haptics = true,
                batterySaver = false,

                inputBindings = InputBindings.CreateDefault(),
                holdTime = DefaultHoldTime,
                showCameraButton = true,
                cameraButtonZoom = true,
                controlsSide = 0,
                showSelectionBar = true,
                tooltips = true
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
            if (source.version < 3) source.opponentEmotes = true;
            if (source.version < 4) source.frameCap = DefaultFrameCap;
            if (source.version < 5)
            {
                source.inputBindings = null;
                source.holdTime = DefaultHoldTime;
                source.showCameraButton = true;
                source.cameraButtonZoom = true;
                source.controlsSide = 0;
                source.showSelectionBar = true;
                source.tooltips = true;
            }

            return new GameSettingsData
            {
                version = CurrentVersion,

                masterVolume = Clamp01(source.masterVolume),
                musicVolume = Clamp01(source.musicVolume),
                effectsVolume = Clamp01(source.effectsVolume),

                colourblindMarks = source.colourblindMarks,
                reducedMotion = source.reducedMotion,
                interfaceSize = ClampInterfaceSize(source.interfaceSize),
                frameCap = ClampFrameCap(source.frameCap),

                cameraSpeed = Clamp01(source.cameraSpeed),
                confirmEachCommand = source.confirmEachCommand,
                opponentRoutes = source.opponentRoutes,
                opponentEmotes = source.opponentEmotes,

                haptics = source.haptics,
                batterySaver = source.batterySaver,

                inputBindings = InputBindings.Normalized(source.inputBindings),
                holdTime = ClampHoldTime(source.holdTime),
                showCameraButton = source.showCameraButton,
                cameraButtonZoom = source.cameraButtonZoom,
                controlsSide = source.controlsSide == 1 ? 1 : 0,
                showSelectionBar = source.showSelectionBar,
                tooltips = source.tooltips
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
                || a.frameCap != b.frameCap
                || a.cameraSpeed != b.cameraSpeed
                || a.confirmEachCommand != b.confirmEachCommand
                || a.opponentRoutes != b.opponentRoutes
                || a.opponentEmotes != b.opponentEmotes
                || a.haptics != b.haptics
                || a.batterySaver != b.batterySaver
                || InputBindings.Differ(a.inputBindings, b.inputBindings)
                || a.holdTime != b.holdTime
                || a.showCameraButton != b.showCameraButton
                || a.cameraButtonZoom != b.cameraButtonZoom
                || a.controlsSide != b.controlsSide
                || a.showSelectionBar != b.showSelectionBar
                || a.tooltips != b.tooltips;
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

        /// <summary>Wraps forward through the caps, as the row does when tapped.</summary>
        public static int NextFrameCap(int current)
        {
            return (ClampFrameCap(current) + 1) % FrameCapCount;
        }

        /// <summary>The Application.targetFrameRate for a stored index; -1 is uncapped.</summary>
        public static int TargetFrameRate(int frameCap)
        {
            return FrameCapRates[ClampFrameCap(frameCap)];
        }

        public static string FrameCapLabel(int frameCap)
        {
            return FrameCapNames[ClampFrameCap(frameCap)];
        }

        // An out-of-range index falls back to the default rather than the
        // nearest end: "uncapped" is not what a corrupt value should mean.
        private static int ClampFrameCap(int value)
        {
            if (value < 0 || value >= FrameCapCount) return DefaultFrameCap;
            return value;
        }

        private static int ClampInterfaceSize(int value)
        {
            if (value < 0) return 0;
            if (value >= InterfaceSizeCount) return InterfaceSizeCount - 1;
            return value;
        }

        // No Mathf here: this file must stay free of UnityEngine so the test
        // project can compile it.
        private static float ClampHoldTime(float value)
        {
            if (float.IsNaN(value)) return DefaultHoldTime;
            if (value < MinHoldTime) return MinHoldTime;
            if (value > MaxHoldTime) return MaxHoldTime;
            return value;
        }

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
