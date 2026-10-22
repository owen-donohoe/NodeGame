namespace NodeWar.Backend
{
    /// <summary>The wire version shared by the client serializer and server allocator.</summary>
    public static class ProtocolVersion
    {
        // Bump for packet layout or match-setup semantics changes, including GameCommand's wire layout.
        // 2: DraftLoadout carries era tables and skin IDs.
        // 3: Relay connections use DTLS instead of plain UDP. No layout change,
        //    but a DTLS peer cannot reach a UDP one, so builds must not mix.
        // 4: TickInput gains senderDelay and requestedDelay bytes (adaptive input delay).
        // 5: MatchSetup / MatchSetupAck packets: the host proposes the map and rules before the
        //    draft, and no draft packet is honoured until the guest has acknowledged them.
        // 6: Arena-dependent chosen capacities and free Warrior/base-district grants.
        //    Count prefixes alone cannot make setup identical: older clients normalize to
        //    fixed capacities and always add the free grants. No packet layout change.
        //    Release only with the capacity, grant, server/bot and launch changes integrated.
        // 7: Always 3/5 slots, with era locks inside; no appended map grants.
        public const ushort Current = 7;
    }
}
