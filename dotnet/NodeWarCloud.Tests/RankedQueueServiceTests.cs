using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class RankedQueueStatusTests
    {
        [TestCase("InProgress", RankedQueueState.Searching)]
        [TestCase("Found", RankedQueueState.Found)]
        [TestCase("Failed", RankedQueueState.Failed)]
        [TestCase("Timeout", RankedQueueState.TimedOut)]
        public void MapsEverySdkStatusAndPreservesMessage(string status, RankedQueueState expected)
        {
            const string matchId = "2c8518f2-3e40-4e52-865a-d58f85bf2c40";
            const string message = "not compatible with the match definition";
            RankedQueueResult result = RankedQueueStatus.Map(status, matchId, message);

            Assert.That(result.state, Is.EqualTo(expected));
            Assert.That(result.matchId, Is.EqualTo(expected == RankedQueueState.Found ? matchId : null));
            Assert.That(result.message, Is.EqualTo(message));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void FoundWithoutMatchIdFails(string matchId)
        {
            RankedQueueResult result = RankedQueueStatus.Map("Found", matchId, "server message");
            Assert.That(result.state, Is.EqualTo(RankedQueueState.Failed));
            Assert.That(result.matchId, Is.Null);
            Assert.That(result.message, Does.Contain("no match ID").And.Contain("server message"));
        }

        [TestCase("FutureStatus")]
        [TestCase("0")]
        [TestCase("Cancelled")]
        [TestCase(null)]
        public void UnknownStatusFailsWithRawName(string status)
        {
            RankedQueueResult result = RankedQueueStatus.Map(status, "match-1", "server message");
            Assert.That(result.state, Is.EqualTo(RankedQueueState.Failed));
            Assert.That(result.matchId, Is.Null);
            Assert.That(result.message, Is.EqualTo("Unknown Matchmaker status: " + (status ?? "<null>") + ". server message"));
        }
    }

    public class LocalRankedQueueServiceTests
    {
        [Test]
        public async Task EmptyScriptKeepsSearching()
        {
            IRankedQueueService service = new LocalRankedQueueService();
            string ticket = await service.EnqueueAsync();
            Assert.That(ticket, Is.Not.Null.And.Not.Empty);
            Assert.That((await service.PollAsync(ticket)).state, Is.EqualTo(RankedQueueState.Searching));
            Assert.That((await service.PollAsync(ticket)).state, Is.EqualTo(RankedQueueState.Searching));
        }

        [TestCase(RankedQueueState.Found)]
        [TestCase(RankedQueueState.Failed)]
        [TestCase(RankedQueueState.TimedOut)]
        [TestCase(RankedQueueState.Cancelled)]
        public async Task PollsScriptInOrderThenRepeatsLastResult(RankedQueueState state)
        {
            var service = new LocalRankedQueueService();
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.Searching });
            service.PollResults.Add(new RankedQueueResult { state = state, matchId = "match-1", message = "scripted" });
            string ticket = await service.EnqueueAsync();

            Assert.That((await service.PollAsync(ticket)).state, Is.EqualTo(RankedQueueState.Searching));
            RankedQueueResult result = await service.PollAsync(ticket);
            Assert.That(result.state, Is.EqualTo(state));
            Assert.That(result.matchId, Is.EqualTo("match-1"));
            Assert.That(result.message, Is.EqualTo("scripted"));
            Assert.That((await service.PollAsync(ticket)).state, Is.EqualTo(state));
        }

        [Test]
        public async Task TicketsHaveDistinctIdsAndIndependentPollPositions()
        {
            var service = new LocalRankedQueueService();
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.Searching });
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.Found, matchId = "match-1" });
            string first = await service.EnqueueAsync();
            string second = await service.EnqueueAsync();

            Assert.That(second, Is.Not.EqualTo(first));
            await service.PollAsync(first);
            Assert.That((await service.PollAsync(first)).state, Is.EqualTo(RankedQueueState.Found));
            Assert.That((await service.PollAsync(second)).state, Is.EqualTo(RankedQueueState.Searching));
        }

        [Test]
        public async Task CancellationOverridesOnlyItsTicketAndCanBeRepeated()
        {
            var service = new LocalRankedQueueService();
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.Found, matchId = "match-1" });
            string first = await service.EnqueueAsync();
            string second = await service.EnqueueAsync();
            await service.CancelAsync(first);
            await service.CancelAsync(first);

            RankedQueueResult result = await service.PollAsync(first);
            Assert.That(result.state, Is.EqualTo(RankedQueueState.Cancelled));
            Assert.That(result.matchId, Is.Null);
            Assert.That((await service.PollAsync(second)).state, Is.EqualTo(RankedQueueState.Found));
        }

        [Test]
        public async Task RecordsCallsInOrderWithTheirTicketIds()
        {
            var service = new LocalRankedQueueService();
            string ticket = await service.EnqueueAsync();
            await service.PollAsync(ticket);
            await service.CancelAsync(ticket);

            Assert.That(service.Calls.Count, Is.EqualTo(3));
            Assert.That(service.Calls[0].Method, Is.EqualTo("EnqueueAsync"));
            Assert.That(service.Calls[1].Method, Is.EqualTo("PollAsync"));
            Assert.That(service.Calls[2].Method, Is.EqualTo("CancelAsync"));
            foreach (LocalRankedQueueService.Call call in service.Calls)
                Assert.That(call.TicketId, Is.EqualTo(ticket));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("unknown-ticket")]
        public void RejectsInvalidTicketsWithoutRecordingCalls(string ticketId)
        {
            var service = new LocalRankedQueueService();
            Assert.ThrowsAsync<ArgumentException>(() => service.PollAsync(ticketId));
            Assert.ThrowsAsync<ArgumentException>(() => service.CancelAsync(ticketId));
            Assert.That(service.Calls, Is.Empty);
        }

        [Test]
        public async Task ReplacingScriptOnlyAffectsNewTickets()
        {
            var service = new LocalRankedQueueService();
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.TimedOut });
            string first = await service.EnqueueAsync();
            service.PollResults.Clear();
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.Found, matchId = "match-2" });
            string second = await service.EnqueueAsync();

            Assert.That((await service.PollAsync(first)).state, Is.EqualTo(RankedQueueState.TimedOut));
            Assert.That((await service.PollAsync(second)).matchId, Is.EqualTo("match-2"));
        }
    }
}
