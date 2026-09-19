namespace NodeWar.Input
{
    public enum GestureTargetKind
    {
        None = 0,
        Villager = 1,
        Node = 2
    }

    /// <summary>
    /// What was under the pointer when a press began.
    ///
    /// Resolved exactly once, on touch-down, by the gesture source -- not
    /// re-raycast per consumer. Three scripts previously raycast the same press
    /// independently (SelectionSystem, CommandSystem, NodePanelManager) and each
    /// guessed at what the others would do with it. Caching the hit at press
    /// time also means the flash can fire immediately, before the gesture has
    /// resolved into a tap, pan or long press.
    ///
    /// Primary presses prefer a villager; secondary clicks resolve nodes only.
    /// The source applies that priority so consumers never re-decide it.
    /// </summary>
    public readonly struct GestureTarget
    {
        public readonly GestureTargetKind kind;

        /// <summary>Villager ID or node ID depending on <see cref="kind"/>. -1 when None.</summary>
        public readonly int id;

        public GestureTarget(GestureTargetKind kind, int id)
        {
            this.kind = kind;
            this.id = id;
        }

        public static GestureTarget None()
        {
            return new GestureTarget(GestureTargetKind.None, -1);
        }

        public override string ToString()
        {
            return kind == GestureTargetKind.None ? "None" : kind + "(" + id + ")";
        }
    }
}
