namespace NodeWar.Backend
{
    /// <summary>The wire version shared by the client serializer and server allocator.</summary>
    public static class ProtocolVersion
    {
        // Bump for packet layout changes, including GameCommand's wire layout.
        // 2: DraftLoadout carries era tables and skin IDs.
        public const ushort Current = 2;
    }
}
