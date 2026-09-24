using NUnit.Framework;
using UnityEngine;
using NodeWar.View.Outline;

namespace NodeWar.Tests
{
    /// <summary>
    /// The registry's job is the rule that a group holds an ID only while it is
    /// actually outlined. Everything expensive downstream depends on it: whether
    /// the renderer feature can skip both passes on an idle frame, whether ID
    /// exhaustion is reachable, and whether a dead villager keeps an outline.
    /// </summary>
    public class OutlineRegistryTests
    {
        /// <summary>
        /// Stands in for OutlineGroup. Exists so these cases need no GameObject,
        /// no scene and no render pipeline -- the registry's logic is the thing
        /// under test, not Unity's component lifecycle.
        /// </summary>
        private sealed class FakeGroup : IOutlineGroup
        {
            public OutlineStyle Style { get; set; }
            public int OutlineId { get; set; }
            public Renderer[] Renderers => System.Array.Empty<Renderer>();
            public Color Tint => Color.clear;
        }

        private static OutlineRegistry NewRegistry() => new OutlineRegistry();

        private static FakeGroup Outlined(OutlineStyle style) =>
            new FakeGroup { Style = style, OutlineId = OutlineIdAllocator.None };

        [Test]
        public void Active_WithNothingOutlined_IsEmptySoBothPassesCanBeSkipped()
        {
            OutlineRegistry registry = NewRegistry();

            // This is the common case in normal play. It has to cost nothing.
            Assert.AreEqual(0, registry.ActiveCount);
            Assert.IsFalse(registry.HasWork);
        }

        [Test]
        public void SyncStyle_FromNoneToSelected_RentsAnIdAndJoinsTheActiveList()
        {
            OutlineRegistry registry = NewRegistry();
            FakeGroup group = Outlined(OutlineStyle.Selected);

            registry.SyncStyle(group);

            Assert.AreNotEqual(OutlineIdAllocator.None, group.OutlineId);
            Assert.AreEqual(1, registry.ActiveCount);
            Assert.IsTrue(registry.HasWork);
        }

        [Test]
        public void SyncStyle_ForAGroupWithNoStyle_GrantsNothing()
        {
            OutlineRegistry registry = NewRegistry();
            FakeGroup group = Outlined(OutlineStyle.None);

            registry.SyncStyle(group);

            // Registration is not what earns an ID -- having a style is. This is
            // what keeps exhaustion out of reach on a board with far more than
            // 255 nodes and villagers over a match.
            Assert.AreEqual(OutlineIdAllocator.None, group.OutlineId);
            Assert.AreEqual(0, registry.ActiveCount);
        }

        [Test]
        public void SyncStyle_BackToNone_ReleasesTheIdAndLeavesTheActiveList()
        {
            OutlineRegistry registry = NewRegistry();
            FakeGroup group = Outlined(OutlineStyle.Selected);

            registry.SyncStyle(group);
            group.Style = OutlineStyle.None;
            registry.SyncStyle(group);

            Assert.AreEqual(OutlineIdAllocator.None, group.OutlineId);
            Assert.AreEqual(0, registry.ActiveCount);
            Assert.IsFalse(registry.HasWork);
        }

        [Test]
        public void SyncStyle_BetweenTwoOutlinedStyles_KeepsTheSameId()
        {
            OutlineRegistry registry = NewRegistry();
            FakeGroup group = Outlined(OutlineStyle.Selected);

            registry.SyncStyle(group);
            int granted = group.OutlineId;

            group.Style = OutlineStyle.Contested;
            registry.SyncStyle(group);

            // The stability guarantee: an ID is constant for as long as a group
            // is continuously outlined. Colour comes from the style index, not
            // the ID, so nothing about the picture depends on this -- but an
            // ID that churned every style change would flicker anything that
            // ever did key off it.
            Assert.AreEqual(granted, group.OutlineId);
            Assert.AreEqual(1, registry.ActiveCount);
        }

