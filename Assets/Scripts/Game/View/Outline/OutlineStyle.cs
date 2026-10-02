namespace NodeWar.View.Outline
{
    /// <summary>
    /// The state an outline can express, and the palette index the composite
    /// pass looks colour up by.
    ///
    /// This carries state only. Player ownership is deliberately not a style --
    /// ownership gets its own shape/badge channel, and a redundant owner tint on
    /// top of an outline colour is a later addition that must never become the
    /// only way to tell two players apart. <see cref="Present"/> does not change
    /// that: it says nothing about *whose* the thing is, and the owner tint that
    /// OutlineDriver puts on it rides the group's tint channel, alongside the
    /// sprite marks rather than in place of them.
    ///
    /// The numeric order of these members *is* the priority order, lowest to
    /// highest. A node can be Selected and Contested in the same frame and only
    /// one line can be drawn, so <see cref="OutlineStyleMask.Highest"/> resolves
    /// the conflict by taking the largest member present. Reordering this enum
    /// therefore silently changes which state wins; OutlineStyleTests pins the
    /// order so that a reorder fails a test instead of changing the game.
    /// </summary>
    public enum OutlineStyle : byte
    {
        /// <summary>
        /// Not outlined. Reserved as zero because zero is also the mask's
        /// "nothing here" ID, and a group in this state holds no ID at all.
        /// </summary>
        None = 0,

        /// <summary>
        /// The thin contact line every living villager carries, all match, both
        /// sides. The one member that is not transient: every other style says
        /// something is *happening* to the object, and this one only says the
        /// object is there.
        ///
        /// Lowest priority on purpose, so hover and selection paint over it
        /// rather than fight it, and thinnest in the palette for the same
        /// reason -- it is on screen constantly, so it has to sit under
        /// everything else without ever being the thing you notice.
        ///
        /// It costs an outline ID per villager for as long as that villager is
        /// alive. The allocator has 255 and the board tops out at fifty
        /// villagers plus its nodes, so the pool still has room to spare; see
        /// OutlineIdAllocator before adding a second always-on style.
        /// </summary>
        Present = 1,

        Hover = 2,
        Contested = 3,
        Selected = 4,
        CommandAck = 5,
    }

    /// <summary>
    /// Composable intent tracking for <see cref="OutlineStyle"/>.
    ///
    /// Callers do not assign a style directly, because several unrelated systems
    /// have an opinion about the same object at the same time: selection comes
    /// from SelectionSystem, contested comes from claim state, hover comes from
    /// the pointer, and a command acknowledgment is a brief pulse over whatever
    /// else is true. If each of those wrote a single Style field, whichever ran
    /// last in the frame would win, and the winner would change with script
    /// execution order.
    ///
    /// Instead each system sets or clears its own bit and the group resolves the
    /// result. Priority lives in exactly one place -- <see cref="Highest"/>.
    /// </summary>
    public static class OutlineStyleMask
    {
        /// <summary>Number of members in <see cref="OutlineStyle"/>, including None. Palette length.</summary>
        public const int StyleCount = 6;

        public static uint Bit(OutlineStyle style) => 1u << (int)style;

        public static uint With(uint mask, OutlineStyle style, bool active)
        {
            // None is not a bit anyone sets -- it is the absence of every other
            // bit. Letting a caller "set None" would make None both a member and
            // a state, and Highest would then have to break the tie.
            if (style == OutlineStyle.None) return mask;
            return active ? (mask | Bit(style)) : (mask & ~Bit(style));
        }

        public static bool Has(uint mask, OutlineStyle style)
        {
            if (style == OutlineStyle.None) return mask == 0u;
            return (mask & Bit(style)) != 0u;
        }

        /// <summary>
        /// The style that wins. Walks down from the highest member so the first
        /// hit is the answer: CommandAck &gt; Selected &gt; Contested &gt; Hover &gt;
        /// Present &gt; None.
        /// </summary>
        public static OutlineStyle Highest(uint mask)
        {
            for (int s = StyleCount - 1; s > 0; s--)
            {
                if ((mask & (1u << s)) != 0u) return (OutlineStyle)s;
            }

            return OutlineStyle.None;
        }
    }
}
