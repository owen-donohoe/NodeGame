using System;
using System.Text;
using System.Threading.Tasks;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class LocalMatchReportServiceTests
    {
        private static byte[] Log() => Encoding.UTF8.GetBytes("log-bytes");

        [Test]
        public void NullMatchIdThrows()
        {
            var service = new LocalMatchReportService();
            Assert.ThrowsAsync<ArgumentException>(() => service.ReportAsync(null, Log()));
        }

        [Test]
        public void EmptyMatchIdThrows()
        {
            var service = new LocalMatchReportService();
            Assert.ThrowsAsync<ArgumentException>(() => service.ReportAsync(string.Empty, Log()));
        }

        [Test]
        public void NullLogThrows()
        {
            var service = new LocalMatchReportService();
            Assert.ThrowsAsync<ArgumentException>(() => service.ReportAsync("match-1", null));
        }

        [Test]
        public void EmptyLogThrows()
        {
            var service = new LocalMatchReportService();
            Assert.ThrowsAsync<ArgumentException>(() => service.ReportAsync("match-1", Array.Empty<byte>()));
        }

        [Test]
        public async Task DefaultResultIsPendingWithLocalFakeMessage()
        {
            var service = new LocalMatchReportService();
            MatchReportingResult result = await service.ReportAsync("match-1", Log());
            Assert.That(result.state, Is.EqualTo(MatchRecordState.Pending));
            Assert.That(result.message, Is.EqualTo("local fake"));
        }

        [Test]
        public async Task RecordsEachCallInOrder()
        {
            var service = new LocalMatchReportService();
            byte[] firstLog = Log();
            byte[] secondLog = Encoding.UTF8.GetBytes("second");

            await service.ReportAsync("match-1", firstLog);
            await service.ReportAsync("match-2", secondLog);

            Assert.That(service.Calls.Count, Is.EqualTo(2));
            Assert.That(service.Calls[0].MatchId, Is.EqualTo("match-1"));
            Assert.That(service.Calls[0].Log, Is.SameAs(firstLog));
            Assert.That(service.Calls[1].MatchId, Is.EqualTo("match-2"));
            Assert.That(service.Calls[1].Log, Is.SameAs(secondLog));
        }

        [Test]
        public async Task ResultIsConfigurable()
        {
            var service = new LocalMatchReportService
            {
                Result = new MatchReportingResult { state = MatchRecordState.Settled, message = "done" }
            };

            MatchReportingResult result = await service.ReportAsync("match-1", Log());

            Assert.That(result.state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(result.message, Is.EqualTo("done"));
        }
    }
}
