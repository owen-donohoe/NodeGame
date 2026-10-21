using System;
using System.IO;
using System.Text;

namespace NodeWar.Lobby
{
    public enum PrivateSeatPosition { Top, TopLeft, TopRight, Right, Bottom, BottomLeft, BottomRight, Left }

    /// <summary>Lobby authority is independent of which peer owns the Relay allocation.</summary>
    public sealed class PrivateLobbyModel
    {
        public LobbyMode Mode { get; }
        public string HostId { get; private set; }
        public int Revision { get; private set; }
        private ModeDefinition Definition => ModeDefinition.For(Mode);
        public int Capacity => Definition.SeatCount;
        private readonly string[] ids = new string[4];
        private readonly string[] names = new string[4];
        public string Player(int seat) => Valid(seat) ? ids[seat] : null;
        public string Name(int seat) => Valid(seat) ? names[seat] : null;
        private bool Valid(int seat) => seat >= 0 && seat < Capacity;
        public bool IsHost(string id) => !string.IsNullOrEmpty(id) && id == HostId;
        public int SeatOf(string id) { for (int i = 0; i < Capacity; i++) if (id != null && ids[i] == id) return i; return -1; }
        public PrivateLobbyModel(LobbyMode mode, string hostId, string hostName)
        {
            if (!Enum.IsDefined(typeof(LobbyMode), mode) || !ModeDefinition.For(mode).SupportsPrivate)
                throw new ArgumentOutOfRangeException(nameof(mode));
            Mode = mode; HostId = hostId; ids[0] = hostId; names[0] = hostName;
        }
        public int Colour(int seat) => Definition.Team(seat);
        public PrivateSeatPosition Position(int seat)
        {
            if (!Valid(seat)) throw new ArgumentOutOfRangeException(nameof(seat));
            return Definition.Position(seat);
        }
        public bool CanStart(string actor) => Capacity == 2 && IsHost(actor) && ids[0] != null && ids[1] != null;
        public bool AddGuest(string id, string name)
        {
            if (string.IsNullOrEmpty(id) || SeatOf(id) >= 0) return false;
            for (int i = 0; i < Capacity; i++) if (ids[i] == null)
            { ids[i] = id; names[i] = name; Revision++; return true; }
            return false;
        }
        public bool Swap(string actor, int a, int b)
        {
            if (!IsHost(actor) || !Valid(a) || !Valid(b) || a == b) return false;
            (ids[a], ids[b]) = (ids[b], ids[a]); (names[a], names[b]) = (names[b], names[a]); Revision++; return true;
        }
        public bool MakeHost(string actor, int seat)
        {
            if (!IsHost(actor) || !Valid(seat) || ids[seat] == null || ids[seat] == HostId) return false;
            HostId = ids[seat]; Revision++; return true;
        }
        public bool Remove(string actor, int seat)
        {
            if (!IsHost(actor) || !Valid(seat) || ids[seat] == null || ids[seat] == HostId) return false;
            ids[seat] = names[seat] = null; Revision++; return true;
        }
        internal void Write(BinaryWriter writer)
        {
            writer.Write((int)Mode); writer.Write(Revision); writer.Write(HostId ?? "");
            for (int i = 0; i < Capacity; i++) { writer.Write(ids[i] ?? ""); writer.Write(names[i] ?? ""); }
        }
        internal static PrivateLobbyModel Read(BinaryReader reader)
        {
            var model = new PrivateLobbyModel((LobbyMode)reader.ReadInt32(), null, null);
            model.Revision = reader.ReadInt32(); model.HostId = reader.ReadString();
            if (model.Revision < 0 || string.IsNullOrEmpty(model.HostId)) throw new InvalidDataException();
            for (int i = 0; i < model.Capacity; i++)
            {
                string id = reader.ReadString(), name = reader.ReadString();
                if (id.Length > 128 || name.Length > 80 || (id.Length > 0 && model.SeatOf(id) >= 0)) throw new InvalidDataException();
                model.ids[i] = id.Length == 0 ? null : id; model.names[i] = name;
            }
            if (model.SeatOf(model.HostId) < 0) throw new InvalidDataException();
            return model;
        }
    }

    /// <summary>Lobby-only envelope; never sent to draft/gameplay. No backend or simulation protocol changes.</summary>
    internal sealed class PrivateLobbyPacket
    {
        internal const byte Marker = 240;
        internal byte Kind; // 0 hello, 1 snapshot, 2 swap, 3 host, 4 kick, 5 start, 6 leave, 7 start-ready
        internal int Revision, A, B;
        internal string Id = "", Name = "";
        internal PrivateLobbyModel Model;
        internal byte[] Encode()
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8);
            writer.Write(Marker); writer.Write((byte)1); writer.Write(Kind);
            writer.Write(Revision); writer.Write(A); writer.Write(B); writer.Write(Id); writer.Write(Name);
            writer.Write(Model != null); Model?.Write(writer); return stream.ToArray();
        }
        internal static bool TryRead(byte[] data, out PrivateLobbyPacket packet)
        {
            packet = null;
            if (data == null || data.Length < 3 || data.Length > 2048 || data[0] != Marker) return false;
            try
            {
                using var reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
                reader.ReadByte(); if (reader.ReadByte() != 1) return false;
                var p = new PrivateLobbyPacket { Kind = reader.ReadByte(), Revision = reader.ReadInt32(), A = reader.ReadInt32(),
                    B = reader.ReadInt32(), Id = reader.ReadString(), Name = reader.ReadString() };
                if (p.Kind > 7 || p.Id.Length > 128 || p.Name.Length > 80) return false;
                if (reader.ReadBoolean()) p.Model = PrivateLobbyModel.Read(reader);
                if (reader.BaseStream.Position != reader.BaseStream.Length || (p.Kind == 1 && p.Model == null)) return false;
                packet = p; return true;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException) { return false; }
        }
    }
}
