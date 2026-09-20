using NUnit.Framework;
using NodeWar.Lobby;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// The trophy bar's fixed-width window. Reading a fill must not move it;
    /// updating moves it only past 70% or below its minimum, and never below
    /// zero. The endpoints matter as much as the fill: a clamped fill alone
    /// can hide a window that stopped following the player's trophies.
    /// </summary>
    [TestFixture]
    public class TrophyBarLogicTests
    {
        // ===== Constructor =====

        [Test]
        public void Constructor_PlacesCurrentValueAtFortyPercent()
        {
            TrophyBarLogic bar = new TrophyBarLogic(100);

            // Suspected bug: the constructor promises ~50%, but starts at 40%.
            // Pin the current position rather than silently fixing the contract.
            Assert.AreEqual(60, bar.RangeMin);
            Assert.AreEqual(160, bar.RangeMax);
            Assert.AreEqual(0.4f, bar.GetFill(100));
        }

        [TestCase(-20, 0f)]
        [TestCase(0, 0f)]
        [TestCase(20, 0.2f)]
        [TestCase(40, 0.4f)]
        public void Constructor_ClampsTheWindowAtZero(int trophies, float expectedFill)
        {
            TrophyBarLogic bar = new TrophyBarLogic(trophies);

            Assert.AreEqual(0, bar.RangeMin);
            Assert.AreEqual(100, bar.RangeMax);
            Assert.AreEqual(expectedFill, bar.GetFill(trophies));
        }

        // ===== GetFill =====

        [TestCase(-1, 0f)]
        [TestCase(59, 0f)]
        [TestCase(60, 0f)]
        [TestCase(85, 0.25f)]
        [TestCase(100, 0.4f)]
        [TestCase(160, 1f)]
        [TestCase(161, 1f)]
        public void GetFill_ClampsWithoutMovingTheWindow(int trophies, float expectedFill)
        {
            TrophyBarLogic bar = new TrophyBarLogic(100);

            Assert.AreEqual(expectedFill, bar.GetFill(trophies));
            Assert.AreEqual(60, bar.RangeMin);
            Assert.AreEqual(160, bar.RangeMax);
        }

        // ===== UpdateAndGetFill =====

        [TestCase(60, 0f)]
        [TestCase(100, 0.4f)]
        [TestCase(129, 0.69f)]
        [TestCase(130, 0.7f)]
        public void Update_LeavesTheWindowAloneThroughExactlySeventyPercent(int trophies, float expectedFill)
        {
            TrophyBarLogic bar = new TrophyBarLogic(100);

            Assert.AreEqual(expectedFill, bar.UpdateAndGetFill(trophies));
            Assert.AreEqual(60, bar.RangeMin);
            Assert.AreEqual(160, bar.RangeMax);
        }

        [TestCase(131, 91, 191)]
        [TestCase(1000, 960, 1060)]
        public void Update_PastSeventyPercentShiftsToFortyPercent(int trophies, int expectedMin, int expectedMax)
        {
            TrophyBarLogic bar = new TrophyBarLogic(100);

            Assert.AreEqual(0.4f, bar.UpdateAndGetFill(trophies));
            Assert.AreEqual(expectedMin, bar.RangeMin);
            Assert.AreEqual(expectedMax, bar.RangeMax);
        }

        [Test]
        public void Update_BelowTheMinimumShiftsToThirtyPercent()
        {
            TrophyBarLogic bar = new TrophyBarLogic(100);

            Assert.AreEqual(0.3f, bar.UpdateAndGetFill(59));
            Assert.AreEqual(29, bar.RangeMin);
            Assert.AreEqual(129, bar.RangeMax);
        }

        [TestCase(-20, 0f)]
        [TestCase(0, 0f)]
        [TestCase(20, 0.2f)]
        public void Update_DownwardShiftStopsAtZero(int trophies, float expectedFill)
        {
            TrophyBarLogic bar = new TrophyBarLogic(500);

            Assert.AreEqual(expectedFill, bar.UpdateAndGetFill(trophies));
            Assert.AreEqual(0, bar.RangeMin);
            Assert.AreEqual(100, bar.RangeMax);
        }

        [Test]
        public void Update_RepeatedUpwardShiftsUseTheNewWindow()
        {
            TrophyBarLogic bar = new TrophyBarLogic(100);
            int[] trophies = { 131, 162, 193 };
            int[] minima = { 91, 122, 153 };
            int[] maxima = { 191, 222, 253 };

            for (int i = 0; i < trophies.Length; i++)
            {
                Assert.AreEqual(0.4f, bar.UpdateAndGetFill(trophies[i]));
                Assert.AreEqual(minima[i], bar.RangeMin);
                Assert.AreEqual(maxima[i], bar.RangeMax);

                // Receiving the same count twice must not keep scrolling.
                Assert.AreEqual(0.4f, bar.UpdateAndGetFill(trophies[i]));
                Assert.AreEqual(minima[i], bar.RangeMin);
                Assert.AreEqual(maxima[i], bar.RangeMax);
            }
        }

        [Test]
        public void Update_RepeatedDownwardShiftsUseTheNewWindow()
        {
            TrophyBarLogic bar = new TrophyBarLogic(500);
            int[] trophies = { 459, 428, 397 };
            int[] minima = { 429, 398, 367 };
            int[] maxima = { 529, 498, 467 };

            for (int i = 0; i < trophies.Length; i++)
            {
                Assert.AreEqual(0.3f, bar.UpdateAndGetFill(trophies[i]));
                Assert.AreEqual(minima[i], bar.RangeMin);
                Assert.AreEqual(maxima[i], bar.RangeMax);
            }
        }

        [Test]
        public void Update_CanReverseDirectionAfterAShift()
        {
            TrophyBarLogic bar = new TrophyBarLogic(100);

            Assert.AreEqual(0.4f, bar.UpdateAndGetFill(200));
            Assert.AreEqual(160, bar.RangeMin);
            Assert.AreEqual(260, bar.RangeMax);

            Assert.AreEqual(0.3f, bar.UpdateAndGetFill(159));
            Assert.AreEqual(129, bar.RangeMin);
            Assert.AreEqual(229, bar.RangeMax);

            Assert.AreEqual(0.4f, bar.UpdateAndGetFill(200));
            Assert.AreEqual(160, bar.RangeMin);
            Assert.AreEqual(260, bar.RangeMax);
        }

        // ===== Range size =====

        [TestCase(1, 100, 101, 0f)]
        [TestCase(2, 100, 102, 0f)]
        [TestCase(3, 99, 102, 1f / 3f)]
        [TestCase(7, 98, 105, 2f / 7f)]
        [TestCase(137, 46, 183, 54f / 137f)]
        public void Constructor_CustomWidthTruncatesTheFortyPercentOffset(int size, int expectedMin,
                                                                         int expectedMax, float expectedFill)
        {
            TrophyBarLogic bar = new TrophyBarLogic(100, size);

            Assert.AreEqual(expectedMin, bar.RangeMin);
            Assert.AreEqual(expectedMax, bar.RangeMax);
            Assert.AreEqual(expectedFill, bar.GetFill(100));
        }

        [Test]
        public void Update_OddWidthTruncatesThresholdAndBothShiftOffsets()
        {
            TrophyBarLogic bar = new TrophyBarLogic(100, 13);

            // The window starts at 95. Its 70% threshold is 104, not 105;
            // shifting up leaves five trophies below current, down leaves three.
            Assert.AreEqual(9f / 13f, bar.UpdateAndGetFill(104));
            Assert.AreEqual(95, bar.RangeMin);
            Assert.AreEqual(108, bar.RangeMax);

            Assert.AreEqual(5f / 13f, bar.UpdateAndGetFill(105));
            Assert.AreEqual(100, bar.RangeMin);
            Assert.AreEqual(113, bar.RangeMax);

            Assert.AreEqual(3f / 13f, bar.UpdateAndGetFill(99));
            Assert.AreEqual(96, bar.RangeMin);
            Assert.AreEqual(109, bar.RangeMax);
        }

        [Test]
        public void ZeroWidth_ReturnsZeroFillAndTracksTheCurrentValue()
        {
            TrophyBarLogic bar = new TrophyBarLogic(100, 0);

            Assert.AreEqual(100, bar.RangeMin);
            Assert.AreEqual(100, bar.RangeMax);
            Assert.AreEqual(0f, bar.GetFill(100));
            Assert.AreEqual(0f, bar.GetFill(120));

            Assert.AreEqual(0f, bar.UpdateAndGetFill(120));
            Assert.AreEqual(120, bar.RangeMin);
            Assert.AreEqual(120, bar.RangeMax);

            Assert.AreEqual(0f, bar.UpdateAndGetFill(90));
            Assert.AreEqual(90, bar.RangeMin);
            Assert.AreEqual(90, bar.RangeMax);
        }

        [Test]
        public void NegativeWidth_ReturnsZeroFillButLeavesAnInvertedWindow()
        {
            TrophyBarLogic bar = new TrophyBarLogic(100, -10);

            // Suspected bug: a negative width is accepted and puts RangeMax
            // below RangeMin. Returning zero fill hides the invalid window.
            Assert.AreEqual(104, bar.RangeMin);
            Assert.AreEqual(94, bar.RangeMax);
            Assert.AreEqual(0f, bar.GetFill(100));

            Assert.AreEqual(0f, bar.UpdateAndGetFill(100));
            Assert.AreEqual(103, bar.RangeMin);
            Assert.AreEqual(93, bar.RangeMax);
        }
    }
}
