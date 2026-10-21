using System;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Pins one inventory request to the page visit, the inspected base item and a single
    /// request serial. A response is applied only while the token is still current.
    /// </summary>
    public readonly struct InventoryRequestToken
    {
        public int Visit { get; }
        public int Selection { get; }
        public int Request { get; }
        public int Account { get; }
        public string BaseID { get; }

        public InventoryRequestToken(int visit, int selection, int request, string baseId, int account = 0)
        {
            Visit = visit; Selection = selection; Request = request; BaseID = baseId; Account = account;
        }
    }

    /// <summary>
    /// Pure lifetime model for the Workshop's asynchronous inventory traffic: one request at
    /// a time, stale responses recognisable, and the busy gate always releasable. No Task or
    /// UI types, so it is testable in the dotnet projects.
    /// </summary>
    public sealed class WorkshopInventorySession
    {
        private int visit, selection, request, activeRequest, account;
        private bool open;
        private string baseId;

        public bool IsOpen => open;
        public bool Busy => activeRequest != 0;
        public string SelectedBaseID => baseId;
        public NodeWar.Backend.PlayerState State { get; private set; }
        public int RetrySeconds { get; private set; }
        private bool failureShown;

        public bool AcceptState(NodeWar.Backend.PlayerState state)
        {
            if (!NodeWar.Backend.RememberGuard.HasInventoryState(state)) return false;
            State = state; RetrySeconds = 0; failureShown = false;
            return true;
        }

        // One toast per outage; retries back off to thirty seconds.
        public bool Failed()
        {
            RetrySeconds = RetrySeconds == 0 ? 2 : Math.Min(30, RetrySeconds * 2);
            bool notify = !failureShown; failureShown = true;
            return notify;
        }

        /// <summary>Opens a new visit; everything issued under an earlier visit becomes stale.</summary>
        public int BeginVisit()
        {
            State = null; RetrySeconds = 0; failureShown = false;
            open = true;
            visit++;
            selection++;
            baseId = null;
            return visit;
        }

        /// <summary>Moves to a new account generation: every earlier token and visit goes stale. Returns the new visit.</summary>
        public int InvalidateAccount()
        {
            State = null; RetrySeconds = 0; failureShown = false;
            account++;
            selection++;
            baseId = null;
            return ++visit;
        }

        /// <summary>Records the inspected base item. Re-selecting the same item keeps requests current.</summary>
        public int Select(string id)
        {
            if (!string.Equals(baseId, id, StringComparison.Ordinal))
            {
                baseId = id;
                selection++;
            }
            return selection;
        }

        /// <summary>Takes the single busy gate. Refused while closed or while another request is in flight.</summary>
        public bool TryBeginRequest(out InventoryRequestToken token)
        {
            if (!open || activeRequest != 0)
            {
                token = default;
                return false;
            }
            activeRequest = ++request;
            token = new InventoryRequestToken(visit, selection, activeRequest, baseId, account);
            return true;
        }

        /// <summary>True while the visit is open and the same item is still inspected.</summary>
        public bool IsCurrent(InventoryRequestToken token) =>
            open && token.Account == account && token.Visit == visit && token.Selection == selection &&
            string.Equals(token.BaseID, baseId, StringComparison.Ordinal);

        /// <summary>True while the visit is open, regardless of which item is inspected.</summary>
        public bool IsVisitCurrent(InventoryRequestToken token) => open && token.Account == account && token.Visit == visit;

        public bool IsVisitCurrent(int visitId) => open && visitId == visit;

        /// <summary>Releases the gate even when the visit closed or the selection moved on.</summary>
        public void EndRequest(InventoryRequestToken token)
        {
            if (activeRequest != 0 && activeRequest == token.Request) activeRequest = 0;
        }

        /// <summary>Ends the visit. An in-flight request keeps the gate until its EndRequest.</summary>
        public void Close()
        {
            open = false;
            visit++;
        }
    }
}
