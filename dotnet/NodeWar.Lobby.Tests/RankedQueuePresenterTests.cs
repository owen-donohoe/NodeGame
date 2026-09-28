using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    /// <summary>Records every call the presenter makes so tests can assert the sequence.</summary>
    internal sealed class FakeRankedQueueView : IRankedQueueView
    {
        public int IdleCount;
        public int SearchingCount;
        public int LastElapsedSeconds = -1;
        public int FoundCount;
        public string LastMatchId;
        public int FailedCount;
        public string LastFailureMessage;
        public int CancelledCount;

        public event Action CancelRequested;

        public void ShowIdle() => IdleCount++;

        public void ShowSearching(int elapsedSeconds)
        {
            SearchingCount++;
            LastElapsedSeconds = elapsedSeconds;
        }

        public void ShowFound(string matchId)
        {
            FoundCount++;
            LastMatchId = matchId;
        }

        public void ShowFailed(string message)
        {
            FailedCount++;
            LastFailureMessage = message;
        }

        public void ShowCancelled() => CancelledCount++;

        public void RaiseCancel() => CancelRequested?.Invoke();
    }

    /// <summary>Throws from EnqueueAsync so failure-path tests don't need a scripted service.</summary>
    internal sealed class ThrowingEnqueueService : IRankedQueueService
    {
        public Task<string> EnqueueAsync() => throw new InvalidOperationException("enqueue exploded");
        public Task<RankedQueueResult> PollAsync(string ticketId) => throw new NotSupportedException();
        public Task CancelAsync(string ticketId) => throw new NotSupportedException();
    }

    /// <summary>Counts PollAsync calls so cadence tests can assert exactly how many happened.</summary>
    internal sealed class CountingRankedQueueService : IRankedQueueService
    {
        private readonly LocalRankedQueueService inner = new LocalRankedQueueService();
        public int PollCount;
        public System.Collections.Generic.List<RankedQueueResult> PollResults => inner.PollResults;

        public Task<string> EnqueueAsync() => inner.EnqueueAsync();

        public async Task<RankedQueueResult> PollAsync(string ticketId)
        {
            PollCount++;
            return await inner.PollAsync(ticketId);
        }

        public Task CancelAsync(string ticketId) => inner.CancelAsync(ticketId);
    }

    /// <summary>
    /// PollAsync does not complete until the test releases it, so overlap tests
    /// can observe the presenter's state while a poll is genuinely in flight.
    /// </summary>
    internal sealed class ManualRankedQueueService : IRankedQueueService
    {
        public int PollCount;
        private TaskCompletionSource<RankedQueueResult> pending;

        public Task<string> EnqueueAsync() => Task.FromResult("manual-ticket");

        public Task<RankedQueueResult> PollAsync(string ticketId)
        {
            PollCount++;
            pending = new TaskCompletionSource<RankedQueueResult>();
            return pending.Task;
        }

        public void CompletePending(RankedQueueResult result) => pending?.SetResult(result);

        public Task CancelAsync(string ticketId) => Task.CompletedTask;
    }

    public class RankedQueuePresenterTests
    {
        [Test]
        public async Task StartAsync_ShowsSearchingImmediately()
        {
            var service = new LocalRankedQueueService();
            var view = new FakeRankedQueueView();
            var presenter = new RankedQueuePresenter(service, view);

            await presenter.StartAsync(0);

            Assert.That(view.SearchingCount, Is.EqualTo(1));
            Assert.That(view.LastElapsedSeconds, Is.EqualTo(0));
            Assert.That(presenter.IsActive, Is.True);
        }

        [Test]
        public async Task Tick_DoesNotPollBeforeTwoSeconds()
        {
            var service = new CountingRankedQueueService();
            var view = new FakeRankedQueueView();
            var presenter = new RankedQueuePresenter(service, view);

            await presenter.StartAsync(0);
            presenter.Tick(0.5);
            presenter.Tick(1.9);

            Assert.That(service.PollCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Tick_PollsOnceCadenceElapses()
        {
            var service = new CountingRankedQueueService();
            var view = new FakeRankedQueueView();
            var presenter = new RankedQueuePresenter(service, view);

            await presenter.StartAsync(0);
            presenter.Tick(2.0);
            await Task.Yield();

            Assert.That(service.PollCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Tick_NeverOverlapsAnInFlightPoll()
        {
            var service = new ManualRankedQueueService();
            var view = new FakeRankedQueueView();
            var presenter = new RankedQueuePresenter(service, view);

            await presenter.StartAsync(0);
            presenter.Tick(2.0);
            await Task.Yield();
            Assert.That(service.PollCount, Is.EqualTo(1), "first poll should have started");

            // The poll is still pending. Further ticks past the cadence must
            // not start a second, overlapping poll.
            presenter.Tick(4.0);
            presenter.Tick(6.0);
            Assert.That(service.PollCount, Is.EqualTo(1));

            service.CompletePending(new RankedQueueResult { state = RankedQueueState.Searching });
            await Task.Yield();
            await Task.Yield();

            // Now that the first poll resolved, cadence can start a new one.
            presenter.Tick(8.0);
            await Task.Yield();
            Assert.That(service.PollCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Poll_Found_ShowsViewAndRaisesMatchFoundOnce()
        {
            var service = new LocalRankedQueueService();
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.Found, matchId = "match-1" });
            var view = new FakeRankedQueueView();
            var presenter = new RankedQueuePresenter(service, view);

            int matchFoundCount = 0;
            string matchFoundId = null;
            presenter.MatchFound += id => { matchFoundCount++; matchFoundId = id; };

            await presenter.StartAsync(0);
            presenter.Tick(2.0);
            await Task.Yield();
            await Task.Yield();

            Assert.That(view.FoundCount, Is.EqualTo(1));
            Assert.That(view.LastMatchId, Is.EqualTo("match-1"));
            Assert.That(matchFoundCount, Is.EqualTo(1));
            Assert.That(matchFoundId, Is.EqualTo("match-1"));
            Assert.That(presenter.IsActive, Is.False);

            // Stops polling after a terminal state.
            presenter.Tick(4.0);
            presenter.Tick(6.0);
            Assert.That(view.FoundCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Poll_Failed_ShowsFailed()
        {
            var service = new LocalRankedQueueService();
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.Failed, message = "no pool" });
            var view = new FakeRankedQueueView();
            var presenter = new RankedQueuePresenter(service, view);

            await presenter.StartAsync(0);
            presenter.Tick(2.0);
            await Task.Yield();
            await Task.Yield();

            Assert.That(view.FailedCount, Is.EqualTo(1));
            Assert.That(view.LastFailureMessage, Is.EqualTo("no pool"));
            Assert.That(presenter.IsActive, Is.False);
        }

        [Test]
        public async Task Poll_TimedOut_ShowsFailed()
        {
            var service = new LocalRankedQueueService();
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.TimedOut, message = "timed out" });
            var view = new FakeRankedQueueView();
            var presenter = new RankedQueuePresenter(service, view);

            await presenter.StartAsync(0);
            presenter.Tick(2.0);
            await Task.Yield();
            await Task.Yield();

            Assert.That(view.FailedCount, Is.EqualTo(1));
            Assert.That(view.LastFailureMessage, Is.EqualTo("timed out"));
        }

        [Test]
        public async Task Cancel_CallsCancelAsyncThenShowsCancelled()
        {
            var service = new LocalRankedQueueService();
            var view = new FakeRankedQueueView();
            var presenter = new RankedQueuePresenter(service, view);

            await presenter.StartAsync(0);
            view.RaiseCancel();
            await Task.Yield();
            await Task.Yield();

            Assert.That(service.Calls.Count, Is.EqualTo(2));
            Assert.That(service.Calls[0].Method, Is.EqualTo(nameof(LocalRankedQueueService.EnqueueAsync)));
            Assert.That(service.Calls[1].Method, Is.EqualTo(nameof(LocalRankedQueueService.CancelAsync)));
            Assert.That(view.CancelledCount, Is.EqualTo(1));
            Assert.That(presenter.IsActive, Is.False);

            // Stops polling once cancelled.
            presenter.Tick(10.0);
            Assert.That(view.CancelledCount, Is.EqualTo(1));
        }

        [Test]
        public async Task StartAsync_EnqueueThrows_ShowsFailed()
        {
            var service = new ThrowingEnqueueService();
            var view = new FakeRankedQueueView();
            var presenter = new RankedQueuePresenter(service, view);

            await presenter.StartAsync(0);

            Assert.That(view.FailedCount, Is.EqualTo(1));
            Assert.That(view.LastFailureMessage, Does.Contain("enqueue exploded"));
            Assert.That(presenter.IsActive, Is.False);
        }
    }
}
