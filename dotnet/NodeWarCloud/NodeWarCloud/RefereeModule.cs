using Unity.Services.CloudCode.Core;

namespace NodeWar.Cloud
{
    public class RefereeModule
    {
        [CloudCodeFunction("VerifyMatch")]
        public RefereeVerdict VerifyMatch(IExecutionContext context, string logBase64)
        {
            if (!MatchLogUpload.TryDecode(logBase64, out byte[] bytes, out string error))
                return RefereeVerdict.Refused(error);
            return new Referee(BalanceCatalog.Embedded).Verify(bytes);
        }
    }
}
