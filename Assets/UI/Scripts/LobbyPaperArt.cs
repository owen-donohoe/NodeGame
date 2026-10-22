using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The deterministic generated paper art (tinted building per district, tinted villager per
    /// suit) as UI backgrounds. Each texture is rasterised once for the app's lifetime.
    /// </summary>
    public static class LobbyPaperArt
    {
        private const int Size = 192;
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, StyleBackground> backgrounds = new Dictionary<string, StyleBackground>();

        /// <summary>Null when the id names no catalog district or suit. Unknown ids are never cached.</summary>
        public static Texture2D For(string lobbyId, int variant = 0)
        {
            if (!Canonicalise(ref lobbyId, ref variant, out bool suit, out int type)) return null;
            string key = Key(lobbyId, variant);
            if (cache.TryGetValue(key, out Texture2D texture) && texture != null) return texture;
            string identity = StageArt.Identity(type, suit);
            Color tint = StagePaperArt.Tints[ItemTint.IndexFor(identity)];
            PaperKind kind = suit ? StageArt.Suit(type) : StageArt.District(type);
            texture = StagePaperArt.Bake(kind, tint, Size, false, type, variant);
            cache[key] = texture;
            return texture;
        }

        public static StyleBackground BackgroundFor(string lobbyId, int variant = 0)
        {
            if (!Canonicalise(ref lobbyId, ref variant, out _, out _)) return new StyleBackground(StyleKeyword.None);
            string key = Key(lobbyId, variant);
            if (backgrounds.TryGetValue(key, out StyleBackground cached)) return cached;
            Texture2D texture = For(lobbyId, variant);
            if (texture == null) return new StyleBackground(StyleKeyword.None);
            var background = new StyleBackground(Background.FromTexture2D(texture));
            backgrounds[key] = background;
            return background;
        }

        private static string Key(string lobbyId, int variant) => lobbyId + "#" + variant;

        // One normalisation shared by both lookups, so they agree on keys and bad input makes no entries.
        private static bool Canonicalise(ref string lobbyId, ref int variant, out bool suit, out int type)
        {
            suit = false; type = 0;
            if (string.IsNullOrWhiteSpace(lobbyId)) return false;
            lobbyId = lobbyId.Trim();
            variant = Mathf.Clamp(variant, 0, 3);
            suit = lobbyId.StartsWith("suit", System.StringComparison.OrdinalIgnoreCase);
            int[] types = suit ? StageLoadoutTypes.Suits(new[] { lobbyId }) : StageLoadoutTypes.Districts(new[] { lobbyId });
            if (types.Length == 0) return false;
            type = types[0];
            return true;
        }
    }
}
