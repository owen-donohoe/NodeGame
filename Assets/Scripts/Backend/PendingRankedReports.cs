using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace NodeWar.Backend
{
    /// <summary>
    /// Ranked logs the server has not answered yet. A ranked match only settles
    /// from its logs, so an upload lost to one network error must not be lost
    /// for good: the player's claim would stay held and their next queue would
    /// ask them to forfeit a match they may have won.
    ///
    /// One file per match, named for the player who played it, so a different
    /// account signed in on this device never uploads someone else's log (the
    /// server would refuse it anyway). A file is removed once the server gives
    /// any answer, accepted or refused: the same bytes would get the same
    /// answer again. It stays only when the call itself fails.
    ///
    /// Retried after every ranked match, when the lobby loads, and before a
    /// ranked queue starts. Separate from LocalMatchLogStore, which prunes to
    /// the newest 20 and would eventually delete an unsent log.
    /// </summary>
    public static class PendingRankedReports
    {
        private const string Separator = "__";

        public static string Folder => Path.Combine(Application.persistentDataPath, "PendingReports");

        private static Task running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            running = null;
        }

        /// <summary>Keeps a finished ranked log until the server answers it.</summary>
        public static void Add(string playerId, string matchId, byte[] log)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(matchId) || log == null) return;
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(PathFor(playerId, matchId), log);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[RankedReports] Could not keep match " + matchId + " for retry: " + e.Message);
            }
        }

        /// <summary>
        /// Uploads every pending log for the signed-in player. Concurrent callers
        /// share one pass. Never throws.
        /// </summary>
        public static Task RetryAsync()
        {
            if (running == null || running.IsCompleted) running = RetryAllAsync();
            return running;
        }

        private static async Task RetryAllAsync()
        {
            string playerId;
            string[] files;
            try
            {
                playerId = BackendServices.Account.Current?.PlayerId;
                if (string.IsNullOrEmpty(playerId) || !Directory.Exists(Folder)) return;
                files = Directory.GetFiles(Folder, playerId + Separator + "*" + LocalMatchLogStore.Extension);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RankedReports] Could not list pending reports: " + e.Message);
                return;
            }

            // Ordinal order, so retries never depend on what the file system returns.
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string path in files)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                string matchId = name.Substring(playerId.Length + Separator.Length);
                try
                {
                    byte[] log = File.ReadAllBytes(path);
                    MatchReportingResult result = await BackendServices.MatchReports.ReportAsync(matchId, log);
                    Debug.Log("[RankedReports] " + matchId + ": " +
                              (result?.state?.ToString() ?? "refused") + " " + (result?.message ?? ""));
                    File.Delete(path);
                }
                catch (Exception e)
                {
                    // Offline, a timeout, a server error: keep it for the next pass.
                    Debug.LogWarning("[RankedReports] Upload for " + matchId + " failed, will retry: " + e.Message);
                }
            }
        }

        private static string PathFor(string playerId, string matchId)
        {
            return Path.Combine(Folder, playerId + Separator + matchId + LocalMatchLogStore.Extension);
        }
    }
}