        [Test]
        public void SyncStyle_CalledRepeatedlyWithNoChange_DoesNotRentTwice()
        {
            OutlineRegistry registry = NewRegistry();
            FakeGroup group = Outlined(OutlineStyle.Hover);

            registry.SyncStyle(group);
            int granted = group.OutlineId;

            registry.SyncStyle(group);
            registry.SyncStyle(group);

            Assert.AreEqual(granted, group.OutlineId);
            Assert.AreEqual(1, registry.ActiveCount);
        }

        [Test]
        public void SyncStyle_ForSeveralGroups_GivesEachADistinctId()
        {
            OutlineRegistry registry = NewRegistry();

            FakeGroup node = Outlined(OutlineStyle.Selected);
            FakeGroup villager = Outlined(OutlineStyle.Selected);

            registry.SyncStyle(node);
            registry.SyncStyle(villager);

            // A villager standing on a node must not merge into one blob with
            // it. Distinct IDs are the entire mechanism for that.
            Assert.AreNotEqual(node.OutlineId, villager.OutlineId);
            Assert.AreEqual(2, registry.ActiveCount);
        }

        [Test]
        public void SyncStyle_WhenIdsAreExhausted_DropsTheGroupRatherThanMergingIt()
        {
            OutlineRegistry registry = NewRegistry();

            for (int i = 0; i < OutlineIdAllocator.Capacity; i++)
            {
                registry.SyncStyle(Outlined(OutlineStyle.Selected));
            }

            FakeGroup overflow = Outlined(OutlineStyle.Selected);
            registry.SyncStyle(overflow);

            // Dropped, not merged. Sharing an ID would erase the line between
            // two groups and draw a confident lie; no outline is the honest
            // failure.
            Assert.AreEqual(OutlineIdAllocator.None, overflow.OutlineId);
            Assert.AreEqual(OutlineIdAllocator.Capacity, registry.ActiveCount);
            Assert.AreEqual(1, registry.DroppedCount);
        }

        [Test]
        public void Remove_WhileOutlined_ReleasesTheIdForReuse()
        {
            OutlineRegistry registry = NewRegistry();
            FakeGroup group = Outlined(OutlineStyle.Selected);

            registry.SyncStyle(group);
            int granted = group.OutlineId;

            registry.Remove(group);

            Assert.AreEqual(OutlineIdAllocator.None, group.OutlineId);
            Assert.AreEqual(0, registry.ActiveCount);

            FakeGroup next = Outlined(OutlineStyle.Hover);
            registry.SyncStyle(next);
            Assert.AreEqual(granted, next.OutlineId);
        }

        [Test]
        public void Remove_ForAGroupThatWasNeverOutlined_IsANoOp()
        {
            OutlineRegistry registry = NewRegistry();
            FakeGroup group = Outlined(OutlineStyle.None);

            Assert.DoesNotThrow(() => registry.Remove(group));
            Assert.AreEqual(0, registry.ActiveCount);
        }

        [Test]
        public void Remove_CalledTwice_IsANoOpTheSecondTime()
        {
            OutlineRegistry registry = NewRegistry();
            FakeGroup group = Outlined(OutlineStyle.Selected);

            registry.SyncStyle(group);
            registry.Remove(group);

            Assert.DoesNotThrow(() => registry.Remove(group));
            Assert.AreEqual(0, registry.ActiveCount);
        }

        [Test]
        public void SyncStyleAndRemove_TolerateNull()
        {
            OutlineRegistry registry = NewRegistry();

            Assert.DoesNotThrow(() => registry.SyncStyle(null));
            Assert.DoesNotThrow(() => registry.Remove(null));
        }

        [Test]
        public void Clear_ReleasesEveryGroupAndResetsTheirIds()
        {
            OutlineRegistry registry = NewRegistry();

            FakeGroup first = Outlined(OutlineStyle.Selected);
            FakeGroup second = Outlined(OutlineStyle.Contested);
            registry.SyncStyle(first);
            registry.SyncStyle(second);

            registry.Clear();

            Assert.AreEqual(0, registry.ActiveCount);
            Assert.AreEqual(OutlineIdAllocator.None, first.OutlineId);
            Assert.AreEqual(OutlineIdAllocator.None, second.OutlineId);
        }
    }
}
