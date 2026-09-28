namespace NodeWar.Backend
{
    /// <summary>
    /// Decides whether a player state returned by an in-flight call is still
    /// safe to remember. A call started for one account can finish after the
    /// user has switched to another; remembering it then would attribute the
    /// wrong player's state to whoever is signed in now.
    /// </summary>
    public static class RememberGuard
    {
        /// <summary>
        /// True only if the call was started for a real player and that same
        /// player is still the one signed in when it finishes.
        /// </summary>
        public static bool ShouldRemember(string requestedFor, string currentPlayerId)
        {
            if (string.IsNullOrEmpty(requestedFor)) return false;
            if (string.IsNullOrEmpty(currentPlayerId)) return false;
            return requestedFor == currentPlayerId;
        }
    }
}
