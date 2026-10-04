using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    public enum RankedQueueState { Searching, Found, Failed, TimedOut, Cancelled }

    public sealed class RankedQueueResult
    {
        public RankedQueueState state;
        public string matchId;
        public string message;
    }

    /// <summary>Queues one signed-in player. Calls may fail; finding a match does not start play.</summary>
    public interface IRankedQueueService
    {
        Task<string> EnqueueAsync();
        Task<RankedQueueResult> PollAsync(string ticketId);
        /// <summary>Deletes the ticket. The caller should stop polling after cancellation.</summary>
        Task CancelAsync(string ticketId);
    }

    /// <summary>SDK status names mapped without taking a dependency on Unity or Matchmaker.</summary>
    public static class RankedQueueStatus
    {
        public static RankedQueueResult Map(string status, string matchId = null, string message = null)
        {
            RankedQueueState state;
            switch (status)
            {
                case "InProgress": state = RankedQueueState.Searching; break;
                case "Found":
                    if (string.IsNullOrWhiteSpace(matchId))
                        return new RankedQueueResult
                        {
                            state = RankedQueueState.Failed,
                            message = "Found assignment has no match ID. " + message
                        };
                    state = RankedQueueState.Found;
                    break;
                case "Failed": state = RankedQueueState.Failed; break;
                case "Timeout": state = RankedQueueState.TimedOut; break;
                default:
                    return new RankedQueueResult
                    {
                        state = RankedQueueState.Failed,
                        message = "Unknown Matchmaker status: " + (status ?? "<null>") + ". " + message
                    };
            }
            return new RankedQueueResult
            {
                state = state,
                matchId = state == RankedQueueState.Found ? matchId : null,
                message = message
            };
        }
    }

    /// <summary>Offline queue with a scripted poll sequence per ticket and a record of valid calls.</summary>
    public sealed class LocalRankedQueueService : IRankedQueueService
    {
        public sealed class Call
        {
            public string Method;
            public string TicketId;
        }

        private sealed class Ticket
        {
            public int Next;
            public bool Cancelled;
            public RankedQueueResult[] Results;
        }

        private readonly List<Call> calls = new List<Call>();
        private readonly Dictionary<string, Ticket> tickets = new Dictionary<string, Ticket>();
        public IReadOnlyList<Call> Calls => calls;

        /// <summary>
        /// Copied when enqueuing. An empty script stays Searching; the last result repeats.
        /// Cancellation overrides the script for that ticket only.
        /// </summary>
        public List<RankedQueueResult> PollResults { get; } = new List<RankedQueueResult>();

        public Task<string> EnqueueAsync()
        {
            string ticketId = "local-ticket-" + (tickets.Count + 1);
            tickets.Add(ticketId, new Ticket { Results = PollResults.ToArray() });
            calls.Add(new Call { Method = nameof(EnqueueAsync), TicketId = ticketId });
            return Task.FromResult(ticketId);
        }

        public Task<RankedQueueResult> PollAsync(string ticketId)
        {
            Ticket ticket = GetTicket(ticketId);
            calls.Add(new Call { Method = nameof(PollAsync), TicketId = ticketId });
            if (ticket.Cancelled)
                return Task.FromResult(new RankedQueueResult { state = RankedQueueState.Cancelled, message = "local fake" });
            if (ticket.Results.Length == 0)
                return Task.FromResult(new RankedQueueResult { state = RankedQueueState.Searching, message = "local fake" });
            RankedQueueResult result = ticket.Results[ticket.Next];
            if (ticket.Next < ticket.Results.Length - 1) ticket.Next++;
            return Task.FromResult(result);
        }

        public Task CancelAsync(string ticketId)
        {
            Ticket ticket = GetTicket(ticketId);
            calls.Add(new Call { Method = nameof(CancelAsync), TicketId = ticketId });
            ticket.Cancelled = true;
            return Task.CompletedTask;
        }

        private Ticket GetTicket(string ticketId)
        {
            if (string.IsNullOrWhiteSpace(ticketId))
                throw new ArgumentException("ticketId must not be blank.", nameof(ticketId));
            if (!tickets.TryGetValue(ticketId, out Ticket ticket))
                throw new ArgumentException("Unknown local ticket: " + ticketId, nameof(ticketId));
            return ticket;
        }
    }
}
