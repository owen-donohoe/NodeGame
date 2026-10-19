using System;
using System.Collections.Generic;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    /// <summary>Local placeholder rewards; this does not grant authoritative wallet or inventory items.</summary>
    public sealed class LocalCrateInventory : ICrateInventory
    {
        private static readonly IReadOnlyList<CrateReward> SmallRewards = Array.AsReadOnly(new[]
        {
            new CrateReward(CrateRewardKind.Coins, "", 50),
            new CrateReward(CrateRewardKind.Premium, "", 1)
        });
        private readonly Func<int> readSmallCount;
        private readonly Func<float> readProgress;
        private readonly Action<int> saveSmallCount;
        private int lastCount;
        private float lastProgress;
        public event Action Changed;

        public LocalCrateInventory(Func<int> readSmallCount, Func<float> readProgress, Action<int> saveSmallCount)
        {
            this.readSmallCount = readSmallCount ?? throw new ArgumentNullException(nameof(readSmallCount));
            this.readProgress = readProgress ?? throw new ArgumentNullException(nameof(readProgress));
            this.saveSmallCount = saveSmallCount ?? throw new ArgumentNullException(nameof(saveSmallCount));
            lastCount = Count(CrateSize.Small);
            lastProgress = NextProgress;
        }

        public int Count(CrateSize size) => size == CrateSize.Small ? Math.Max(0, readSmallCount()) : 0;
        public CrateSize NextSize => CrateSize.Small;
        public float NextProgress
        {
            get
            {
                float progress = readProgress();
                return float.IsNaN(progress) ? 0f : Math.Max(0f, Math.Min(1f, progress));
            }
        }

        public bool TryOpen(CrateSize size, out IReadOnlyList<CrateReward> rewards)
        {
            rewards = Array.Empty<CrateReward>();
            int count = Count(size);
            if (size != CrateSize.Small || count == 0) return false;
            saveSmallCount(count - 1);
            rewards = SmallRewards;
            Refresh();
            return true;
        }

        public void Refresh()
        {
            int count = Count(CrateSize.Small);
            float progress = NextProgress;
            if (count == lastCount && progress == lastProgress) return;
            lastCount = count;
            lastProgress = progress;
            Changed?.Invoke();
        }
    }
}
