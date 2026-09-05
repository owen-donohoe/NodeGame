namespace NodeWar.View.Outline
{
    /// <summary>
    /// Hands out the group IDs the mask pass writes, and takes them back.
    ///
    /// The whole union-silhouette trick rests on this: every renderer in one
    /// group writes the same ID, so the dilate pass finds no edge along the
    /// seams inside a group, and two different groups get different IDs so a
    /// villager standing on a node does not merge into one blob with it.
    ///
    /// IDs live in one 8-bit mask channel, so the usable range is 1..255 and
    /// zero is reserved for "nothing here". IDs only have to be unique among
    /// groups that can overlap *on screen*, which is why this recycles from a
    /// pool instead of assigning a globally unique ID per node and villager --
    /// a board of thirty nodes and forty villagers would blow past 255 on its
    /// own, while the number outlined at any one moment is a handful.
    ///
    /// Deliberately free of UnityEngine so it can be unit tested without an
    /// Editor, a GameObject, or a render pipeline.
    /// </summary>
    public sealed class OutlineIdAllocator
    {
        /// <summary>Reserved. Written by the mask wherever no group is present.</summary>
        public const int None = 0;

        public const int MinId = 1;
        public const int MaxId = 255;

        /// <summary>How many distinct groups can be outlined at once.</summary>
        public const int Capacity = MaxId - MinId + 1;

        // Indexed by ID, so slot 0 exists and is never used. Costs one byte and
        // removes an offset calculation from every read.
        private readonly bool[] inUse = new bool[MaxId + 1];

        /// <summary>How many IDs are currently rented.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>
        /// How many times <see cref="Rent"/> has had to refuse since the last
        /// <see cref="Clear"/>. Exposed so exhaustion is observable in a test
        /// and in a debug overlay rather than being silent.
        /// </summary>
        public int DroppedCount { get; private set; }

        public bool IsRented(int id) =>
            id >= MinId && id <= MaxId && inUse[id];

        /// <summary>
        /// Takes the lowest free ID, or <see cref="None"/> when every ID is out.
        ///
        /// Lowest-free rather than most-recently-freed because it makes the
        /// assignment reproducible: the same sequence of selections produces the
        /// same IDs every run, which matters when you are staring at a mask
        /// debug view trying to work out which group is which.
        ///
        /// The scan is O(255) and runs only when a group starts being outlined,
        /// not per frame, so the loop is cheaper than the bookkeeping a free
        /// list would need to stay sorted.
        /// </summary>
        public int Rent()
        {
            for (int id = MinId; id <= MaxId; id++)
            {
                if (inUse[id]) continue;

                inUse[id] = true;
                ActiveCount++;
                return id;
            }

            // Caller drops the group rather than sharing an ID. Reusing one
            // would erase the outline *between* the two groups sharing it,
            // which is precisely the failure this system exists to prevent --
            // a silently wrong picture is worse than a missing outline.
            DroppedCount++;
            return None;
        }

        /// <summary>
        /// Returns an ID to the pool. Returning <see cref="None"/>, an
        /// out-of-range value, or an ID that is not currently rented is a no-op,
        /// so a double release cannot corrupt the pool or drive
        /// <see cref="ActiveCount"/> negative.
        /// </summary>
        /// <returns>True if an ID was actually released.</returns>
        public bool Return(int id)
        {
            if (id < MinId || id > MaxId) return false;
            if (!inUse[id]) return false;

            inUse[id] = false;
            ActiveCount--;
            return true;
        }

        /// <summary>Releases everything. For scene teardown and test isolation.</summary>
        public void Clear()
        {
            for (int id = MinId; id <= MaxId; id++) inUse[id] = false;
            ActiveCount = 0;
            DroppedCount = 0;
        }
    }
}
