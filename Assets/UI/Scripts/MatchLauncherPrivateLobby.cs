using System;
using NodeWar.Network;
using UnityEngine;

namespace NodeWar.Lobby
{
    public partial class MatchLauncher
    {
        public PrivateLobbyModel PrivateLobby { get; private set; }
        private bool privateLobbyActive, privatePeerCompatible, privateCoordinator;
        private string privateLocalId, privateLocalName, privatePeerId, privateBlockedId;
        private float privateNextSend;
        private float privateLastHeard;
        private PrivateLobbyPacket privatePendingAction;
        public bool PrivateReady => PrivateLobby != null && PrivateLobby.CanStart(privateLocalId)
            && (!privateCoordinator || privatePeerCompatible);

        public void HostPrivate(LobbyMode mode, string id, string name)
        {
            ResetPrivateLobby();
            privateLobbyActive = privateCoordinator = true;
            privateLocalId = id; privateLocalName = name;
            PrivateLobby = new PrivateLobbyModel(mode, id, name);
            JoinCode = null;
            SetRanked(null, null, NoDeadline, HandshakeTimeout);
            BeginHost(false, null);
        }

        public void JoinPrivate(string code, string id, string name)
        {
            JoinRelay(code);
            privateLobbyActive = true; privateLocalId = id; privateLocalName = name;
            privateLastHeard = Time.unscaledTime;
        }

        public void PrivateAction(byte kind, int a = 0, int b = 0)
        {
            if (PrivateLobby == null || !PrivateLobby.IsHost(privateLocalId)) return;
            var packet = new PrivateLobbyPacket { Kind = kind, Id = privateLocalId,
                Revision = PrivateLobby.Revision, A = a, B = b };
            if (privateCoordinator) ApplyPrivateAction(packet);
            else { privatePendingAction = packet; networkManager?.Send(packet.Encode()); }
        }

        private void ApplyPrivateAction(PrivateLobbyPacket p)
        {
            if (PrivateLobby == null || p.Revision != PrivateLobby.Revision || !PrivateLobby.IsHost(p.Id)) return;
            switch (p.Kind)
            {
                case 2: PrivateLobby.Swap(p.Id, p.A, p.B); break;
                case 3: PrivateLobby.MakeHost(p.Id, p.A); break;
                case 4:
                    string removed = PrivateLobby.Player(p.A);
                    if (PrivateLobby.Remove(p.Id, p.A))
                    {
                        privateBlockedId = removed; privatePeerCompatible = false;
                        if (removed == privateLocalId)
                        {
                            // The remaining host creates a fresh allocation when it receives this roster.
                            SendPrivateSnapshot(); SendPrivateSnapshot(); SendPrivateSnapshot(); networkManager?.Flush();
                            Cleanup(); Fail("You were removed from the lobby.", "The host is creating a new room."); return;
                        }
                    }
                    break;
                case 5:
                    if (!privatePeerCompatible || !PrivateLobby.BeginStart(p.Id)) return;
                    SendPrivateSnapshot(); Notify(); return;
            }
            SendPrivateSnapshot(); Notify();
        }

        private void PumpPrivateLobby()
        {
            if (!privateLobbyActive || networkManager == null || Time.unscaledTime < privateNextSend) return;
            privateNextSend = Time.unscaledTime + 0.4f;
            if (privateCoordinator && privatePeerId != null && Time.unscaledTime - privateLastHeard > 8f)
            { RemoveDepartedPeer(); }
            if (!privateCoordinator && Time.unscaledTime - privateLastHeard > (PrivateLobby == null ? 30f : 8f))
            { Cleanup(); Fail("The private lobby closed.", "Ask the host for a new join code."); return; }
            if (privateCoordinator) SendPrivateSnapshot();
            else
            {
                networkManager.Send(new PrivateLobbyPacket { Id = privateLocalId, Name = privateLocalName }.Encode());
                if (privatePendingAction != null) networkManager.Send(privatePendingAction.Encode());
            }
        }

        private void SendPrivateSnapshot()
        {
            if (PrivateLobby != null) networkManager?.Send(new PrivateLobbyPacket { Kind = 1, Model = PrivateLobby,
                Id = privateLocalId, A = PrivateLobby.Starting ? 1 : 0 }.Encode());
        }

