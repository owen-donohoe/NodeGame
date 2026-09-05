using NUnit.Framework;
using NodeWar.View.Outline;

namespace NodeWar.Tests
{
    /// <summary>
    /// A node can be Selected and Contested in the same frame, and only one line
    /// can be drawn. These cases pin which one wins.
    ///
    /// Priority is expressed as the numeric order of the OutlineStyle members,
    /// which means reordering the enum silently changes game behaviour rather
    /// than breaking a build. That is a real hazard, so the order is asserted
    /// here: a reorder fails a test instead of quietly changing what the player
    /// sees.
    /// </summary>
    public class OutlineStyleTests
    {
        [Test]
        public void StyleOrder_IsThePriorityOrder_HighestWins()
        {
            Assert.Less((int)OutlineStyle.None, (int)OutlineStyle.Hover);
            Assert.Less((int)OutlineStyle.Hover, (int)OutlineStyle.Contested);
            Assert.Less((int)OutlineStyle.Contested, (int)OutlineStyle.Selected);
            Assert.Less((int)OutlineStyle.Selected, (int)OutlineStyle.CommandAck);
        }

        [Test]
        public void StyleCount_MatchesTheEnum_SoThePaletteIsNeverShort()
        {
            // The composite indexes the palette by style value without a bounds
            // check, so a mismatch here is a garbage colour, not an exception.
            Assert.AreEqual((int)OutlineStyle.CommandAck + 1, OutlineStyleMask.StyleCount);
        }

        [Test]
        public void Highest_OfAnEmptyMask_IsNone()
        {
            Assert.AreEqual(OutlineStyle.None, OutlineStyleMask.Highest(0u));
        }

        [Test]
        public void Highest_OfSelectedAndContested_IsSelected()
        {
            uint mask = 0u;
            mask = OutlineStyleMask.With(mask, OutlineStyle.Contested, true);
            mask = OutlineStyleMask.With(mask, OutlineStyle.Selected, true);

            // The case from the brief: a node that is both selected and
            // contested reads as selected, because selection is the thing the
            // player just did.
            Assert.AreEqual(OutlineStyle.Selected, OutlineStyleMask.Highest(mask));
        }

        [Test]
        public void Highest_WithCommandAckPresent_BeatsEverythingElse()
        {
            uint mask = 0u;
            mask = OutlineStyleMask.With(mask, OutlineStyle.Hover, true);
            mask = OutlineStyleMask.With(mask, OutlineStyle.Contested, true);
            mask = OutlineStyleMask.With(mask, OutlineStyle.Selected, true);
            mask = OutlineStyleMask.With(mask, OutlineStyle.CommandAck, true);

            Assert.AreEqual(OutlineStyle.CommandAck, OutlineStyleMask.Highest(mask));
        }

        [Test]
        public void With_ClearingTheWinner_FallsBackToTheNextHighest()
        {
            uint mask = 0u;
            mask = OutlineStyleMask.With(mask, OutlineStyle.Hover, true);
            mask = OutlineStyleMask.With(mask, OutlineStyle.Selected, true);

            Assert.AreEqual(OutlineStyle.Selected, OutlineStyleMask.Highest(mask));

            mask = OutlineStyleMask.With(mask, OutlineStyle.Selected, false);

            // Deselecting a hovered node leaves the hover outline, rather than
            // dropping the outline entirely and popping back on when the
            // pointer moves a pixel.
            Assert.AreEqual(OutlineStyle.Hover, OutlineStyleMask.Highest(mask));
        }

        [Test]
        public void With_SettingTheSameIntentTwice_ChangesNothing()
        {
            uint once = OutlineStyleMask.With(0u, OutlineStyle.Hover, true);
            uint twice = OutlineStyleMask.With(once, OutlineStyle.Hover, true);

            Assert.AreEqual(once, twice);
        }

        [Test]
        public void With_ClearingAnIntentThatWasNeverSet_ChangesNothing()
        {
            uint mask = OutlineStyleMask.With(0u, OutlineStyle.Selected, true);
            uint after = OutlineStyleMask.With(mask, OutlineStyle.Hover, false);

            Assert.AreEqual(mask, after);
        }

        [Test]
        public void With_None_IsIgnoredBecauseNoneIsTheAbsenceOfEveryIntent()
        {
            uint mask = OutlineStyleMask.With(0u, OutlineStyle.Selected, true);

            // Letting a caller "set None" would make None both a member and a
            // state, and Highest would then have to break the tie between them.
            Assert.AreEqual(mask, OutlineStyleMask.With(mask, OutlineStyle.None, true));
            Assert.AreEqual(mask, OutlineStyleMask.With(mask, OutlineStyle.None, false));
        }

        [Test]
        public void Has_ReportsNoneOnlyWhenNoIntentIsSet()
        {
            uint empty = 0u;
            Assert.IsTrue(OutlineStyleMask.Has(empty, OutlineStyle.None));

            uint hovered = OutlineStyleMask.With(empty, OutlineStyle.Hover, true);
            Assert.IsFalse(OutlineStyleMask.Has(hovered, OutlineStyle.None));
            Assert.IsTrue(OutlineStyleMask.Has(hovered, OutlineStyle.Hover));
            Assert.IsFalse(OutlineStyleMask.Has(hovered, OutlineStyle.Selected));
        }
    }
}
