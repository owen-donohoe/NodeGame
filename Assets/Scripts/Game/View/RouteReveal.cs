namespace NodeWar.View
{
    /// <summary>Route windows without a simulation or drawing dependency.</summary>
    public static class RouteReveal
    {
        public static int LegCount(int[] path, int index, int revealLegs)
        {
            if (path == null || index < 0 || index >= path.Length || revealLegs <= 0) return 0;
            int remaining = path.Length - index - 1;
            return remaining < revealLegs ? remaining : revealLegs;
        }

        /// <summary>The next legs only: standing on a node is not a threat to reach it.</summary>
        public static bool Contains(int[] path, int index, int revealLegs, int node)
        {
            int count = LegCount(path, index, revealLegs);
            for (int k = 1; k <= count; k++)
                if (path[index + k] == node) return true;
            return false;
        }

        public static bool SameWindow(int[] a, int indexA, int[] b, int indexB, int revealLegs)
        {
            int countA = LegCount(a, indexA, revealLegs);
            int countB = LegCount(b, indexB, revealLegs);
            if (countA != countB) return false;
            if (countA == 0) return true;
            return SameRoute(a, indexA + 1, b, indexB + 1, countA);
        }

        /// <summary>
        /// Compares a clipped sequence starting at each index. Lines include
        /// their starting node; threat windows start at the next one instead.
        /// Hidden tails must not split a squad and give away unrevealed orders.
        /// </summary>
        public static bool SameRoute(int[] a, int indexA, int[] b, int indexB, int compareNodes)
        {
            int remainingA = a.Length - indexA;
            int remainingB = b.Length - indexB;
            int countA = remainingA < compareNodes ? remainingA : compareNodes;
            int countB = remainingB < compareNodes ? remainingB : compareNodes;
            if (countA != countB) return false;

            for (int k = 0; k < countA; k++)
                if (a[indexA + k] != b[indexB + k]) return false;
            return true;
        }
    }
}
