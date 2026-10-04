using System;
using System.IO;
using UnityEngine;

namespace NodeWar.Backend
{
    /// <summary>
    /// Where finished match logs go until the server takes them (Stage 7 of
    /// BACKEND-PLAN uploads each one for the referee). One file per match,
    /// named by match ID, and only the newest are kept: a log is only useful
    /// here as a replay or as training data, and neither needs every match a
    /// device has ever played.
    /// </summary>
    public static class LocalMatchLogStore
    {
        public const string Extension = ".nwml";
        public const int KeepNewest = 20;

        public static string Folder => Path.Combine(Application.persistentDataPath, "MatchLogs");

        /// <summary>
        /// Writes the log and prunes the folder. Returns the path, or null when
        /// the write failed: a lost log costs a replay, never the match, so this
        /// logs the failure rather than throwing into the end-of-match flow.
        /// </summary>
        public static string Save(string matchId, byte[] bytes)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, matchId + Extension);
                File.WriteAllBytes(path, bytes);
                Prune();
                Debug.Log("[MatchLog] Saved " + bytes.Length + " bytes to " + path);
                return path;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[MatchLog] Could not save match " + matchId + ": " + e.Message);
                return null;
            }
        }

        private static void Prune()
        {
            FileInfo[] files = new DirectoryInfo(Folder).GetFiles("*" + Extension);
            if (files.Length <= KeepNewest) return;

            // Newest first; the name breaks ties so the order never depends on
            // what the file system happens to return.
            Array.Sort(files, (a, b) =>
            {
                int byTime = b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc);
                return byTime != 0 ? byTime : string.CompareOrdinal(a.Name, b.Name);
            });
            for (int i = KeepNewest; i < files.Length; i++)
                files[i].Delete();
        }
    }
}
