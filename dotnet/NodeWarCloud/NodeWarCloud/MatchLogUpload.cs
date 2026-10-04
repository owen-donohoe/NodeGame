using System;

namespace NodeWar.Cloud
{
    public static class MatchLogUpload
    {
        public static bool TryDecode(string base64, out byte[] bytes, out string error)
        {
            bytes = null;
            error = null;
            if (base64 == null) { error = "bad base64"; return false; }
            // Bound allocation before decoding; whitespace counts toward transport size.
            if (base64.Length > 4 * ((Referee.MaxLogBytes + 2) / 3))
            { error = "log exceeds 512 KB"; return false; }
            try { bytes = Convert.FromBase64String(base64); }
            catch (FormatException) { error = "bad base64"; return false; }
            // The last base64 quartet can decode up to two bytes beyond the limit.
            if (bytes.Length > Referee.MaxLogBytes)
            { bytes = null; error = "log exceeds 512 KB"; return false; }
            return true;
        }
    }
}
