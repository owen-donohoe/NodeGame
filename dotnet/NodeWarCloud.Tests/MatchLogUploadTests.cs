using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class MatchLogUploadTests
    {
        [TestCase(null, "bad base64")]
        [TestCase("!", "bad base64")]
        public async Task BothEndpointsShareTransportRefusalsBeforeStoreAccess(string input, string error)
        {
            Assert.That(new RefereeModule().VerifyMatch(null, input).error, Is.EqualTo(error));
            Assert.That((await new MatchReportingModule(null).ReportMatch(null, "m", input)).message, Is.EqualTo(error));
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(3, false)]
        public async Task ExactByteLimitIsEnforcedIncludingSharedFinalBase64Quartet(int extra, bool accepted)
        {
            string input = Convert.ToBase64String(new byte[Referee.MaxLogBytes + extra]);
            Assert.That(MatchLogUpload.TryDecode(input, out byte[] bytes, out string error), Is.EqualTo(accepted));
            if (accepted)
            {
                Assert.That(bytes.Length, Is.EqualTo(Referee.MaxLogBytes));
                Assert.That(error, Is.Null);
            }
            else
            {
                Assert.That(new RefereeModule().VerifyMatch(null, input).error, Is.EqualTo("log exceeds 512 KB"));
                Assert.That((await new MatchReportingModule(null).ReportMatch(null, "m", input)).message,
                    Is.EqualTo("log exceeds 512 KB"));
            }
        }
    }
}
