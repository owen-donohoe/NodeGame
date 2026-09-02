using UnityEngine;

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
    /// Villager wins over node when both are under the finger; that priority is
    /// applied when this struct is built, so consumers never re-decide it.
    /// </summary>
    public readonly struct GestureTarget
    {
        public readonly GestureTargetKind kind;

        /// <summary>Villager ID or node ID depending on <see cref="kind"/>. -1 when None.</summary>
        public readonly int id;

        /// <summary>Screen position of the press that produced this target.</summary>
        public readonly Vector2 screenPos;

        public GestureTarget(GestureTargetKind kind, int id, Vector2 screenPos)
        {
            this.kind = kind;
            this.id = id;
            this.screenPos = screenPos;
        }

        public static GestureTarget None(Vector2 screenPos)
        {
            return new GestureTarget(GestureTargetKind.None, -1, screenPos);
        }

        public bool IsVillager => kind == GestureTargetKind.Villager;
        public bool IsNode => kind == GestureTargetKind.Node;

        public override string ToString()
        {
            return kind == GestureTargetKind.None ? "None" : kind + "(" + id + ")";
        }
    }
}
