using System;
using Unity.Services.CloudCode.Core;

namespace NodeWar.Cloud
{
    public class RefereeModule
    {
        [CloudCodeFunction("VerifyMatch")]
        public RefereeVerdict VerifyMatch(IExecutionContext context, string logBase64)
        {
            if (logBase64 == null) return RefereeVerdict.Refused("bad base64");
            // Bound the allocation before decoding. Canonical base64 of MaxLogBytes
            // fits exactly; whitespace counts toward the transport limit too.
            if (logBase64.Length > 4 * ((Referee.MaxLogBytes + 2) / 3))
                return RefereeVerdict.Refused("log exceeds 512 KB");
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(logBase64);
            }
            catch (FormatException)
            {
                return RefereeVerdict.Refused("bad base64");
            }
            return new Referee(BalanceCatalog.Embedded).Verify(bytes);
        }
    }
}
