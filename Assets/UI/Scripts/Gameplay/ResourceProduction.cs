using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Every production job a player has in flight for one resource, as a
    /// progress fraction each. The resource rings draw these on the segments
    /// just above the current value, so four food with two farms half-done
    /// shows segments five and six both half filled - progress and timing in
    /// the same place the amount already is.
    ///
    /// WHAT COUNTS AS IN FLIGHT is TickProduction's rule, read back rather
    /// than guessed at: a Working villager on a district that yields this
    /// resource, with a timer running. A forge is the one that can be running
    /// and still produce nothing - it pays a material on completion and the
    /// sim silently resets the timer when it cannot - so a forge with no
    /// allocation or an empty till is not in flight, and its white is not
    /// drawn. A market alternates food and materials, and which half it is on
    /// is read the way the sim reads it: off the timer length.
    ///
    /// SORTED, NEAREST FIRST. Job 0 is the one closest to landing, so it sits
    /// on the segment that will light next and the rest queue behind it. Ties
    /// break by villager order, which keeps a row of identical farms from
    /// swapping places between frames.
    ///
    /// ResourceKind is NodeSheetContent's flags enum, reused rather than
    /// redeclared. InFlight wants exactly one of its bits: it is asking about
    /// one resource, not a set.
    ///
    /// No UnityEngine types - the simulation has none either, so this runs in
    /// dotnet/NodeWar.View.Tests directly.
    /// </summary>
    public static class ResourceProduction
    {
        /// <summary>
        /// Ceiling on jobs reported at once. The rings only have thirty
        /// segments and a player only has so many villagers; past this the
        /// extra jobs are invisible anyway.
        /// </summary>
        public const int MaxInFlight = 30;

        /// <summary>
        /// Writes every in-flight job's progress into <paramref name="into"/>,
        /// nearest to landing first, and returns how many were written. Never
        /// writes more than the array holds.
        /// </summary>
        public static int InFlight(SimulationState state, GameBalanceData balance, int playerID,
            ResourceKind resource, float tickAlpha, float[] into)
        {
            if (state == null || into == null || into.Length == 0) return 0;
            if (state.villagers == null || state.nodes == null || state.players == null) return 0;

            int count = 0;

            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData villager = state.villagers[i];

                if (villager.ownerID != playerID) continue;
                if (villager.state != VillagerState.Working) continue;
                if (villager.isConsumed) continue;
                if (villager.productionTicksMax <= 0) continue;
                if (villager.currentNodeID < 0 || villager.currentNodeID >= state.nodes.Length) continue;

                if (!TryYield(state, balance, villager, state.nodes[villager.currentNodeID], out ResourceKind kind))
                    continue;

                if (kind != resource) continue;

                Insert(into, ref count, Progress(villager, tickAlpha));
            }

            return count;
        }

        /// <summary>
        /// How far through its cycle one job is, 0 to 1, smoothed across the
        /// tick by <paramref name="tickAlpha"/> so the fill moves at render
        /// rate rather than stepping ten times a second. Same reading
        /// ProductionContent and ForgeContent take of the same two fields.
        /// </summary>
        public static float Progress(VillagerData villager, float tickAlpha)
        {
            if (villager.productionTicksMax <= 0) return 0f;

            float done = 1f - (villager.productionTicksRemaining - tickAlpha) / villager.productionTicksMax;

            if (done < 0f) return 0f;
            if (done > 1f) return 1f;
            return done;
        }

        /// <summary>
        /// What this villager's district will yield, and whether it will yield
        /// anything at all. See TickProduction - this mirrors it, and the one
        /// case where a running timer produces nothing is the forge that
        /// cannot pay its material.
        /// </summary>
        public static bool TryYield(SimulationState state, GameBalanceData balance,
            VillagerData villager, NodeData node, out ResourceKind kind)
        {
            switch (node.districtType)
            {
                case DistrictType.Farm:
                    kind = ResourceKind.Food;
                    return true;

                case DistrictType.Mine:
                    kind = ResourceKind.Materials;
                    return true;

                case DistrictType.Forge:
                    kind = ResourceKind.Metal;
                    if (node.materialAllocation <= 0) return false;
                    if (villager.ownerID < 0 || villager.ownerID >= state.players.Length) return false;
                    return state.players[villager.ownerID].materials >= 1;

                case DistrictType.Market:
                    kind = villager.productionTicksMax
                           == balance.GetDistrictStats(DistrictType.Market, node.districtEra).productionTicks
                        ? ResourceKind.Food
                        : ResourceKind.Materials;
                    return true;

                default:
                    kind = ResourceKind.Food;
                    return false;
            }
        }

        /// <summary>
        /// Insertion sort, descending, into a fixed array. A player has at most
        /// a couple of dozen villagers and this runs once per resource per
        /// frame, so the simple shape beats the clever one. Strictly-greater
        /// keeps equal jobs in villager order rather than shuffling them.
        /// </summary>
        private static void Insert(float[] into, ref int count, float progress)
        {
            int at = count;
            while (at > 0 && progress > into[at - 1]) at--;

            if (at >= into.Length) return;

            int last = count < into.Length ? count : into.Length - 1;
            for (int i = last; i > at; i--) into[i] = into[i - 1];

            into[at] = progress;
            if (count < into.Length) count++;
        }
    }
}
