using System;
using NodeWar.View;
using UnityEngine;

namespace NodeWar.Lobby
{
    [CreateAssetMenu(fileName = "UIArtTheme", menuName = "NodeWar/UI Art Theme")]
    public sealed class UIArtTheme : ScriptableObject
    {
        [Serializable]
        public struct IconEntry
        {
            public LobbyIconKind kind;
            public Sprite sprite;
            public bool keepOriginalColours;
        }

        public IconEntry[] icons;
        public DistrictVisualTable districtVisuals;

        // Array order is authoritative, including an empty first entry.
        public bool TryIcon(LobbyIconKind kind, out IconEntry entry)
        {
            if (icons != null)
                for (int i = 0; i < icons.Length; i++)
                    if (icons[i].kind == kind) { entry = icons[i]; return true; }
            entry = default;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (icons == null) return;
            for (int i = 0; i < icons.Length; i++)
                for (int j = i + 1; j < icons.Length; j++)
                    if (icons[i].kind == icons[j].kind)
                        Debug.LogWarning("[UIArtTheme] Duplicate " + icons[i].kind +
                            " in " + name + "; the first entry wins.", this);
        }
#endif
    }

    public static class UIArt
    {
        private static bool loaded;
        private static UIArtTheme theme;

        public static UIArtTheme Theme
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    theme = Resources.Load<UIArtTheme>("UIArtTheme");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (theme == null) Debug.LogWarning("[UIArt] Resources/UIArtTheme is missing; using default UI art.");
#endif
                }
                return theme;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { loaded = false; theme = null; }
    }
}
