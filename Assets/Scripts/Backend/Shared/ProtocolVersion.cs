namespace NodeWar.Backend
{
    /// <summary>The wire version shared by the client serializer and server allocator.</summary>
    public static class ProtocolVersion
    {
        // Bump for packet layout changes, including GameCommand's wire layout.
        // 2: DraftLoadout carries era tables and skin IDs.
        // 3: Relay connections use DTLS instead of plain UDP. No layout change,
        //    but a DTLS peer cannot reach a UDP one, so builds must not mix.
        // 4: TickInput gains senderDelay and requestedDelay bytes (adaptive input delay).
        public const ushort Current = 4;
    }
}
