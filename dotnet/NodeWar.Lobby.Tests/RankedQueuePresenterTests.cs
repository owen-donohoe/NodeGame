using System;
using System.Collections.Generic;
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
        public int BlockedCount;
        public int LastBlockedSeconds = -1;
        public int TotalCalls => IdleCount + SearchingCount + FoundCount + FailedCount + CancelledCount
            + ConnectingCount + RequeueingCount + ForfeitPromptCount + WaitingForResultCount + BotOfferCount
            + BlockedCount;

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

        public void ShowBlocked(int secondsLeft)
        {
            BlockedCount++;
            LastBlockedSeconds = secondsLeft;
        }

        public void RaiseCancel() => CancelRequested?.Invoke();
        public void RaiseForfeitConfirmed() => ForfeitConfirmed?.Invoke();
        public void RaiseBotAccepted() => BotAccepted?.Invoke();
    }

    /// <summary>Scripted stand-in for IPlayerStateService: one result or exception per call, repeating the last.</summary>
    internal sealed class FakePlayerStateService : IPlayerStateService
    {
        public System.Collections.Generic.List<PlayerState> Results { get; } = new System.Collections.Generic.List<PlayerState>();
        public Exception ThrowOnGet;
        public Task<PlayerState> PendingGet;
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
            if (PendingGet != null) return PendingGet;
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

    internal sealed class ScriptedRankedQueueService : IRankedQueueService
    {
        public Func<Task<string>> Enqueue = () => Task.FromResult("ticket-1");
        public Func<string, Task<RankedQueueResult>> Poll = _ => Task.FromResult(
            new RankedQueueResult { state = RankedQueueState.Searching });
        public Func<string, Task> Cancel = _ => Task.CompletedTask;
        public int EnqueueCount;
        public readonly List<string> PolledTickets = new List<string>();
        public readonly List<string> CancelledTickets = new List<string>();

        public Task<string> EnqueueAsync() { EnqueueCount++; return Enqueue(); }
        public Task<RankedQueueResult> PollAsync(string ticketId)
        {
            PolledTickets.Add(ticketId);
            return Poll(ticketId);
        }
        public Task CancelAsync(string ticketId)
        {
            CancelledTickets.Add(ticketId);
            return Cancel(ticketId);
        }
    }

    internal sealed class ScriptedRankedMatchService : IRankedMatchService
    {
        public Func<Task<RendezvousResult>> Roster = () => Task.FromResult(new RendezvousResult
        {
            state = MatchRecordState.Open, slot = 0, playerIds = new[] { "a", "b" }
        });
        public Func<Task<LeaveMatchResult>> Leave = () => Task.FromResult(
            new LeaveMatchResult { outcome = LeaveOutcome.Cleared });
        public readonly List<string> LeftMatches = new List<string>();
        public int RosterCalls;
        public Task<RendezvousResult> RendezvousAsync(string matchId, string joinCode)
        {
            RosterCalls++;
            return Roster();
        }
        public Task ConfirmConnectedAsync(string matchId) => Task.CompletedTask;
        public Task<LeaveMatchResult> LeaveAsync(string matchId, bool forfeit)
        {
            LeftMatches.Add(matchId);
            return Leave();
        }
        public Task<MatchResultView> GetResultAsync(string matchId) =>
            Task.FromResult(new MatchResultView { state = MatchRecordState.Pending });
        public Task<PresenceResult> PresenceAsync(string matchId, bool holding) =>
            Task.FromResult(new PresenceResult { state = MatchRecordState.Open, opponentSeenSecondsAgo = 0 });
        public Task<ResolveHoldResult> ResolveHoldAsync(string matchId) =>
            Task.FromResult(new ResolveHoldResult { outcome = HoldOutcome.OpponentPresent });
    }

    public class RankedQueuePresenterTests
    {
        private static RankedQueuePresenter MakePresenter(
            IRankedQueueService service,
            FakeRankedQueueView view,
            IRankedMatchService rankedMatch = null,
            IPlayerStateService playerState = null,
            Func<IRankedConnection> connectionFactory = null,
            Func<long> unixNow = null)
        {
            return new RankedQueuePresenter(
                service,
                view,
                rankedMatch ?? new LocalRankedMatchService(),
                playerState ?? new FakePlayerStateService(),
                connectionFactory ?? (() => new FakeRankedConnection()),
                unixNow);
        }

        private static PlayerState BlockedUntil(long unixSeconds, string activeMatchId = null) => new PlayerState
        {
            Discipline = new DisciplineRecord { Level = 3, BlockedUntilUnixSeconds = unixSeconds },
            ActiveMatch = activeMatchId == null ? null : new ActiveMatchRecord { matchId = activeMatchId }
        };

        [Test]
        public async Task Blocked_ShowsTheCountdown_AndNeverQueuesOrLeaves()
        {
            long clock = 1000;
            var service = new ScriptedRankedQueueService();
            var match = new LocalRankedMatchService();
            var states = new FakePlayerStateService();
            states.Results.Add(BlockedUntil(1120, activeMatchId: "held"));
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view, match, states, unixNow: () => clock);

            await presenter.StartAsync(0);

            Assert.That(view.BlockedCount, Is.EqualTo(1));
            Assert.That(view.LastBlockedSeconds, Is.EqualTo(120));
            Assert.That(service.EnqueueCount, Is.Zero);
            Assert.That(match.Calls, Is.Empty, "A held match is not resolved while blocked.");
            Assert.That(presenter.IsActive, Is.True);

            presenter.Tick(1);
            Assert.That(view.BlockedCount, Is.EqualTo(1), "Same second, no redraw.");
            clock = 1060;
            presenter.Tick(2);
            Assert.That(view.LastBlockedSeconds, Is.EqualTo(60));
        }

        [Test]
        public async Task Blocked_EndsInIdle_WhenTheBlockRunsOut()
        {
            long clock = 1000;
            var states = new FakePlayerStateService();
            states.Results.Add(BlockedUntil(1010));
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(new ScriptedRankedQueueService(), view, playerState: states, unixNow: () => clock);

            await presenter.StartAsync(0);
            clock = 1010;
            presenter.Tick(1);

            Assert.That(presenter.IsActive, Is.False);
            Assert.That(view.IdleCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ABlockInThePast_QueuesAsNormal()
        {
            var service = new ScriptedRankedQueueService();
            var states = new FakePlayerStateService();
            states.Results.Add(BlockedUntil(999));
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view, playerState: states, unixNow: () => 1000);

            await presenter.StartAsync(0);

            Assert.That(view.BlockedCount, Is.Zero);
            Assert.That(service.EnqueueCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Blocked_CanBeCancelled()
        {
            var states = new FakePlayerStateService();
            states.Results.Add(BlockedUntil(5000));
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(new ScriptedRankedQueueService(), view, playerState: states, unixNow: () => 1000);

            await presenter.StartAsync(0);
            await presenter.RequestCancelAsync();

            Assert.That(presenter.IsActive, Is.False);
            Assert.That(view.CancelledCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Cancel_PendingEnqueue_CleansUpLateTicket(bool cleanupThrows)
        {
            var pending = new TaskCompletionSource<string>();
            var service = new ScriptedRankedQueueService { Enqueue = () => pending.Task };
            if (cleanupThrows) service.Cancel = _ => throw new InvalidOperationException("offline");
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);
            Task start = presenter.StartAsync(0);

            await presenter.RequestCancelAsync();
            pending.SetResult("late-ticket");
            await start;
            presenter.Tick(10);

            Assert.That(service.CancelledTickets, Is.EqualTo(new[] { "late-ticket" }));
            Assert.That(service.PolledTickets, Is.Empty);
            Assert.That(presenter.IsActive, Is.False);
            Assert.That(view.CancelledCount, Is.EqualTo(1));
            Assert.That(view.FailedCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Cancel_PendingPreflight_DoesNotEnqueue(bool throws)
        {
            var pending = new TaskCompletionSource<PlayerState>();
            var service = new ScriptedRankedQueueService();
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view,
                playerState: new FakePlayerStateService { PendingGet = pending.Task });
            Task start = presenter.StartAsync(0);

            await presenter.RequestCancelAsync();
            if (throws) pending.SetException(new InvalidOperationException("offline"));
            else pending.SetResult(FakePlayerStateService.NoActiveMatch());
            await start;

            Assert.That(service.EnqueueCount, Is.Zero);
            Assert.That(view.CancelledCount, Is.EqualTo(1));
            Assert.That(view.FailedCount, Is.Zero);
            Assert.That(presenter.IsActive, Is.False);
        }

        [Test]
        public async Task Cancel_PendingLeave_DoesNotEnqueue()
        {
            var pending = new TaskCompletionSource<LeaveMatchResult>();
            var match = new ScriptedRankedMatchService { Leave = () => pending.Task };
            var state = new FakePlayerStateService();
            state.Results.Add(FakePlayerStateService.WithActiveMatch("old-match"));
            var service = new ScriptedRankedQueueService();
            var presenter = MakePresenter(service, new FakeRankedQueueView(), match, state);
            Task start = presenter.StartAsync(0);
            await presenter.RequestCancelAsync();
            pending.SetResult(new LeaveMatchResult { outcome = LeaveOutcome.Cleared });
            await start;
            Assert.That(service.EnqueueCount, Is.Zero);
            Assert.That(presenter.IsActive, Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        public async Task Cancel_PendingRoster_DoesNotStartConnection(int slot)
        {
            var pending = new TaskCompletionSource<RendezvousResult>();
            var match = new ScriptedRankedMatchService { Roster = () => pending.Task };
            var service = new ScriptedRankedQueueService
            {
                Poll = _ => Task.FromResult(new RankedQueueResult { state = RankedQueueState.Found, matchId = "match" })
            };
            var connection = new FakeRankedConnection();
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view, match, connectionFactory: () => connection);
            await presenter.StartAsync(0);
            presenter.Tick(2);
            await presenter.RequestCancelAsync();
            pending.SetResult(new RendezvousResult
            {
                state = MatchRecordState.Open, slot = slot, playerIds = new[] { "a", "b" }, joinCode = "CODE"
            });
            await Task.Yield();
            presenter.Tick(4);

            Assert.That(connection.HostCalls + connection.JoinCalls, Is.Zero);
            Assert.That(match.LeftMatches, Is.EqualTo(new[] { "match" }));
            Assert.That(view.CancelledCount, Is.EqualTo(1));
            Assert.That(presenter.IsActive, Is.False);
        }

        [Test]
        public async Task Cancel_ForfeitPromptViaMethod_CompletesStartWithoutForfeiting()
        {
            var match = new ScriptedRankedMatchService
            {
                Leave = () => Task.FromResult(new LeaveMatchResult { outcome = LeaveOutcome.NeedsForfeit })
            };
            var state = new FakePlayerStateService();
            state.Results.Add(FakePlayerStateService.WithActiveMatch("match"));
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(new ScriptedRankedQueueService(), view, match, state);
            Task start = presenter.StartAsync(0);
            await presenter.RequestCancelAsync();
            await start;
            view.RaiseForfeitConfirmed();
            Assert.That(match.LeftMatches.Count, Is.EqualTo(1));
            Assert.That(view.CancelledCount, Is.EqualTo(1));
            Assert.That(presenter.IsActive, Is.False);
        }

        [Test]
        public async Task Cancel_WaitingForResult_DoesNotRecheck()
        {
            var state = new FakePlayerStateService();
            state.Results.Add(FakePlayerStateService.WithActiveMatch("match"));
            var match = new LocalRankedMatchService
            {
                LeaveResult = new LeaveMatchResult { outcome = LeaveOutcome.Waiting, secondsLeft = 2 }
            };
            var presenter = MakePresenter(new ScriptedRankedQueueService(), new FakeRankedQueueView(), match, state);
            await presenter.StartAsync(0);
            await presenter.RequestCancelAsync();
            presenter.Tick(3);
            Assert.That(state.CallCount, Is.EqualTo(1));
            Assert.That(presenter.IsActive, Is.False);
        }

        [Test]
        public async Task Restart_OldPollCannotAffectNewAttemptOrReleaseItsPollGate()
        {
            var oldPoll = new TaskCompletionSource<RankedQueueResult>();
            var newPoll = new TaskCompletionSource<RankedQueueResult>();
            var service = new ScriptedRankedQueueService { Poll = _ => oldPoll.Task };
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);
            await presenter.StartAsync(0);
            presenter.Tick(2);
            service.Poll = _ => newPoll.Task;
            await presenter.StartAsync(3);
            presenter.Tick(5);
            oldPoll.SetResult(new RankedQueueResult { state = RankedQueueState.Found, matchId = "old-match" });
            await Task.Yield();
            presenter.Tick(7);

            Assert.That(service.PolledTickets.Count, Is.EqualTo(2));
            Assert.That(view.ConnectingCount, Is.Zero);
            newPoll.SetResult(new RankedQueueResult { state = RankedQueueState.Searching });
            await Task.Yield();
            presenter.Tick(9);
            Assert.That(service.PolledTickets.Count, Is.EqualTo(3));
        }

        [TestCase("preflight")]
        [TestCase("enqueue")]
        [TestCase("poll")]
        [TestCase("roster")]
        [TestCase("cancel")]
        [TestCase("forfeit")]
        public async Task Dispose_PendingAwait_MakesNoFurtherViewCalls(string pendingPhase)
        {
            var stateResult = new TaskCompletionSource<PlayerState>();
            var ticket = new TaskCompletionSource<string>();
            var poll = new TaskCompletionSource<RankedQueueResult>();
            var roster = new TaskCompletionSource<RendezvousResult>();
            var cancel = new TaskCompletionSource<bool>();
            var service = new ScriptedRankedQueueService();
            var state = new FakePlayerStateService();
            var match = new ScriptedRankedMatchService();
            if (pendingPhase == "preflight") state.PendingGet = stateResult.Task;
            if (pendingPhase == "enqueue") service.Enqueue = () => ticket.Task;
            if (pendingPhase == "poll") service.Poll = _ => poll.Task;
            if (pendingPhase == "cancel") service.Cancel = _ => cancel.Task;
            if (pendingPhase == "roster")
            {
                service.Poll = _ => Task.FromResult(new RankedQueueResult { state = RankedQueueState.Found, matchId = "match" });
                match.Roster = () => roster.Task;
            }
            if (pendingPhase == "forfeit")
            {
                state.Results.Add(FakePlayerStateService.WithActiveMatch("match"));
                match.Leave = () => Task.FromResult(new LeaveMatchResult { outcome = LeaveOutcome.NeedsForfeit });
            }
            var view = new FakeRankedQueueView();
            var connection = new FakeRankedConnection();
            var presenter = MakePresenter(service, view, match, state, () => connection);
            Task start = presenter.StartAsync(0);
            if (pendingPhase == "poll" || pendingPhase == "roster") presenter.Tick(2);
            Task cancellation = pendingPhase == "cancel" ? presenter.RequestCancelAsync() : Task.CompletedTask;
            int calls = view.TotalCalls;
            presenter.Dispose();

            stateResult.SetResult(FakePlayerStateService.NoActiveMatch());
            ticket.SetResult("late-ticket");
            if (pendingPhase == "poll") poll.SetException(new InvalidOperationException("offline"));
            roster.SetResult(new RendezvousResult { state = MatchRecordState.Open, slot = 0, playerIds = new[] { "a", "b" } });
            cancel.SetResult(true);
            await start;
            await cancellation;
            await Task.Yield();
            view.RaiseForfeitConfirmed();
            presenter.Tick(100);
            await presenter.StartAsync(100);

            Assert.That(view.TotalCalls, Is.EqualTo(calls));
            Assert.That(connection.HostCalls + connection.JoinCalls, Is.Zero);
            Assert.That(presenter.IsActive, Is.False);
            if (pendingPhase == "enqueue") Assert.That(service.CancelledTickets, Does.Contain("late-ticket"));
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
        public async Task Poll_TwoExceptionsThenSearching_KeepsTicketAndResetsCounter()
        {
            int polls = 0;
            var service = new ScriptedRankedQueueService
            {
                Poll = _ => ++polls % 3 == 0
                    ? Task.FromResult(new RankedQueueResult { state = RankedQueueState.Searching })
                    : Task.FromException<RankedQueueResult>(new InvalidOperationException("offline"))
            };
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);
            await presenter.StartAsync(0);
            for (int i = 1; i <= 6; i++)
            {
                presenter.Tick(i * 2 - 0.1);
                Assert.That(polls, Is.EqualTo(i - 1), "retries retain the 2 second cadence");
                presenter.Tick(i * 2);
                Assert.That(presenter.IsActive, Is.True);
            }

            Assert.That(service.PolledTickets, Is.EqualTo(new[]
                { "ticket-1", "ticket-1", "ticket-1", "ticket-1", "ticket-1", "ticket-1" }));
            Assert.That(service.EnqueueCount, Is.EqualTo(1));
            Assert.That(service.CancelledTickets, Is.Empty);
            Assert.That(view.FailedCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Poll_ThreeExceptions_CancelsBeforeFailureWithoutAwaitingCleanup(bool cleanupThrows)
        {
            var cleanup = new TaskCompletionSource<bool>();
            var view = new FakeRankedQueueView();
            int failuresAtCancel = -1;
            var service = new ScriptedRankedQueueService
            {
                Poll = _ => throw new InvalidOperationException("offline"),
                Cancel = _ =>
                {
                    failuresAtCancel = view.FailedCount;
                    if (cleanupThrows) throw new InvalidOperationException("delete failed");
                    return cleanup.Task;
                }
            };
            var presenter = MakePresenter(service, view);
            await presenter.StartAsync(0);
            presenter.Tick(2);
            presenter.Tick(4);
            Assert.That(view.FailedCount, Is.Zero);
            presenter.Tick(6);

            Assert.That(service.CancelledTickets, Is.EqualTo(new[] { "ticket-1" }));
            Assert.That(failuresAtCancel, Is.Zero);
            Assert.That(view.FailedCount, Is.EqualTo(1));
            Assert.That(view.LastFailureMessage, Is.EqualTo("offline"));
            Assert.That(presenter.IsActive, Is.False);
            if (!cleanupThrows) cleanup.SetException(new InvalidOperationException("late delete failure"));
            await Task.Yield();
            presenter.Tick(8);
            Assert.That(service.PolledTickets.Count, Is.EqualTo(3));
            Assert.That(view.FailedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Poll_StaleException_DoesNotCountAgainstNewAttempt()
        {
            var oldPoll = new TaskCompletionSource<RankedQueueResult>();
            var service = new ScriptedRankedQueueService { Poll = _ => oldPoll.Task };
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);
            await presenter.StartAsync(0);
            presenter.Tick(2);
            await presenter.RequestCancelAsync();
            service.Enqueue = () => Task.FromResult("ticket-2");
            service.Poll = _ => throw new InvalidOperationException("offline");
            await presenter.StartAsync(3);
            oldPoll.SetException(new InvalidOperationException("old failure"));
            await Task.Yield();
            presenter.Tick(5);
            presenter.Tick(7);
            Assert.That(presenter.IsActive, Is.True);
            Assert.That(view.FailedCount, Is.Zero);
            presenter.Tick(9);
            Assert.That(service.CancelledTickets, Is.EqualTo(new[] { "ticket-1", "ticket-2" }));
            Assert.That(view.FailedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Poll_NewAttempt_ResetsExceptionCounter()
        {
            var service = new ScriptedRankedQueueService
            {
                Poll = _ => throw new InvalidOperationException("offline")
            };
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);
            await presenter.StartAsync(0);
            presenter.Tick(2);
            presenter.Tick(4);
            await presenter.RequestCancelAsync();
            await presenter.StartAsync(5);
            presenter.Tick(7);
            presenter.Tick(9);
            Assert.That(presenter.IsActive, Is.True);
            Assert.That(view.FailedCount, Is.Zero);
            presenter.Tick(11);
            Assert.That(view.FailedCount, Is.EqualTo(1));
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

        [TestCase(false)]
        [TestCase(true)]
        public async Task BotAccept_CancelFailsWithoutFound_DoesNotLaunchBot(bool pollThrows)
        {
            var service = new ScriptedRankedQueueService
            {
                Cancel = _ => throw new InvalidOperationException("cannot delete")
            };
            if (pollThrows) service.Poll = _ => throw new InvalidOperationException("offline");
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);
            int bots = 0;
            presenter.BotMatchAccepted += () => bots++;
            await presenter.StartAsync(0);
            view.RaiseBotAccepted();

            Assert.That(bots, Is.Zero);
            Assert.That(service.PolledTickets, Is.EqualTo(new[] { "ticket-1" }));
            Assert.That(view.LastFailureMessage, Is.EqualTo("Couldn't leave the ranked queue. Try again."));
            Assert.That(view.FailedCount, Is.EqualTo(1));
            Assert.That(presenter.IsActive, Is.False);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task BotAccept_RacesFound_ExactlyOneLaunch(bool botFirst, bool cancelFails)
        {
            var oldPoll = new TaskCompletionSource<RankedQueueResult>();
            var cancellation = new TaskCompletionSource<bool>();
            var found = new RankedQueueResult { state = RankedQueueState.Found, matchId = "match" };
            var service = new ScriptedRankedQueueService
            {
                Poll = _ => oldPoll.Task,
                Cancel = _ => cancellation.Task
            };
            var match = new ScriptedRankedMatchService();
            var connection = new FakeRankedConnection();
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view, match, connectionFactory: () => connection);
            int bots = 0;
            presenter.BotMatchAccepted += () => bots++;
            await presenter.StartAsync(0);
            presenter.Tick(90);

            if (botFirst) view.RaiseBotAccepted();
            oldPoll.SetResult(found);
            await Task.Yield();
            if (botFirst)
            {
                Assert.That(match.RosterCalls, Is.Zero, "Found from the old poll is ignored during bot cancellation");
                Assert.That(bots, Is.Zero, "bot must wait for successful cancellation");
            }
            view.RaiseBotAccepted(); // A second click cannot start another cancellation.
            service.Poll = _ => Task.FromResult(found);
            if (botFirst && cancelFails) cancellation.SetException(new InvalidOperationException("allocated"));
            else cancellation.SetResult(true);
            await Task.Yield();

            Assert.That(bots + connection.HostCalls, Is.EqualTo(1));
            Assert.That(bots, Is.EqualTo(botFirst && !cancelFails ? 1 : 0));
            Assert.That(match.RosterCalls, Is.EqualTo(bots == 1 ? 0 : 1));
            Assert.That(service.CancelledTickets.Count, Is.EqualTo(botFirst ? 1 : 0));
            Assert.That(view.FailedCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task BotAccept_LateFoundAfterCancellationFinishes_IsIgnored(bool cancelFails)
        {
            var oldPoll = new TaskCompletionSource<RankedQueueResult>();
            var service = new ScriptedRankedQueueService { Poll = _ => oldPoll.Task };
            var match = new ScriptedRankedMatchService();
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view, match);
            int bots = 0;
            presenter.BotMatchAccepted += () => bots++;
            await presenter.StartAsync(0);
            presenter.Tick(90);
            if (cancelFails) service.Cancel = _ => throw new InvalidOperationException("offline");
            service.Poll = _ => Task.FromResult(new RankedQueueResult { state = RankedQueueState.Searching });
            view.RaiseBotAccepted();
            oldPoll.SetResult(new RankedQueueResult { state = RankedQueueState.Found, matchId = "late-match" });
            await Task.Yield();

            Assert.That(match.RosterCalls, Is.Zero);
            Assert.That(bots, Is.EqualTo(cancelFails ? 0 : 1));
            Assert.That(view.FailedCount, Is.EqualTo(cancelFails ? 1 : 0));
            Assert.That(presenter.IsActive, Is.False);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task BotAccept_CancelOrDisposeDuringAwait_DoesNotLaunch(bool dispose, bool recoveringFound)
        {
            var cancellation = new TaskCompletionSource<bool>();
            var recovery = new TaskCompletionSource<RankedQueueResult>();
            var service = new ScriptedRankedQueueService
            {
                Cancel = _ => cancellation.Task,
                Poll = _ => recovery.Task
            };
            var match = new ScriptedRankedMatchService();
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view, match);
            int bots = 0;
            presenter.BotMatchAccepted += () => bots++;
            await presenter.StartAsync(0);
            view.RaiseBotAccepted();
            if (recoveringFound)
            {
                cancellation.SetException(new InvalidOperationException("allocated"));
                await Task.Yield();
            }
            if (dispose) presenter.Dispose();
            else await presenter.RequestCancelAsync();
            int calls = view.TotalCalls;
            if (!recoveringFound) cancellation.SetResult(true);
            recovery.SetResult(new RankedQueueResult { state = RankedQueueState.Found, matchId = "match" });
            await Task.Yield();

            Assert.That(view.TotalCalls, Is.EqualTo(calls));
            Assert.That(match.RosterCalls, Is.Zero);
            Assert.That(bots, Is.Zero);
            Assert.That(presenter.IsActive, Is.False);
        }

        [Test]
        public async Task BotOffer_PendingEnqueue_WaitsForKnownTicket()
        {
            var pending = new TaskCompletionSource<string>();
            var service = new ScriptedRankedQueueService { Enqueue = () => pending.Task };
            var view = new FakeRankedQueueView();
            var presenter = MakePresenter(service, view);
            int bots = 0;
            presenter.BotMatchAccepted += () => bots++;
            Task start = presenter.StartAsync(0);
            presenter.Tick(90);
            view.RaiseBotAccepted();
            Assert.That(bots, Is.Zero);
            Assert.That(view.BotOfferCount, Is.Zero);
            pending.SetResult("late-ticket");
            await start;
            presenter.Tick(91);
            Assert.That(view.BotOfferCount, Is.EqualTo(1));
            view.RaiseBotAccepted();
            Assert.That(bots, Is.EqualTo(1));
            Assert.That(service.CancelledTickets, Is.EqualTo(new[] { "late-ticket" }));
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
