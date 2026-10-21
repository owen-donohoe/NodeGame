using NodeWar.Lobby;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class WorkshopInventorySessionTests
    {
        private static WorkshopInventorySession Open(string baseId = "villager_guardian")
        {
            var session = new WorkshopInventorySession();
            session.BeginVisit();
            session.Select(baseId);
            return session;
        }

        [Test]
        public void CloseReopen_RejectsOldResponse()
        {
            var session = Open();
            Assert.That(session.TryBeginRequest(out var token), Is.True);
            session.Close();
            Assert.That(session.IsCurrent(token), Is.False);
            session.BeginVisit();
            session.Select("villager_guardian");
            Assert.That(session.IsCurrent(token), Is.False);
            Assert.That(session.IsVisitCurrent(token), Is.False);
        }

        [Test]
        public void AccountChange_RejectsPreviousAccountResponses()
        {
            var session = Open("a");
            Assert.That(session.TryBeginRequest(out var old), Is.True);
            int visit = session.InvalidateAccount();
            session.Select("a");
            Assert.That(session.IsOpen, Is.True);
            Assert.That(session.IsCurrent(old), Is.False);
            Assert.That(session.IsVisitCurrent(old), Is.False);
            Assert.That(session.IsVisitCurrent(visit), Is.True);
            session.EndRequest(old);
            Assert.That(session.TryBeginRequest(out var fresh), Is.True);
            Assert.That(session.IsCurrent(fresh), Is.True);
            var forged = new InventoryRequestToken(fresh.Visit, fresh.Selection, fresh.Request, fresh.BaseID, fresh.Account - 1);
            Assert.That(session.IsCurrent(forged), Is.False, "the account generation alone must invalidate");
            Assert.That(session.IsVisitCurrent(forged), Is.False);
        }

        [Test]
        public void SwitchItem_RejectsOldSelection()
        {
            var session = Open("a");
            session.TryBeginRequest(out var token);
            session.Select("b");
            Assert.That(session.IsCurrent(token), Is.False);
            Assert.That(session.IsVisitCurrent(token), Is.True);
            session.Select("a");
            Assert.That(session.IsCurrent(token), Is.False, "returning to the item is a new selection");
        }

        [Test]
        public void Busy_RejectsSecondRequest()
        {
            var session = Open();
            Assert.That(session.TryBeginRequest(out var first), Is.True);
            Assert.That(session.Busy, Is.True);
            Assert.That(session.TryBeginRequest(out _), Is.False);
            session.EndRequest(first);
            Assert.That(session.TryBeginRequest(out _), Is.True);
        }

        [Test]
        public void CompletionAfterClose_ReleasesGate()
        {
            var session = Open();
            session.TryBeginRequest(out var token);
            session.Close();
            Assert.That(session.Busy, Is.True);
            session.BeginVisit();
            Assert.That(session.TryBeginRequest(out _), Is.False, "reopened visit waits for the save");
            session.EndRequest(token);
            Assert.That(session.Busy, Is.False);
            Assert.That(session.TryBeginRequest(out _), Is.True);
        }

        [Test]
        public void MatchingToken_AcceptsResponse()
        {
            var session = Open();
            session.Select("villager_guardian");
            session.TryBeginRequest(out var token);
            Assert.That(session.IsCurrent(token), Is.True);
            Assert.That(token.BaseID, Is.EqualTo("villager_guardian"));
        }

        [Test]
        public void ClosedSession_RefusesRequests()
        {
            var session = new WorkshopInventorySession();
            Assert.That(session.TryBeginRequest(out _), Is.False);
            session.BeginVisit();
            session.Close();
            Assert.That(session.TryBeginRequest(out _), Is.False);
        }

        [Test]
        public void StaleEndRequest_DoesNotReleaseNewerRequest()
        {
            var session = Open();
            session.TryBeginRequest(out var first);
            session.EndRequest(first);
            session.TryBeginRequest(out var second);
            session.EndRequest(first);
            Assert.That(session.Busy, Is.True);
            session.EndRequest(second);
            Assert.That(session.Busy, Is.False);
        }

        [Test]
        public void ReselectingSameItem_KeepsTokenCurrent()
        {
            var session = Open("a");
            session.TryBeginRequest(out var token);
            session.Select("a");
            Assert.That(session.IsCurrent(token), Is.True);
        }
    }
}
