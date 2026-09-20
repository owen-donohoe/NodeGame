using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class ViewSideTests
    {
        // ===== side <-> yaw =====

        [TestCase(0, 0f)]
        [TestCase(1, 90f)]
        [TestCase(2, 180f)]
        [TestCase(3, 270f)]
        [TestCase(4, 0f)]
        [TestCase(-1, 270f)]
        public void Yaw_is_ninety_degrees_per_side(int side, float yaw)
        {
            Assert.AreEqual(yaw, ViewSide.Yaw(side));
        }

        [TestCase(0f, 0)]
        [TestCase(44f, 0)]
        [TestCase(46f, 1)]
        [TestCase(180f, 2)]
        [TestCase(269.9f, 3)]
        [TestCase(359f, 0)]
        [TestCase(-90f, 3)]
        [TestCase(450f, 1)]
        public void FromYaw_snaps_to_the_nearest_quarter_turn(float yaw, int side)
        {
            Assert.AreEqual(side, ViewSide.FromYaw(yaw));
        }

        [Test]
        public void FromYaw_inverts_Yaw()
        {
            for (int s = 0; s < ViewSide.Count; s++)
                Assert.AreEqual(s, ViewSide.FromYaw(ViewSide.Yaw(s)));
        }

        [TestCase(0, 1, 1)]
        [TestCase(3, 1, 0)]
        [TestCase(0, -1, 3)]
        [TestCase(1, 4, 1)]
        [TestCase(2, -6, 0)]
        public void Rotate_steps_and_wraps(int side, int turns, int expected)
        {
            Assert.AreEqual(expected, ViewSide.Rotate(side, turns));
        }

        // ===== sort axis =====

        [Test]
        public void SortAxis_points_away_from_the_camera_on_every_side()
        {
            // Measured in Unity: a camera looking +Z draws CustomAxis +Z with
            // the near sprite on top. P1 (yaw 0) -> +Z, P0 (yaw 180) -> -Z.
            ViewSide.SortAxis(0, out float x0, out float z0);
            Assert.AreEqual(0f, x0);
            Assert.AreEqual(1f, z0);

            ViewSide.SortAxis(2, out float x2, out float z2);
            Assert.AreEqual(0f, x2);
            Assert.AreEqual(-1f, z2);

            for (int s = 0; s < ViewSide.Count; s++)
            {
                ViewSide.Forward(s, out float fx, out float fz);
                ViewSide.SortAxis(s, out float ax, out float az);
                Assert.AreEqual(1f, fx * ax + fz * az);
            }
        }

        [Test]
        public void Forward_matches_a_yaw_rotation_of_plus_z()
        {
            // Quaternion.Euler(0, yaw, 0) * forward = (sin yaw, 0, cos yaw).
            for (int s = 0; s < ViewSide.Count; s++)
            {
                double rad = ViewSide.Yaw(s) * System.Math.PI / 180.0;
                ViewSide.Forward(s, out float fx, out float fz);
                Assert.AreEqual(System.Math.Sin(rad), fx, 1e-6);
                Assert.AreEqual(System.Math.Cos(rad), fz, 1e-6);
            }
        }

        // ===== core offset and ResolveViewer =====

        [TestCase(0f, 12f, 2)]   // core on the high-Z edge: look toward -Z
        [TestCase(0f, -12f, 0)]  // low-Z edge: look toward +Z
        [TestCase(12f, 0f, 3)]   // high-X edge: look toward -X
        [TestCase(-12f, 0f, 1)]  // low-X edge: look toward +X
        [TestCase(5f, 12f, 2)]   // nearest quarter turn wins
        [TestCase(-5f, -12f, 0)]
        [TestCase(12f, -5f, 3)]
        [TestCase(-12f, 5f, 1)]
        [TestCase(8f, 8f, 2)]    // exact diagonal prefers Z
        [TestCase(0f, 0f, 0)]
        public void FromCoreOffset_snaps_to_the_nearest_side(float dx, float dz, int side)
        {
            Assert.AreEqual(side, ViewSide.FromCoreOffset(dx, dz));
        }

        [Test]
        public void ResolveViewer_two_players_keeps_the_existing_yaws()
        {
            // The default 4x7 board at nodeScale 6: cores at (1,6) and (2,0), centre (9,18).
            float[] cx = { 6f, 12f };
            float[] cz = { 36f, 0f };

            int p0 = ViewSide.ResolveViewer(ViewerMode.Player, 0, cx, cz, 9f, 18f);
            int p1 = ViewSide.ResolveViewer(ViewerMode.Player, 1, cx, cz, 9f, 18f);

            Assert.AreEqual(180f, ViewSide.Yaw(p0));
            Assert.AreEqual(0f, ViewSide.Yaw(p1));
        }

        [Test]
        public void ResolveViewer_four_players_one_core_per_edge()
        {
            // Cores at the middle of each edge of a square board centred on the origin.
            float[] cx = { 0f, 10f, 0f, -10f };
            float[] cz = { 10f, 0f, -10f, 0f };

            Assert.AreEqual(2, ViewSide.ResolveViewer(ViewerMode.Player, 0, cx, cz, 0f, 0f));
            Assert.AreEqual(3, ViewSide.ResolveViewer(ViewerMode.Player, 1, cx, cz, 0f, 0f));
            Assert.AreEqual(0, ViewSide.ResolveViewer(ViewerMode.Player, 2, cx, cz, 0f, 0f));
            Assert.AreEqual(1, ViewSide.ResolveViewer(ViewerMode.Player, 3, cx, cz, 0f, 0f));
        }

        [Test]
        public void ResolveViewer_layout_rotated_ninety_degrees_rotates_the_sides()
        {
            // The two-player board laid along X instead of Z.
            float[] cx = { 36f, 0f };
            float[] cz = { 6f, 12f };

            Assert.AreEqual(3, ViewSide.ResolveViewer(ViewerMode.Player, 0, cx, cz, 18f, 9f));
            Assert.AreEqual(1, ViewSide.ResolveViewer(ViewerMode.Player, 1, cx, cz, 18f, 9f));
        }

        [Test]
        public void ResolveViewer_spectator_is_side_on_whatever_the_player_id()
        {
            float[] cx = { 6f, 12f };
            float[] cz = { 36f, 0f };

            int side = ViewSide.ResolveViewer(ViewerMode.Spectator, 0, cx, cz, 9f, 18f);

            Assert.AreEqual(ViewSide.SpectatorDefault, side);
            Assert.IsTrue(side == 1 || side == 3);
            Assert.AreEqual(side, ViewSide.ResolveViewer(ViewerMode.Spectator, 1, null, null, 0f, 0f));
        }

        [Test]
        public void ResolveViewer_falls_back_to_side_zero_when_no_core_is_known()
        {
            float[] cx = { float.NaN, 12f };
            float[] cz = { float.NaN, 0f };

            Assert.AreEqual(0, ViewSide.ResolveViewer(ViewerMode.Player, 0, cx, cz, 9f, 18f));
            Assert.AreEqual(0, ViewSide.ResolveViewer(ViewerMode.Player, 5, cx, cz, 9f, 18f));
            Assert.AreEqual(0, ViewSide.ResolveViewer(ViewerMode.Player, -1, cx, cz, 9f, 18f));
            Assert.AreEqual(0, ViewSide.ResolveViewer(ViewerMode.Player, 0, null, null, 0f, 0f));
        }

        // ===== sort keys =====

        [Test]
        public void Depth_grows_toward_the_back_of_the_view()
        {
            // Side 0 looks +Z, so a point at higher Z is further and has the higher depth...
            Assert.Greater(ViewSide.Depth(0, 0f, 10f), ViewSide.Depth(0, 0f, 2f));
            // ...and side 2 looks -Z, so the same points swap.
            Assert.Less(ViewSide.Depth(2, 0f, 10f), ViewSide.Depth(2, 0f, 2f));
        }

        [Test]
        public void ComputeOrders_ranks_equal_heights_back_to_front()
        {
            float[] depth = { 5f, 9f, 1f };
            int[] height = { 0, 0, 0 };
            int[] orders = new int[3];

            ViewSide.ComputeOrders(depth, height, 3, orders);

            // Furthest (9) draws first, nearest (1) last.
            Assert.AreEqual(new[] { 1, 0, 2 }, orders);
        }

        [Test]
        public void ComputeOrders_height_beats_depth()
        {
            // A sprite on top of another stays in front even when it is further back.
            float[] depth = { 1f, 100f };
            int[] height = { 0, 1 };
            int[] orders = new int[2];

            ViewSide.ComputeOrders(depth, height, 2, orders);

            Assert.Greater(orders[1], orders[0]);
        }

        [Test]
        public void ComputeOrders_ties_fall_back_to_index()
        {
            float[] depth = { 3f, 3f, 3f };
            int[] height = { 0, 0, 0 };
            int[] orders = new int[3];

            ViewSide.ComputeOrders(depth, height, 3, orders);

            Assert.AreEqual(new[] { 0, 1, 2 }, orders);
        }

        [Test]
        public void ComputeOrders_flips_with_the_side()
        {
            // Two boxes a row apart in Z. Seen from either end the nearer one draws last.
            float[] depth = new float[2];
            int[] height = { 0, 0 };
            int[] orders = new int[2];

            depth[0] = ViewSide.Depth(0, 0f, 0f);
            depth[1] = ViewSide.Depth(0, 0f, 6f);
            ViewSide.ComputeOrders(depth, height, 2, orders);
            Assert.Less(orders[1], orders[0], "side 0 looks +Z: high Z is further, so it draws first");

            depth[0] = ViewSide.Depth(2, 0f, 0f);
            depth[1] = ViewSide.Depth(2, 0f, 6f);
            ViewSide.ComputeOrders(depth, height, 2, orders);
            Assert.Less(orders[0], orders[1], "side 2 looks -Z: low Z is further, so it draws first");
        }

        [Test]
        public void ComputeOrders_side_on_views_sort_along_x()
        {
            float[] depth = new float[2];
            int[] height = { 0, 0 };
            int[] orders = new int[2];

            // Side 1 looks +X: high X is further.
            depth[0] = ViewSide.Depth(1, 0f, 0f);
            depth[1] = ViewSide.Depth(1, 6f, 0f);
            ViewSide.ComputeOrders(depth, height, 2, orders);
            Assert.Less(orders[1], orders[0]);

            // Side 3 looks -X: low X is further.
            depth[0] = ViewSide.Depth(3, 0f, 0f);
            depth[1] = ViewSide.Depth(3, 6f, 0f);
            ViewSide.ComputeOrders(depth, height, 2, orders);
            Assert.Less(orders[0], orders[1]);
        }

        [Test]
        public void ComputeOrders_clamps_height_inside_a_short()
        {
            float[] depth = { 0f, 0f };
            int[] height = { int.MaxValue, int.MinValue };
            int[] orders = new int[2];

            ViewSide.ComputeOrders(depth, height, 2, orders);

            Assert.LessOrEqual(orders[0], short.MaxValue);
            Assert.GreaterOrEqual(orders[1], short.MinValue);
        }
    }
}
