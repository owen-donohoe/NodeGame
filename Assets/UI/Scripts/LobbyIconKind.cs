namespace NodeWar.Lobby
{
    /// <summary>Which glyph a <see cref="LobbyIcon"/> draws.</summary>
    public enum LobbyIconKind
    {
        None,
        Shop,     // prototype: ⌂
        Spark,    // prototype: ✦
        Tools,    // prototype: ⚒
        Smile,    // prototype: ☺
        Gear,     // prototype: ⚙
        Envelope, // prototype: ✉
        Mouth,    // the villager's smile: a CSS bottom border with radii, which USS draws flat
        Tv,       // match history
        Back,     // prototype: ←
        Flag,     // prototype: ⚑
        Hat,      // prototype: ◠
        Diamond,  // prototype: ❖
        District, // prototype: 🏛
        Suit,     // prototype: 🥋
        Lock,     // prototype: 🔒
        Pip,      // prototype: ◆
        Close,    // the match sheet's close button: ✕

        // In-match indicators and emotes. Same reason as the rest: Fredoka
        // carries none of these, and a fallback font would differ by platform.
        Alert,    // !  - an enemy headed for something of yours
        Swords,   // ⚔  - a fight
        Capture,  // ↓  - a node being pushed toward the enemy
        Sleep,    // zz - an idle villager
        Respawn,  // ↻  - a villager back at the Core
        Pointer,  // ▶  - the edge arrow, drawn pointing right and rotated
        Frown,    // ☹  - the sad emote
        Angry,    // the angry emote
        Speaker,  // emote mute state

        // In-match resources. The HUD's readouts and the sheet's costs both
        // need these inline, and Fredoka carries no emoji for them either.
        Food,      // 🍖 - a ham on the bone
        Materials, // 🪨 - a stone
        Metal,     // an ingot

        // Semantic kinds: append only; values are persisted in themes and UXML.
        NavBarHome,
        NavBarSocial,
        NavBarShop,
        DailyBox,
        VictoryBox,
        ShopBundle,
        GoldLeaf,
        MagicResource,
        SuitTreeAvailable,
        SuitTreeOwned,
        SuitTreeEquipped,
        SuitTreeLocked,
        ProfileYouAreHere,
        IndicatorEffect,
        IndicatorThreatToCore,
        IndicatorThreatToTerritory,
        IndicatorNodeUnderAttack,
        IndicatorNodeContested,
        EmoteHappy,
        EmoteWhiteFlag,
        CosmeticTinRoof,
        CosmeticPaperBanner,
        CosmeticStrawHat,
    }

}
