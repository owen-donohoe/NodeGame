namespace NodeWar.Network
{
    /// <summary>
    /// Decides what input delay to ask the peer for.
    ///
    /// How early our inputs must be stamped depends on how long they take to
    /// reach the peer, which we cannot see: we only see when the peer's inputs
    /// reach us. So each side watches its own stalls, which are the peer's delay
    /// being too short, and asks the peer for one more tick; after a long calm
    /// with slack to spare it asks for one fewer. The peer applies the request
    /// to its own stamping. Both sides run this, so each ends up with the delay
    /// its own link to the other needs, with no round-trip estimate and no clock
    /// offset to get wrong.
    ///
    /// Plain C#, no clock of its own: the core hands it the time on each live
    /// tick, so it runs under dotnet test against synthetic tick sequences.
    ///
    /// Raising is quick, lowering is slow, and each change waits out a cooldown
    /// before the next, because the peer needs a round trip to act on a request
    /// and the ticks in flight meanwhile are still late.
    /// </summary>
    internal sealed class InputDelayController
    {
        /// <summary>The delay a match starts with, and the floor.</summary>
        public const int BaseDelay = 2;

        /// <summary>The ceiling: 600 ms at 10 Hz, beyond which commands feel broken however bad the link.</summary>
        public const int MaxDelay = 6;

        // The last this many live ticks are looked at for lateness.
        private const int Window = 30;

        // This many late ticks in the window mean the peer's delay is too short.
        // One is jitter and two can be a bad frame; three is a pattern.
        private const int LateToRaise = 3;

        // Ticks in a row with no late one before lowering is considered: 20 s.
        private const int CalmToLower = 200;

        // The smallest slack, in seconds, seen across the calm stretch before
        // lowering. One tick is 0.1 s, so lowering leaves at least 40 ms.
        private const float SlackToLower = 0.14f;

        private const float RaiseCooldown = 1.5f;
        private const float LowerCooldown = 10f;

        private readonly bool[] late = new bool[Window];
        private int next;
        private int filled;
        private int lateCount;

        private int calmTicks;
        private float calmMinSlack = float.MaxValue;
        private float lastChange = float.MinValue;

        /// <summary>The delay to ask the peer for, or 0 while there is no opinion.</summary>
        public int Request { get; private set; }

        public void Reset()
        {
            ClearWindow();
            Request = 0;
            lastChange = float.MinValue;
        }

        /// <summary>
        /// One live tick has run. <paramref name="late"/>: it had to wait for the
        /// peer's input. <paramref name="slack"/>: how long that input had been
        /// there when the tick ran, in seconds. <paramref name="disturbed"/>:
        /// the link or our clock was not in an ordinary state (speculation,
        /// hold, a silent peer, catch-up), so the tick says nothing about the
        /// delay. <paramref name="peerDelay"/>: what the peer says it is using.
        /// </summary>
        public void OnLiveTick(float now, bool late, float slack, bool disturbed, int peerDelay)
        {
            if (disturbed)
            {
                ClearWindow();
                return;
            }

            Push(late);
            if (late)
            {
                calmTicks = 0;
                calmMinSlack = float.MaxValue;
            }
            else
            {
                calmTicks++;
                if (slack < calmMinSlack) calmMinSlack = slack;
            }

            if (peerDelay < BaseDelay) peerDelay = BaseDelay;
            if (peerDelay > MaxDelay) peerDelay = MaxDelay;

            if (lateCount >= LateToRaise && peerDelay < MaxDelay && now - lastChange >= RaiseCooldown)
            {
                Change(now, peerDelay + 1);
            }
            else if (calmTicks >= CalmToLower && calmMinSlack >= SlackToLower &&
                     peerDelay > BaseDelay && now - lastChange >= LowerCooldown)
            {
                Change(now, peerDelay - 1);
            }
        }

        private void Change(float now, int request)
        {
            Request = request;
            lastChange = now;
            ClearWindow();
        }

        private void Push(bool wasLate)
        {
            if (filled == Window && late[next]) lateCount--;
            late[next] = wasLate;
            if (wasLate) lateCount++;
            next = (next + 1) % Window;
            if (filled < Window) filled++;
        }

        private void ClearWindow()
        {
            for (int i = 0; i < Window; i++) late[i] = false;
            next = 0;
            filled = 0;
            lateCount = 0;
            calmTicks = 0;
            calmMinSlack = float.MaxValue;
        }
    }
}
