using System;
using System.Collections.Generic;
using NodeWar.Backend;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    internal static class ProfileViews
    {
        private static readonly string[] Stats = { "Matches", "Wins", "Losses", "Win rate", "Streak", "Best" };
        public static VisualElement StatsGrid(PlayerStats stats)
        {
            var grid = new VisualElement { name = "public-profile-stats" };
            string[] values = ProfileStatsFormat.Values(stats);
            for (int row = 0; row < 3; row++)
            {
                var line = new VisualElement(); line.AddToClassList("g2-stat-row"); grid.Add(line);
                for (int col = 0; col < 2; col++)
                {
                    int i = row * 2 + col; var cell = new MkCard(); cell.AddToClassList("g2-stat");
                    cell.Add(GuildViews.Text(Stats[i], "mk-eyebrow")); cell.Add(GuildViews.Text(values[i], "mk-number")); line.Add(cell);
                }
            }
            return grid;
        }
        public static VisualElement Loadout(PublicProfileView profile)
        {
            var root = new VisualElement { name = "public-profile-loadout" }; root.AddToClassList("g2-loadout");
            root.Add(GuildViews.Text("Recent loadout", "mk-h3"));
            var back = MiniRow(); back.Add(Mini(profile.DistrictIds, 3, false)); back.Add(Mini(profile.DistrictIds, 4, false)); root.Add(back);
            var front = MiniRow(); for (int i = 0; i < 3; i++) front.Add(Mini(profile.DistrictIds, i, false)); root.Add(front);
            var suits = MiniRow(); for (int i = 0; i < 3; i++) suits.Add(Mini(profile.SuitIds, i, true)); root.Add(suits);
            return root;
        }
        private static VisualElement MiniRow() { var row = new VisualElement(); row.AddToClassList("g2-mini-row"); return row; }
        private static VisualElement Mini(string[] ids, int index, bool suit)
        {
            string id = ids != null && index < ids.Length ? ids[index] : null;
            var mini = new VisualElement { tooltip = id ?? "Empty slot", pickingMode = PickingMode.Ignore };
            mini.AddToClassList("mk-card"); mini.AddToClassList("g2-mini");
            if (string.IsNullOrEmpty(id)) mini.Add(GuildViews.Text("\u2014"));
            else
            {
                var icon = new LobbyIcon(suit ? LobbyIconKind.Suit : LobbyIconKind.District, LobbyIconContext.Profile); mini.Add(icon);
                mini.Add(GuildViews.Text(ItemTint.MonogramFor(id.Substring(id.IndexOf('_') + 1), id), "mk-caption"));
            }
            return mini;
        }
        public static VisualElement Ladder(int rr)
        {
            var road = new ScrollView { name = "profile-road" }; road.AddToClassList("g2-road"); LobbyScrollbars.Hide(road);
            var inside = new VisualElement(); inside.AddToClassList("g2-road-inner"); road.Add(inside);
            int count = RankTable.Thresholds.Count; float height = count * 150f; inside.style.height = height + 20f;
            var rail = new VisualElement(); rail.AddToClassList("g2-rail"); inside.Add(rail);
            var track = new VisualElement(); track.AddToClassList("g2-rtrack"); rail.Add(track);
            var rank = new RankDisplay(rr);
            float progress = rank.Span.HasValue ? rank.Fill : Mathf.Clamp01((rr - RankTable.Thresholds[count - 1]) / 700f);
            float marker = (rank.Arena + progress) * 150f;
            var fill = new VisualElement(); fill.AddToClassList("g2-rfill"); fill.style.height = marker; rail.Add(fill);
            for (int i = 0; i < count; i++)
            {
                var waypoint = new VisualElement(); waypoint.AddToClassList("g2-waypoint"); waypoint.style.bottom = i * 150f - 6f; rail.Add(waypoint);
            }
            var you = new VisualElement { tooltip = "You: " + rr + " RR" }; you.AddToClassList("g2-you"); you.style.bottom = marker - 10f; rail.Add(you);
            var bands = new VisualElement(); bands.AddToClassList("g2-era-bands"); inside.Add(bands);
            for (int i = count - 1; i >= 0; i--)
            {
                var band = new VisualElement(); band.AddToClassList("g2-era-band"); band.AddToClassList("g2-era--" + (Math.Min(4, i) + 1));
                band.Add(GuildViews.Text(RankTable.Names[i], "mk-h3"));
                band.Add(GuildViews.Text(RankTable.Thresholds[i] + " RR", "mk-cap"));
                for (int reward = 0; reward < 2; reward++)
                {
                    var notch = new VisualElement { tooltip = "Era reward" }; notch.AddToClassList("g2-reward-notch"); band.Add(notch);
                }
                bands.Add(band);
            }
            road.schedule.Execute(() => road.scrollOffset = new Vector2(0, Mathf.Max(0, height - marker - road.contentViewport.layout.height / 2))).ExecuteLater(32);
            return road;
        }
        public static VisualElement History(List<MatchSummary> summaries)
        {
            var strip = new VisualElement { name = "public-profile-recent" }; strip.AddToClassList("g2-win-history");
            var sorted = PlayerPanelPolicy.LastTen(summaries);
            for (int i = 0; i < Math.Min(10, sorted.Count); i++)
            {
                var cell = GuildViews.Text(sorted[i].Won ? "W" : "L"); cell.AddToClassList("g2-result"); cell.AddToClassList(sorted[i].Won ? "g2-gain" : "g2-loss"); strip.Add(cell);
            }
            if (sorted.Count == 0) strip.Add(GuildViews.Text("No battles yet", "mk-muted"));
            return strip;
        }
    }
}
