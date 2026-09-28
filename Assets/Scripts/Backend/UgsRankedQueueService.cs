using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Network;
using Unity.Services.Authentication;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;

namespace NodeWar.Backend
{
    /// <summary>Client ticketing for the ranked queue; ratings and arenas come from Cloud Save.</summary>
    public sealed class UgsRankedQueueService : IRankedQueueService
    {
        public async Task<string> EnqueueAsync()
        {
            await GameServices.EnsureReadyAsync();
            // Matchmaker's Cloud Save rules reject new players until these records exist.
            try
            {
                if (await BackendServices.PlayerState.GetAsync() == null)
                    throw new InvalidOperationException("GetPlayerState returned no player state.");
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Cannot join the ranked queue: player records could not be prepared. Please try again.", exception);
            }

            var build = LocalBuildIdentity.Current;
            var players = new List<Player>
            {
                new Player(AuthenticationService.Instance.PlayerId, new Dictionary<string, object>
                {
                    { "protocol", build.protocol },
                    { "sim", build.sim },
                    { "content", build.content }
                })
            };
            IMatchmakerService matchmaker = MatchmakerService.Instance;
            CreateTicketResponse response = await matchmaker.CreateTicketAsync(players, new CreateTicketOptions("ranked"));
            if (string.IsNullOrWhiteSpace(response?.Id))
                throw new InvalidOperationException("Matchmaker returned no ranked ticket ID.");
            return response.Id;
        }

        public async Task<RankedQueueResult> PollAsync(string ticketId)
        {
            await GameServices.EnsureReadyAsync();
            ValidateTicketId(ticketId);
            TicketStatusResponse response = await MatchmakerService.Instance.GetTicketAsync(ticketId);
            switch (response?.Value)
            {
                case MatchIdAssignment assignment:
                    return RankedQueueStatus.Map(assignment.Status.ToString(), assignment.MatchId, assignment.Message);
                case CustomAssignment assignment:
                    return RankedQueueStatus.Map(assignment.Status.ToString(), assignment.MatchId, assignment.Message);
                case IpPortAssignment assignment:
                    return RankedQueueStatus.Map(assignment.Status.ToString(), assignment.MatchId, assignment.Message);
                case MultiplayAssignment assignment:
                    return RankedQueueStatus.Map(assignment.Status.ToString(), assignment.MatchId, assignment.Message);
                case NoneAssignment assignment:
                    return RankedQueueStatus.Map(assignment.Status.ToString(), null, assignment.Message);
                default:
                    return new RankedQueueResult
                    {
                        state = RankedQueueState.Failed,
                        message = "Unknown Matchmaker assignment: " + (response?.Value?.GetType().Name ?? "<null>")
                    };
            }
        }

        public async Task CancelAsync(string ticketId)
        {
            await GameServices.EnsureReadyAsync();
            ValidateTicketId(ticketId);
            await MatchmakerService.Instance.DeleteTicketAsync(ticketId);
        }

        private static void ValidateTicketId(string ticketId)
        {
            if (string.IsNullOrWhiteSpace(ticketId))
                throw new ArgumentException("ticketId must not be blank.", nameof(ticketId));
        }
    }
}