        private bool ReceivePrivateLobby(byte[] bytes)
        {
            if (bytes.Length == 0 || bytes[0] != PrivateLobbyPacket.Marker) return false;
            if (!privateLobbyActive || !PrivateLobbyPacket.TryRead(bytes, out var packet)) return true;
            if (privateCoordinator)
            {
                if (packet.Kind == 0 && packet.Id != privateLocalId && packet.Id != privateBlockedId)
                {
                    if (privatePeerId == null || privatePeerId == packet.Id)
                    {
                        privatePeerId = packet.Id;
                        privateLastHeard = Time.unscaledTime;
                        if (PrivateLobby.AddGuest(packet.Id, packet.Name)) Notify();
                    }
                }
                else if (packet.Id == privatePeerId)
                {
                    privateLastHeard = Time.unscaledTime;
                    if (packet.Kind == 6)
                        RemoveDepartedPeer();
                    else if (packet.Kind == 7 && PrivateLobby.IsStartingAt(packet.Revision)
                        && PrivateLobby.CanStart(PrivateLobby.HostId))
                    {
                        if (!RecheckLoadout()) return true;
                        byte[] ack = InputSerializer.SerializeHandshakeAck(LocalBuildIdentity.Current);
                        networkManager.Send(ack); networkManager.Send(ack); networkManager.Send(ack);
                        Succeed();
                    }
                    else ApplyPrivateAction(packet);
                }
            }
            else if (packet.Kind == 1)
            {
                privateLastHeard = Time.unscaledTime;
                if (PrivateLobby != null && packet.Model.Revision < PrivateLobby.Revision) return true;
                bool wasSeated = PrivateLobby?.SeatOf(privateLocalId) >= 0;
                PrivateLobby = packet.Model;
                // The flag rides with this snapshot's revision; the model never carries an older start over.
                if (packet.A == 1) PrivateLobby.BeginStart(PrivateLobby.HostId);
                if (privatePendingAction != null && privatePendingAction.Revision != PrivateLobby.Revision) privatePendingAction = null;
                if (wasSeated && PrivateLobby.SeatOf(privateLocalId) < 0)
                { Cleanup(); Fail("You were removed from the lobby.", "Ask the host for a new invitation."); }
                else if (PrivateLobby.IsHost(privateLocalId) && PrivateLobby.SeatOf(packet.Id) < 0)
                {
                    var mode = PrivateLobby.Mode;
                    HostPrivate(mode, privateLocalId, privateLocalName); return true;
                }
                else if (packet.A == 1 && PrivateLobby.SeatOf(privateLocalId) >= 0)
                {
                    if (!RecheckLoadout()) return true;
                    networkManager?.Send(new PrivateLobbyPacket { Kind = 7, Id = privateLocalId, Revision = PrivateLobby.Revision }.Encode());
                }
                Notify();
            }
            else if (!privateCoordinator && packet.Kind == 6)
            { Cleanup(); Fail("The private lobby closed.", "Ask the host for a new join code."); }
            return true;
        }

        private void RemoveDepartedPeer()
        {
            int seat = PrivateLobby.SeatOf(privatePeerId);
            PrivateLobby.CancelStart();
            PrivateLobby.MakeHost(privatePeerId, PrivateLobby.SeatOf(privateLocalId));
            PrivateLobby.Remove(privateLocalId, seat);
            privatePeerId = null; privatePeerCompatible = false; Notify();
        }

        private void LeavePrivateLobby()
        {
            if (privateLobbyActive)
            {
                var bytes = new PrivateLobbyPacket { Kind = 6, Id = privateLocalId }.Encode();
                networkManager?.Send(bytes); networkManager?.Send(bytes); networkManager?.Flush();
            }
            ResetPrivateLobby();
        }
        private void ResetPrivateLobby()
        {
            privateLobbyActive = privatePeerCompatible = privateCoordinator = false;
            privatePendingAction = null;
            PrivateLobby = null; privatePeerId = privateBlockedId = null; privateNextSend = 0;
        }
    }
}
