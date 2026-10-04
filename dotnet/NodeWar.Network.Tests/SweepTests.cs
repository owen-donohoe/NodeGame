using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace NodeWar.Network.Tests
{
    /// <summary>
    /// Not a test: a measurement. Runs a fixed set of bad-link conditions over
    /// several seeds and prints one line per condition, so a change to a tuning
    /// constant (REDUNDANT_INPUTS, the resend intervals) can be compared before
    /// and after on the same links. Explicit, so a normal run skips it:
    ///
    ///   dotnet test dotnet/NodeWar.Network.Tests --filter "Category=Sweep"
    ///
    /// Every run still has to match the reference; a setting that is fast but
    /// wrong fails here rather than printing a good-looking number.
    /// </summary>
    [Explicit("measurement, not a test")]
    [Category("Sweep")]
    public class SweepTests
    {
        private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6 };

        private struct Condition
        {
            public string name;
            public Func<LinkProfile> link;
        }

        private static readonly Condition[] Conditions =
        {
            new Condition { name = "clean", link = () => new LinkProfile() },
            new Condition { name = "random 15%", link = () => new LinkProfile { Loss = 0.15 } },
            new Condition { name = "random 30%", link = () => new LinkProfile { Loss = 0.30 } },
            new Condition { name = "burst 2%/0.3", link = () => new LinkProfile { BurstEnter = 0.02, BurstExit = 0.3 } },
            new Condition { name = "burst 2%/0.1", link = () => new LinkProfile { BurstEnter = 0.02, BurstExit = 0.1 } },
            new Condition { name = "burst 5%/0.1", link = () => new LinkProfile { BurstEnter = 0.05, BurstExit = 0.1 } },
            new Condition { name = "burst 5%/0.05", link = () => new LinkProfile { BurstEnter = 0.05, BurstExit = 0.05 } },
        };

        [Test]
        public void Sweep()
        {
            TestContext.Out.WriteLine("condition        frozen s/peer  holds  spec  worst gap s  KB/s sent  (mean over " + Seeds.Length + " seeds, 60 s)");
            foreach (Condition c in Conditions)
            {
                double frozen = 0, worst = 0, bytes = 0;
                int holds = 0, spec = 0;
                foreach (int seed in Seeds)
                {
                    LockstepScenario s = new LockstepScenario
                    {
                        Seed = seed,
                        Seconds = 60,
                        ZeroToOne = c.link(),
                        OneToZero = c.link()
                    }.Run();
                    s.AssertMatchesReference(minimum: 5);
                    foreach (HarnessPeer p in s.Peers)
                    {
                        frozen += p.FrozenSeconds;
                        holds += p.Holds;
                        spec += p.SpeculationStarts;
                        if (p.WorstTickGap > worst) worst = p.WorstTickGap;
                        bytes += s.Links[p.Player].BytesSent;
                    }
                }
                int peers = Seeds.Length * 2;
                TestContext.Out.WriteLine(string.Format("{0,-16} {1,13:F2}  {2,5}  {3,4}  {4,11:F2}  {5,9:F2}",
                    c.name, frozen / peers, holds, spec, worst, bytes / peers / 60.0 / 1024.0));
            }
        }
    }
}
