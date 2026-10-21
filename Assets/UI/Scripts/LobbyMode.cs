using System;
using System.Collections.Generic;

namespace NodeWar.Lobby
{
    // Persisted IDs: append only.
    public enum LobbyMode { Ranked1v1, Team2v2, FreeForAll4, Bot, Lan, Unranked1v1 }
    public enum LobbyPlayAction { Ranked, PrivateInvite, TeamChoice, FreeForAllChoice, Bot, Lan, Unranked }
    public enum ModeLaunchStrategy { Queue, Bot, Lan, Private }
    public enum ModeAvailability { Available, ComingSoon, Hidden }

    /// <summary>Immutable mode capabilities and presentation. Seat order is wire order.</summary>
    public sealed class ModeDefinition
    {
        public LobbyMode Id { get; }
        public string DisplayName { get; }
        public LobbyModeCategory Category { get; }
        public int SeatCount => teams.Length;
        public bool SupportsPrivate { get; }
        public ModeLaunchStrategy LaunchStrategy { get; }
        public string QueueKey { get; }
        public ModeAvailability Availability { get; }
        public string Colour { get; }
        public LobbyIconKind Icon { get; }
        public LobbyPlayAction PlayAction { get; }
        private readonly int[] teams;
        private readonly PrivateSeatPosition[] positions;
        private ModeDefinition(LobbyMode id, string name, LobbyModeCategory category, bool supportsPrivate,
            ModeLaunchStrategy launch, string queue, ModeAvailability availability, string colour,
            LobbyIconKind icon, LobbyPlayAction action, int[] teams, PrivateSeatPosition[] positions)
        {
            Id = id; DisplayName = name; Category = category; SupportsPrivate = supportsPrivate;
            LaunchStrategy = launch; QueueKey = queue; Availability = availability; Colour = colour;
            Icon = icon; PlayAction = action; this.teams = teams; this.positions = positions;
        }
        public int Team(int seat) => teams[seat];
        public PrivateSeatPosition Position(int seat) => positions[seat];
        public bool CanSelect(bool isPrivate) => isPrivate
            ? SupportsPrivate && Category != LobbyModeCategory.Ranked && Availability != ModeAvailability.Hidden
            : Availability == ModeAvailability.Available;
        public string DisabledReason(bool isPrivate) => CanSelect(isPrivate) ? null
            : isPrivate && (!SupportsPrivate || Category == LobbyModeCategory.Ranked) ? "Off in private" : "Coming soon";

