namespace NodeWar.UI
{
    // The runtime binding names and the dotnet UXML contract tests share this list.
    public static class UiRequiredNames
    {
        public static class GameplayHud
        {
            public const string HudRoot = "hud-root";
            public const string HudSafeArea = "hud-safe-area";
            public const string HudResSafeBottom = "hud-res-safe-bottom";
            public const string HudClock = "hud-clock";
            public const string HudFood = "hud-food";
            public const string HudRingFood = "hud-ring-food";
            public const string HudMaterials = "hud-materials";
            public const string HudRingMaterials = "hud-ring-materials";
            public const string HudMetal = "hud-metal";
            public const string HudRingMetal = "hud-ring-metal";
            public const string HudMagic = "hud-magic";
            public const string HudRingMagic = "hud-ring-magic";
            public const string HudResMetal = "hud-res-metal";
            public const string HudResMagic = "hud-res-magic";
            public const string HudVillagerToggle = "hud-villager-toggle";
            public const string HudVillagers = "hud-villagers";
            public const string HudVillagersP0 = "hud-villagers-p0";
            public const string HudVillagersP1 = "hud-villagers-p1";
            public const string HudSelection = "hud-selection";
            public const string HudSelectionText = "hud-selection-text";
            public const string HudSelbar = "hud-selbar";
            public const string HudSelbarCount = "hud-selbar-count";
            public const string HudRecentreDock = "hud-recentre-dock";
            public const string HudZoom = "hud-zoom";
            public const string HudZoomValue = "hud-zoom-value";
            public const string HudZoomFill = "hud-zoom-fill";
            public const string HudRecentre = "hud-recentre";
            public const string HudCountdown = "hud-countdown";
            public const string HudCountdownStep = "hud-countdown-step";
            public const string HudTempo = "hud-tempo";
            public const string HudTempoBanner = "hud-tempo-banner";
            public const string HudTempoTitle = "hud-tempo-title";
            public const string HudTempoSub = "hud-tempo-sub";
            public const string HudSdCountdown = "hud-sd-countdown";
            public const string HudSdTitle = "hud-sd-title";
            public const string HudSdSub = "hud-sd-sub";
            public const string HudEnd = "hud-end";
            public const string HudEndTitle = "hud-end-title";
            public const string HudEndSub = "hud-end-sub";
            public const string HudEndRank = "hud-end-rank";
            public const string HudEndRankHeadline = "hud-end-rank-headline";
            public const string HudEndRankDetail = "hud-end-rank-detail";
            public const string HudEndReturn = "hud-end-return";
            public const string HudHold = "hud-hold";
            public const string HudHoldTitle = "hud-hold-title";
            public const string HudHoldLine = "hud-hold-line";
            public const string HudHoldAction = "hud-hold-action";
            public const string HudConnection = "hud-connection";
            public static readonly string[] All =
            {
                HudRoot,
                HudSafeArea,
                HudResSafeBottom,
                HudClock,
                HudFood,
                HudRingFood,
                HudMaterials,
                HudRingMaterials,
                HudMetal,
                HudRingMetal,
                HudMagic,
                HudRingMagic,
                HudResMetal,
                HudResMagic,
                HudVillagerToggle,
                HudVillagers,
                HudVillagersP0,
                HudVillagersP1,
                HudSelection,
                HudSelectionText,
                HudSelbar,
                HudSelbarCount,
                HudRecentreDock,
                HudZoom,
                HudZoomValue,
                HudZoomFill,
                HudRecentre,
                HudCountdown,
                HudCountdownStep,
                HudTempo,
                HudTempoBanner,
                HudTempoTitle,
                HudTempoSub,
                HudSdCountdown,
                HudSdTitle,
                HudSdSub,
                HudEnd,
                HudEndTitle,
                HudEndSub,
                HudEndRank,
                HudEndRankHeadline,
                HudEndRankDetail,
                HudEndReturn,
                HudHold,
                HudHoldTitle,
                HudHoldLine,
                HudHoldAction,
                HudConnection,
            };
        }
        public static class Settings
        {
            public const string SettingsAccountStatus = "settings-account-status";
            public const string SettingsAccountMessage = "settings-account-message";
            public const string SettingsAccountLink = "settings-account-link";
            public const string SettingsAccountSignIn = "settings-account-sign-in";
            public const string SettingsAccountSignOut = "settings-account-sign-out";
            public const string SettingsAccountGuest = "settings-account-guest";
            public const string SettingsMaster = "settings-master";
            public const string SettingsMusic = "settings-music";
            public const string SettingsEffects = "settings-effects";
            public const string SettingsCamera = "settings-camera";
            public const string SettingsColourblind = "settings-colourblind";
            public const string SettingsMotion = "settings-motion";
            public const string SettingsConfirm = "settings-confirm";
            public const string SettingsHaptics = "settings-haptics";
            public const string SettingsBattery = "settings-battery";
            public const string SettingsSize = "settings-size";
            public const string SettingsFramecap = "settings-framecap";
            public const string SettingsGraphics = "settings-graphics";
            public const string SettingsRowGraphics = "settings-row-graphics";
            public const string SettingsRowSize = "settings-row-size";
            public const string SettingsRowFramecap = "settings-row-framecap";
            public const string SettingsRowControls = "settings-row-controls";
            public const string SettingsRowColourblind = "settings-row-colourblind";
            public const string SettingsRowMotion = "settings-row-motion";
            public const string SettingsRowConfirm = "settings-row-confirm";
            public const string SettingsRowHaptics = "settings-row-haptics";
            public const string SettingsRowBattery = "settings-row-battery";
            public const string GraphicsDetails = "graphics-details";
            public const string SettingsWaves = "settings-waves";
            public const string SettingsRowWaves = "settings-row-waves";
            public const string GraphicsTransparency = "settings-transparency";
            public const string GraphicsRowTransparency = "settings-row-transparency";
            public const string GraphicsSurfaceFoamDistortion = "settings-surface-foam-distortion";
            public const string GraphicsRowSurfaceFoamDistortion = "settings-row-surface-foam-distortion";
            public const string GraphicsHorizonFresnel = "settings-horizon-fresnel";
            public const string GraphicsRowHorizonFresnel = "settings-row-horizon-fresnel";
            public const string GraphicsSpecular = "settings-specular";
            public const string GraphicsRowSpecular = "settings-row-specular";
            public const string GraphicsCrestColour = "settings-crest-colour";
            public const string GraphicsRowCrestColour = "settings-row-crest-colour";
            public const string GraphicsShoreNoise = "settings-shore-noise";
            public const string GraphicsRowShoreNoise = "settings-row-shore-noise";
            public const string GraphicsDither = "settings-dither";
            public const string GraphicsRowDither = "settings-row-dither";
            public const string GraphicsShadows = "settings-shadows";
            public const string GraphicsRowShadows = "settings-row-shadows";
            public const string GraphicsDetailNormals = "settings-detail-normals";
            public const string GraphicsRowDetailNormals = "settings-row-detail-normals";
            public const string GraphicsMacroNormals = "settings-macro-normals";
            public const string GraphicsRowMacroNormals = "settings-row-macro-normals";
            public const string GraphicsReflections = "settings-reflections";
            public const string GraphicsRowReflections = "settings-row-reflections";
            public const string GraphicsLappingLines = "settings-lapping-lines";
            public const string GraphicsRowLappingLines = "settings-row-lapping-lines";
            public const string GraphicsCrestFoam = "settings-crest-foam";
            public const string GraphicsRowCrestFoam = "settings-row-crest-foam";
            public const string GraphicsSecondFoamLayer = "settings-second-foam-layer";
            public const string GraphicsRowSecondFoamLayer = "settings-row-second-foam-layer";
            public const string GraphicsCaustics = "settings-caustics";
            public const string GraphicsRowCaustics = "settings-row-caustics";
            public static readonly string[] GraphicsToggles = { GraphicsTransparency, GraphicsSurfaceFoamDistortion, GraphicsHorizonFresnel, GraphicsSpecular, GraphicsCrestColour, GraphicsShoreNoise, GraphicsDither, GraphicsShadows, GraphicsDetailNormals, GraphicsMacroNormals, GraphicsReflections, GraphicsLappingLines, GraphicsCrestFoam, GraphicsSecondFoamLayer, GraphicsCaustics };
            public static readonly string[] GraphicsRows = { GraphicsRowTransparency, GraphicsRowSurfaceFoamDistortion, GraphicsRowHorizonFresnel, GraphicsRowSpecular, GraphicsRowCrestColour, GraphicsRowShoreNoise, GraphicsRowDither, GraphicsRowShadows, GraphicsRowDetailNormals, GraphicsRowMacroNormals, GraphicsRowReflections, GraphicsRowLappingLines, GraphicsRowCrestFoam, GraphicsRowSecondFoamLayer, GraphicsRowCaustics };
            public static readonly string[] All =
            {
                SettingsAccountStatus,
                SettingsAccountMessage,
                SettingsAccountLink,
                SettingsAccountSignIn,
                SettingsAccountSignOut,
                SettingsAccountGuest,
                SettingsMaster,
                SettingsMusic,
                SettingsEffects,
                SettingsCamera,
                SettingsColourblind,
                SettingsMotion,
                SettingsConfirm,
                SettingsHaptics,
                SettingsBattery,
                SettingsSize,
                SettingsFramecap,
                SettingsGraphics,
                SettingsRowGraphics,
                GraphicsDetails, SettingsWaves, SettingsRowWaves,
                GraphicsTransparency, GraphicsRowTransparency,
                GraphicsSurfaceFoamDistortion, GraphicsRowSurfaceFoamDistortion,
                GraphicsHorizonFresnel, GraphicsRowHorizonFresnel,
                GraphicsSpecular, GraphicsRowSpecular,
                GraphicsCrestColour, GraphicsRowCrestColour,
                GraphicsShoreNoise, GraphicsRowShoreNoise,
                GraphicsDither, GraphicsRowDither,
                GraphicsShadows, GraphicsRowShadows,
                GraphicsDetailNormals, GraphicsRowDetailNormals,
                GraphicsMacroNormals, GraphicsRowMacroNormals,
                GraphicsReflections, GraphicsRowReflections,
                GraphicsLappingLines, GraphicsRowLappingLines,
                GraphicsCrestFoam, GraphicsRowCrestFoam,
                GraphicsSecondFoamLayer, GraphicsRowSecondFoamLayer,
                GraphicsCaustics, GraphicsRowCaustics,
                SettingsRowSize,
                SettingsRowFramecap,
                SettingsRowControls,
                SettingsRowColourblind,
                SettingsRowMotion,
                SettingsRowConfirm,
                SettingsRowHaptics,
                SettingsRowBattery,
            };
        }
        public static class MatchSettings
        {
            public const string HudSettingsScrim = "hud-settings-scrim";
            public const string HudSettings = "hud-settings";
            public const string HudSettingsGear = "hud-settings-gear";
            public const string HudSettingsMusic = "hud-settings-music";
            public const string HudSettingsEffects = "hud-settings-effects";
            public const string HudSettingsRoutes = "hud-settings-routes";
            public const string HudSettingsEmotes = "hud-settings-emotes";
            public const string HudSettingsFramecap = "hud-settings-framecap";
            public const string HudSettingsGraphics = "hud-settings-graphics";
            public const string HudSettingsRowGraphics = "hud-settings-row-graphics";
            public const string HudSettingsRowRoutes = "hud-settings-row-routes";
            public const string HudSettingsRowEmotes = "hud-settings-row-emotes";
            public const string HudSettingsRowFramecap = "hud-settings-row-framecap";
            public const string HudSettingsCamerabutton = "hud-settings-camerabutton";
            public const string HudSettingsSide = "hud-settings-side";
            public const string HudSettingsTooltips = "hud-settings-tooltips";
            public const string HudSettingsTarget = "hud-settings-target";
            public const string HudSettingsSurrenderArea = "hud-settings-surrender-area";
            public const string HudSettingsSurrenderLine = "hud-settings-surrender-line";
            public const string HudSettingsSurrender = "hud-settings-surrender";
            public const string HudSettingsSurrenderNo = "hud-settings-surrender-no";
            public const string HudSettingsSurrenderYes = "hud-settings-surrender-yes";
            public const string HudSettingsRowTarget = "hud-settings-row-target";
            public const string HudSettingsRowCamerabutton = "hud-settings-row-camerabutton";
            public const string HudSettingsRowTooltips = "hud-settings-row-tooltips";
            public const string HudSettingsRowSide = "hud-settings-row-side";
            public const string HudSettingsRowControlsReset = "hud-settings-row-controls-reset";
            public static readonly string[] All =
            {
                HudSettingsScrim,
                HudSettings,
                HudSettingsGear,
                HudSettingsMusic,
                HudSettingsEffects,
                HudSettingsRoutes,
                HudSettingsEmotes,
                HudSettingsFramecap,
                HudSettingsGraphics,
                HudSettingsRowGraphics,
                HudSettingsRowRoutes,
                HudSettingsRowEmotes,
                HudSettingsRowFramecap,
                HudSettingsCamerabutton,
                HudSettingsSide,
                HudSettingsTooltips,
                HudSettingsTarget,
                HudSettingsSurrenderArea,
                HudSettingsSurrenderLine,
                HudSettingsSurrender,
                HudSettingsSurrenderNo,
                HudSettingsSurrenderYes,
                HudSettingsRowTarget,
                HudSettingsRowCamerabutton,
                HudSettingsRowTooltips,
                HudSettingsRowSide,
                HudSettingsRowControlsReset,
            };
        }
    }
}
