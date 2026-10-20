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

        /// <summary>Null when the id names no catalog district or suit.</summary>
        public static Texture2D For(string lobbyId, int variant = 0)
        {
            if (string.IsNullOrEmpty(lobbyId)) return null;
            variant = Mathf.Clamp(variant, 0, 3);
            string key = lobbyId + "#" + variant;
            if (cache.TryGetValue(key, out Texture2D texture) && texture != null) return texture;
            bool suit = lobbyId.StartsWith("suit", System.StringComparison.OrdinalIgnoreCase);
            int[] types = suit ? StageLoadoutTypes.Suits(new[] { lobbyId }) : StageLoadoutTypes.Districts(new[] { lobbyId });
            if (types.Length == 0) return null;
            int type = types[0];
            string identity = StageArt.Identity(type, suit);
            Color tint = StagePaperArt.Tints[ItemTint.IndexFor(identity)];
            PaperKind kind = suit ? StageArt.Suit(type) : StageArt.District(type);
            texture = StagePaperArt.Bake(kind, tint, Size, false, type, variant);
            cache[key] = texture;
            return texture;
        }

        public static StyleBackground BackgroundFor(string lobbyId, int variant = 0)
        {
            string key = lobbyId + "#" + variant;
            if (backgrounds.TryGetValue(key, out StyleBackground cached)) return cached;
            Texture2D texture = For(lobbyId, variant);
            var background = texture == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(Background.FromTexture2D(texture));
            backgrounds[key] = background;
            return background;
        }
    }
}