        private static readonly int[] DuelTeams = { 0, 1 };
        private static readonly PrivateSeatPosition[] DuelPositions = { PrivateSeatPosition.Top, PrivateSeatPosition.Bottom };
        private static readonly ModeDefinition[] definitions = {
            new ModeDefinition(LobbyMode.Ranked1v1, "1v1 Ranked", LobbyModeCategory.Ranked, true,
                ModeLaunchStrategy.Queue, "ranked", ModeAvailability.Available, "#F4CC72", LobbyIconKind.Swords,
                LobbyPlayAction.Ranked, DuelTeams, DuelPositions),
            new ModeDefinition(LobbyMode.Unranked1v1, "1v1", LobbyModeCategory.Unranked, true,
                ModeLaunchStrategy.Queue, "unranked-1v1", ModeAvailability.ComingSoon, "#A8D8EF", LobbyIconKind.Swords,
                LobbyPlayAction.Unranked, DuelTeams, DuelPositions),
            new ModeDefinition(LobbyMode.Team2v2, "2v2", LobbyModeCategory.Unranked, true,
                ModeLaunchStrategy.Queue, "unranked-2v2", ModeAvailability.Available, "#A8D8EF", LobbyIconKind.Invite,
                LobbyPlayAction.TeamChoice, new[] { 0, 0, 1, 1 }, new[] { PrivateSeatPosition.TopLeft,
                    PrivateSeatPosition.TopRight, PrivateSeatPosition.BottomLeft, PrivateSeatPosition.BottomRight }),
            new ModeDefinition(LobbyMode.FreeForAll4, "1v1v1v1", LobbyModeCategory.Unranked, true,
                ModeLaunchStrategy.Queue, "unranked-ffa4", ModeAvailability.Available, "#E3B8EB", LobbyIconKind.Swords,
                LobbyPlayAction.FreeForAllChoice, new[] { 0, 1, 2, 3 }, new[] { PrivateSeatPosition.Top,
                    PrivateSeatPosition.Right, PrivateSeatPosition.Bottom, PrivateSeatPosition.Left }),
            new ModeDefinition(LobbyMode.Bot, "Bot", LobbyModeCategory.Other, false,
                ModeLaunchStrategy.Bot, null, ModeAvailability.Available, "#C8E5A0", LobbyIconKind.Suit,
                LobbyPlayAction.Bot, DuelTeams, DuelPositions),
            new ModeDefinition(LobbyMode.Lan, "LAN", LobbyModeCategory.Other, false,
                ModeLaunchStrategy.Lan, null, ModeAvailability.Available, "#A8D8EF", LobbyIconKind.Invite,
                LobbyPlayAction.Lan, DuelTeams, DuelPositions)
        };
        public static IReadOnlyList<ModeDefinition> All { get; } = Array.AsReadOnly(definitions);
        static ModeDefinition() { Validate(All); }
        public static void Validate(IReadOnlyList<ModeDefinition> table)
        {
            if (table == null || table.Count != Enum.GetValues(typeof(LobbyMode)).Length)
                throw new ArgumentException("Every stable mode ID needs one definition.");
            for (int i = 0; i < table.Count; i++)
            {
                var row = table[i];
                if (row == null || !Enum.IsDefined(typeof(LobbyMode), row.Id) || string.IsNullOrWhiteSpace(row.DisplayName)
                    || !Enum.IsDefined(typeof(LobbyModeCategory), row.Category)
                    || !Enum.IsDefined(typeof(ModeLaunchStrategy), row.LaunchStrategy)
                    || !Enum.IsDefined(typeof(ModeAvailability), row.Availability)
                    || (row.SeatCount != 2 && row.SeatCount != 4) || row.positions.Length != row.SeatCount
                    || (row.LaunchStrategy == ModeLaunchStrategy.Queue && string.IsNullOrWhiteSpace(row.QueueKey))
                    || (row.SupportsPrivate && (row.LaunchStrategy == ModeLaunchStrategy.Bot || row.LaunchStrategy == ModeLaunchStrategy.Lan)))
                    throw new ArgumentException("Invalid mode definition.");
                for (int j = 0; j < i; j++) if (table[j].Id == row.Id) throw new ArgumentException("Duplicate mode ID.");
                for (int seat = 0; seat < row.SeatCount; seat++)
                {
                    if (row.Team(seat) < 0 || row.Team(seat) >= row.SeatCount) throw new ArgumentException("Invalid team.");
                    for (int other = 0; other < seat; other++)
                        if (row.Position(seat) == row.Position(other)) throw new ArgumentException("Duplicate seat position.");
                }
            }
        }
        public static ModeDefinition For(LobbyMode id)
        {
            foreach (var row in definitions) if (row.Id == id) return row;
            throw new ArgumentOutOfRangeException(nameof(id));
        }
    }

    public readonly struct LobbyModeSelection
    {
        public LobbyMode Mode { get; }
        public bool Private { get; }
        public bool IsPrivate => Private;
        public ModeDefinition Definition => ModeDefinition.For(Mode);
        public ModeLaunchStrategy LaunchStrategy => Private ? ModeLaunchStrategy.Private : Definition.LaunchStrategy;
        public LobbyModeSelection(LobbyMode mode, bool isPrivate = false)
        {
            var valid = Enum.IsDefined(typeof(LobbyMode), mode) ? mode : LobbyMode.Ranked1v1;
            // Private wins over an incompatible saved mode: retain the room intent.
            if (isPrivate && (!ModeDefinition.For(valid).SupportsPrivate || valid == LobbyMode.Ranked1v1))
                valid = LobbyMode.Unranked1v1;
            if (!isPrivate && ModeDefinition.For(valid).Availability != ModeAvailability.Available)
                valid = LobbyMode.Ranked1v1;
            Mode = valid; Private = isPrivate;
        }
        public GameMode LegacyMode => LaunchStrategy == ModeLaunchStrategy.Bot ? GameMode.Bot
            : LaunchStrategy == ModeLaunchStrategy.Lan || Private ? GameMode.OneVsOne : GameMode.Locked;
        public LobbyPlayAction PlayAction => Private ? LobbyPlayAction.PrivateInvite : Definition.PlayAction;
        public static LobbyModeSelection FromProfile(PlayerProfileData data)
        {
            if (data.lobbyModeVersion > 0) return new LobbyModeSelection((LobbyMode)data.selectedLobbyModeIndex, data.privateLobbyMode);
            GameMode legacy = (GameMode)data.selectedGameModeIndex;
            return new LobbyModeSelection(legacy == GameMode.Bot ? LobbyMode.Bot : LobbyMode.Ranked1v1, legacy == GameMode.OneVsOne);
        }
        public void Store(ref PlayerProfileData data)
        {
            data.lobbyModeVersion = 1;
            data.selectedLobbyModeIndex = (int)Mode;
            data.privateLobbyMode = Private;
            data.selectedGameModeIndex = (int)LegacyMode;
        }
    }
}
