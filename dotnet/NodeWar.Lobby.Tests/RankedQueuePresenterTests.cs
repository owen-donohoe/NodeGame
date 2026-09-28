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
        public int ConnectingCount;
        public int RequeueingCount;
        public string LastRequeueingMessage;
        public int ForfeitPromptCount;
        public int WaitingForResultCount;
        public int LastWaitingSeconds = -1;
        public int BotOfferCount;

        public event Action CancelRequested;
        public event Action ForfeitConfirmed;
        public event Action BotAccepted;

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

        public void ShowConnecting() => ConnectingCount++;

        public void ShowRequeueing(string message)
        {
            RequeueingCount++;
            LastRequeueingMessage = message;
        }

        public void ShowForfeitPrompt() => ForfeitPromptCount++;

        public void ShowWaitingForResult(int seconds)
        {
            WaitingForResultCount++;
            LastWaitingSeconds = seconds;
        }

        public void ShowBotOffer() => BotOfferCount++;

        public void RaiseCancel() => CancelRequested?.Invoke();
        public void RaiseForfeitConfirmed() => ForfeitConfirmed?.Invoke();
        public void RaiseBotAccepted() => BotAccepted?.Invoke();
    }

    /// <summary>Scripted stand-in for IPlayerStateService: one result or exception per call, repeating the last.</summary>
    internal sealed class FakePlayerStateService : IPlayerStateService
    {
        public System.Collections.Generic.List<PlayerState> Results { get; } = new System.Collections.Generic.List<PlayerState>();
        public Exception ThrowOnGet;
        public int CallCount;
        private int next;

        public static PlayerState NoActiveMatch() => new PlayerState();

        public static PlayerState WithActiveMatch(string matchId) => new PlayerState
        {
            ActiveMatch = new ActiveMatchRecord { matchId = matchId, expiresUnixSeconds = 0 }
        };

        public Task<PlayerState> GetAsync()
        {
            CallCount++;
            if (ThrowOnGet != null) throw ThrowOnGet;
            if (Results.Count == 0) return Task.FromResult(NoActiveMatch());
            PlayerState result = Results[next];
            if (next < Results.Count - 1) next++;
            return Task.FromResult(result);
        }
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
        private static RankedQueuePresenter MakePresenter(
            IRankedQueueService service,
            FakeRankedQueueView view,
            IRankedMatchService rankedMatch = null,
            IPlayerStateService playerState = null,
            Func<IRankedConnection> connectionFactory = null)
        {
            return new RankedQueuePresenter(
                service,
                view,
                rankedMatch ?? new LocalRankedMatchService(),
                playerState ?? new FakePlayerStateService(),
                connectionFactory ?? (() => new FakeRankedConnection()));
        }

        [Test]
        public async Task StartAsync_NoActiveMatch_Queues()
        {
            var service = new LocalRankedQueueService();
            var view = new FakeRankedQueueView();
            var playerState = new FakePlayerStateService();
            var presenter = MakePresenter(service, view, playerState: playerState);

            await presenter.StartAsync(0);

            Assert.That(playerState.CallCount, Is.EqualTo(1));
            Assert.That(view.SearchingCount, Is.EqualTo(1));
            Assert.That(view.LastElapsedSeconds, Is.EqualTo(0));
            Assert.That(presenter.IsActive, Is.True);
        }

        [Test]
        public async Task StartAsync_ActiveMatchCleared_Queues()
        {
            var service = new LocalRankedQueueService();
            var view = new FakeRankedQueueView();
            var playerState = new FakePlayerStateService();
            playerState.Results.Add(FakePlayerStateService.WithActiveMatch("stale-match"));
            var rankedMatch = new LocalRankedMatchService { LeaveResult = new LeaveMatchResult { outcome = LeaveOutcome.Cleared } };
            var presenter = MakePresenter(service, view, rankedMatch: rankedMatch, playerState: playerState);

            await presenter.StartAsync(0);

            Assert.That(rankedMatch.Calls.Count, Is.EqualTo(1));
            Assert.That(rankedMatch.Calls[0].Method, Is.EqualTo(nameof(LocalRankedMatchService.LeaveAsync)));
            Assert.That(rankedMatch.Calls[0].Forfeit, Is.False);
            Assert.That(view.SearchingCount, Is.EqualTo(1));
        }

        [Test]
        public async Task StartAsync_ActiveMatchNeedsForfeit_ConfirmThenQueues()
        {
            var service = new LocalRankedQueueService();
            var view = new FakeRankedQueueView();
            var playerState = new FakePlayerStateService();
            playerState.Results.Add(FakePlayerStateService.WithActiveMatch("played-match"));
            var rankedMatch = new LocalRankedMatchService { LeaveResult = new LeaveMatchResult { outcome = LeaveOutcome.NeedsForfeit } };
            var presenter = MakePresenter(service, view, rankedMatch: rankedMatch, playerState: playerState);

            // StartAsync's task does not complete while it is waiting on the
            // player's forfeit decision, so it must not be awaited here.
            _ = presenter.StartAsync(0);
            await Task.Yield();
            await Task.Yield();

            Assert.That(view.ForfeitPromptCount, Is.EqualTo(1));
            Assert.That(view.SearchingCount, Is.EqualTo(0));

            // Confirming re-calls Leave with forfeit=true; script it to clear next.
            rankedMatch.LeaveResult = new LeaveMatchResult { outcome = LeaveOutcome.Cleared };
            view.RaiseForfeitConfirmed();
            await Task.Yield();
            await Task.Yield();

            Assert.That(rankedMatch.Calls.Count, Is.EqualTo(2));
            Assert.That(rankedMatch.Calls[0].Forfeit, Is.False);
            Assert.That(rankedMatch.Calls[1].Forfeit, Is.True);
            Assert.That(view.SearchingCount, Is.EqualTo(1));
        }

        [Test]
        public async Task StartAsync_ActiveMatchNeedsForfeit_CancelGoesIdle()
        {
            var service = new LocalRankedQueueService();
            var view = new FakeRankedQueueView();
            var playerState = new FakePlayerStateService();
            playerState.Results.Add(FakePlayerStateService.WithActiveMatch("played-match"));
            var rankedMatch = new LocalRankedMatchService { LeaveResult = new LeaveMatchResult { outcome = LeaveOutcome.NeedsForfeit } };
            var presenter = MakePresenter(service, view, rankedMatch: rankedMatch, playerState: playerState);

            _ = presenter.StartAsync(0);
            await Task.Yield();
            await Task.Yield();
            Assert.That(view.ForfeitPromptCount, Is.EqualTo(1));

            view.RaiseCancel();
            await Task.Yield();
            await Task.Yield();

            Assert.That(view.CancelledCount, Is.EqualTo(1));
            Assert.That(presenter.IsActive, Is.False);
            // Only the original check happened; declining never forfeits.
            Assert.That(rankedMatch.Calls.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task StartAsync_ActiveMatchWaiting_CountsDownThenRechecks()
        {
            var service = new LocalRankedQueueService();
            var view = new FakeRankedQueueView();
            var playerState = new FakePlayerStateService();
            playerState.Results.Add(FakePlayerStateService.WithActiveMatch("reported-match"));
            playerState.Results.Add(FakePlayerStateService.NoActiveMatch()); // after the wait, preflight sees it's clear
            var rankedMatch = new LocalRankedMatchService { LeaveResult = new LeaveMatchResult { outcome = LeaveOutcome.Waiting, secondsLeft = 3 } };
            var presenter = MakePresenter(service, view, rankedMatch: rankedMatch, playerState: playerState);

            await presenter.StartAsync(0);
            Assert.That(view.WaitingForResultCount, Is.EqualTo(1));
            Assert.That(view.LastWaitingSeconds, Is.EqualTo(3));

            presenter.Tick(1);
            Assert.That(view.LastWaitingSeconds, Is.EqualTo(2));

            presenter.Tick(3);
            await Task.Yield();
            await Task.Yield();

            // The countdown hit zero and re-ran preflight, which this time found no active match.
            Assert.That(playerState.CallCount, Is.EqualTo(2));
            Assert.That(view.SearchingCount, Is.EqualTo(1));
        }

        [Test]
        public async Task StartAsync_PreflightThrows_ShowsFailed()
        {
            var service = new LocalRankedQueueService();
            var view = new FakeRankedQueueView();
            var playerState = new FakePlayerStateService { ThrowOnGet = new InvalidOperationException("offline") };
            var presenter = MakePresenter(service, view, playerState: playerState);

            await presenter.StartAsync(0);

            Assert.That(view.FailedCount, Is.EqualTo(1));
            Assert.That(view.LastFailureMessage, Does.Contain("offline"));
            Assert.That(presenter.IsActive, Is.False);
        }

        [Test]
        public async Task Tick_DoesNotPollBeforeTwoSeconds()
        {
            var service = new CountingRankedQueueService();
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);

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
            var presenter = MakePresenter(service, view);

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
            var presenter = MakePresenter(service, view);

            await presenter.StartAsync(0);
            presenter.Tick(2.0);
            await Task.Yield();
            Assert.That(service.PollCount, Is.EqualTo(1), "first poll should have started");

            presenter.Tick(4.0);
            presenter.Tick(6.0);
            Assert.That(service.PollCount, Is.EqualTo(1));

            service.CompletePending(new RankedQueueResult { state = RankedQueueState.Searching });
            await Task.Yield();
            await Task.Yield();

            presenter.Tick(8.0);
            await Task.Yield();
            Assert.That(service.PollCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Poll_Found_ConnectsThenRaisesMatchFoundOnce()
        {
            var service = new LocalRankedQueueService();
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.Found, matchId = "match-1" });
            var view = new FakeRankedQueueView();
            var rankedMatch = new LocalRankedMatchService();
            rankedMatch.RendezvousResults.Add(new RendezvousResult
            {
                state = MatchRecordState.Open,
                playerIds = new[] { "a", "b" },
                slot = 0
            });
            var connection = new FakeRankedConnection();
            var presenter = MakePresenter(service, view, rankedMatch: rankedMatch, connectionFactory: () => connection);

            int matchFoundCount = 0;
            string matchFoundId = null;
            presenter.MatchFound += id => { matchFoundCount++; matchFoundId = id; };

            await presenter.StartAsync(0);
            presenter.Tick(2.0);
            await Task.Yield();
            await Task.Yield();

            Assert.That(view.ConnectingCount, Is.EqualTo(1));
            Assert.That(connection.HostCalls, Is.EqualTo(1));

            connection.JoinCode = "CODE";
            presenter.Tick(2.1);

            connection.Phase = RankedConnectionPhase.Connected;
            presenter.Tick(2.2);

            Assert.That(matchFoundCount, Is.EqualTo(1));
            Assert.That(matchFoundId, Is.EqualTo("match-1"));
            Assert.That(presenter.IsActive, Is.False);

            presenter.Tick(4.0);
            Assert.That(matchFoundCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Poll_TimedOut_ShowsFailed()
        {
            var service = new LocalRankedQueueService();
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.TimedOut, message = "timed out" });
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);

            await presenter.StartAsync(0);
            presenter.Tick(2.0);
            await Task.Yield();
            await Task.Yield();

            Assert.That(view.FailedCount, Is.EqualTo(1));
            Assert.That(view.LastFailureMessage, Is.EqualTo("timed out"));
        }

        [Test]
        public async Task Poll_Failed_RequeuesThenStopsOnFourthConsecutiveFailure()
        {
            var service = new LocalRankedQueueService();
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.Failed, message = "no pool" });
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);

            await presenter.StartAsync(0);

            // Failures 1-3: each re-queues (shows requeueing, goes back to searching).
            // Each requeue resets the poll cadence clock to the tick time it
            // happened on, so ticks must stay 2s apart from the previous one.
            double t = 0;
            for (int i = 0; i < 3; i++)
            {
                t += 2;
                presenter.Tick(t);
                await Task.Yield();
                await Task.Yield();
                await Task.Yield();
            }

            Assert.That(view.RequeueingCount, Is.EqualTo(3));
            Assert.That(view.FailedCount, Is.EqualTo(0));
            Assert.That(presenter.IsActive, Is.True);

            // 4th consecutive failure stops.
            t += 2;
            presenter.Tick(t);
            await Task.Yield();
            await Task.Yield();
            await Task.Yield();

            Assert.That(view.FailedCount, Is.EqualTo(1));
            Assert.That(view.LastFailureMessage, Is.EqualTo("no pool"));
            Assert.That(presenter.IsActive, Is.False);
        }

        [Test]
        public async Task RendezvousFailure_Requeues()
        {
            var service = new LocalRankedQueueService();
            service.PollResults.Add(new RankedQueueResult { state = RankedQueueState.Found, matchId = "match-1" });
            var view = new FakeRankedQueueView();
            var rankedMatch = new LocalRankedMatchService();
            rankedMatch.RendezvousResults.Add(new RendezvousResult
            {
                state = MatchRecordState.Open,
                playerIds = new[] { "a", "b" },
                slot = 0
            });
            var connection = new FakeRankedConnection { FailureMessage = "relay dead" };
            var presenter = MakePresenter(service, view, rankedMatch: rankedMatch, connectionFactory: () => connection);

            await presenter.StartAsync(0);
            presenter.Tick(2.0);
            await Task.Yield();
            await Task.Yield();
            Assert.That(view.ConnectingCount, Is.EqualTo(1));

            connection.Phase = RankedConnectionPhase.Failed;
            presenter.Tick(2.1);
            await Task.Yield();
            await Task.Yield();
            await Task.Yield();

            Assert.That(view.RequeueingCount, Is.EqualTo(1));
            Assert.That(view.LastRequeueingMessage, Does.Contain("connection failed"));
            Assert.That(presenter.IsActive, Is.True);
        }

        [Test]
        public async Task BotOffer_ShownOnceAtNinetySeconds_AcceptCancelsTicketAndRaisesEvent()
        {
            var service = new LocalRankedQueueService();
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);

            bool botAccepted = false;
            presenter.BotMatchAccepted += () => botAccepted = true;

            await presenter.StartAsync(0);
            presenter.Tick(89);
            Assert.That(view.BotOfferCount, Is.EqualTo(0));

            presenter.Tick(90);
            Assert.That(view.BotOfferCount, Is.EqualTo(1));

            presenter.Tick(92);
            Assert.That(view.BotOfferCount, Is.EqualTo(1), "shown only once");

            view.RaiseBotAccepted();
            await Task.Yield();
            await Task.Yield();

            Assert.That(botAccepted, Is.True);
            Assert.That(presenter.IsActive, Is.False);
        }

        [Test]
        public async Task Cancel_CallsCancelAsyncThenShowsCancelled()
        {
            var service = new LocalRankedQueueService();
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);

            await presenter.StartAsync(0);
            view.RaiseCancel();
            await Task.Yield();
            await Task.Yield();

            Assert.That(view.CancelledCount, Is.EqualTo(1));
            Assert.That(presenter.IsActive, Is.False);

            presenter.Tick(10.0);
            Assert.That(view.CancelledCount, Is.EqualTo(1));
        }

        [Test]
        public async Task StartAsync_EnqueueThrows_ShowsFailed()
        {
            var service = new ThrowingEnqueueService();
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);

            await presenter.StartAsync(0);

            Assert.That(view.FailedCount, Is.EqualTo(1));
            Assert.That(view.LastFailureMessage, Does.Contain("enqueue exploded"));
            Assert.That(presenter.IsActive, Is.False);
        }
    }
}
