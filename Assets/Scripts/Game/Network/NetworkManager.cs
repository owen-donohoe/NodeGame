using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Collections;

namespace NodeWar.Network
{
    /// <summary>
    /// Raw UDP send/receive. Opens a socket, sends byte arrays to a remote endpoint,
    /// receives on a background thread, and queues incoming packets for main-thread consumption.
    /// No external dependencies. Built-in .NET sockets only.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public const int DEFAULT_PORT = 7777;

        private UdpClient udpClient;
        private IPEndPoint remoteEndPoint;
        private Thread receiveThread;
        private volatile bool isRunning;

        // Thread-safe incoming packet queue
        private readonly object queueLock = new object();
        private Queue<byte[]> incomingQueue = new Queue<byte[]>();

        // Connection state
        public bool IsConnected { get; private set; }
        public bool IsHost { get; private set; }

        public enum TransportMode { DirectUDP, UnityRelay }
        private TransportMode mode = TransportMode.DirectUDP;

        private NetworkDriver relayDriver;
        private NetworkConnection relayPeerConnection;
        private bool relayReady = false;

        public string JoinCode { get; private set; }
        public bool RelayReady => relayReady;

        public async void StartAsRelayHost()
        {
            mode = TransportMode.UnityRelay;
            IsHost = true;
            isRunning = true;
            relayReady = false;

            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Alternative if .ToRelayServerData() isn't found:
            //var relayServerData = AllocationUtils.ToRelayServerData(allocation, "udp");

            var relayServerData = allocation.ToRelayServerData("udp");
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref relayServerData);

            relayDriver = NetworkDriver.Create(settings);
            relayDriver.Bind(NetworkEndpoint.AnyIpv4);
            relayDriver.Listen();

            relayReady = true;
            Debug.Log("[Net] Relay host ready. Code: " + JoinCode);
        }

        public async void StartAsRelayClient(string joinCode)
        {
            mode = TransportMode.UnityRelay;
            IsHost = false;
            isRunning = true;
            relayReady = false;
            JoinCode = joinCode;

            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var relayServerData = joinAllocation.ToRelayServerData("udp");
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref relayServerData);

            relayDriver = NetworkDriver.Create(settings);
            relayDriver.Bind(NetworkEndpoint.AnyIpv4);
            relayPeerConnection = relayDriver.Connect();

            relayReady = true;
            Debug.Log("[Net] Relay client connecting.");
        }

        /// <summary>
        /// Open socket as host. Listens on the specified port.
        /// Remote endpoint is set when first packet arrives (handshake).
        /// </summary>
        public void StartAsHost(int port = DEFAULT_PORT)
        {
            IsHost = true;
            udpClient = new UdpClient(port);
            StartReceiveThread();
            Debug.Log("[Net] Hosting on port " + port);
        }

        /// <summary>
        /// Open socket as client. Connects to remote host IP and port.
        /// </summary>
        public void StartAsClient(string remoteIP, int port = DEFAULT_PORT)
        {
            IsHost = false;
            udpClient = new UdpClient(0); // bind to any available local port
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteIP), port);
            IsConnected = true;
            StartReceiveThread();
            Debug.Log("[Net] Connecting to " + remoteIP + ":" + port);
        }

        /// <summary>
        /// Send raw bytes to the remote endpoint.
        /// </summary>
        public void Send(byte[] data)
        {
            if (mode == TransportMode.DirectUDP)
            {
                if (udpClient == null || remoteEndPoint == null) return;
                try { udpClient.Send(data, data.Length, remoteEndPoint); }
                catch (SocketException e) { Debug.LogWarning("[Net] Send failed: " + e.Message); }
                return;
            }

            if (!relayDriver.IsCreated || relayPeerConnection == default) return;

            relayDriver.BeginSend(relayPeerConnection, out var writer);
            var nativeData = new NativeArray<byte>(data, Allocator.Temp);
            writer.WriteBytes(nativeData);
            nativeData.Dispose();
            relayDriver.EndSend(writer);
        }

        /// <summary>
        /// Drain all packets received since last call. Returns empty array if none.
        /// Call from main thread only (Update loop).
        /// </summary>
        public byte[][] ReceiveAll()
        {
            if (mode == TransportMode.DirectUDP)
            {
                lock (queueLock)
                {
                    if (incomingQueue.Count == 0) return Array.Empty<byte[]>();
                    byte[][] result = incomingQueue.ToArray();
                    incomingQueue.Clear();
                    return result;
                }
            }

            if (!relayDriver.IsCreated || !relayReady) return Array.Empty<byte[]>();

            relayDriver.ScheduleUpdate().Complete();

            // Host: accept incoming connection
            if (IsHost)
            {
                NetworkConnection incoming;
                while ((incoming = relayDriver.Accept()) != default)
                {
                    relayPeerConnection = incoming;
                    IsConnected = true;
                    Debug.Log("[Net] Relay peer connected.");
                }
            }

            if (relayPeerConnection == default) return Array.Empty<byte[]>();

            // Check connection state (client)
            if (!IsHost && !IsConnected)
            {
                var state = relayDriver.GetConnectionState(relayPeerConnection);
                if (state == NetworkConnection.State.Connected)
                {
                    IsConnected = true;
                    Debug.Log("[Net] Relay connected to host.");
                }
            }

            var packets = new List<byte[]>();
            NetworkEvent.Type evt;
            DataStreamReader reader;

            while ((evt = relayDriver.PopEventForConnection(relayPeerConnection, out reader)) != NetworkEvent.Type.Empty)
            {
                switch (evt)
                {
                    case NetworkEvent.Type.Data:
                        var nativeData = new NativeArray<byte>(reader.Length, Allocator.Temp);
                        reader.ReadBytes(nativeData);
                        packets.Add(nativeData.ToArray());
                        nativeData.Dispose();
                        break;
                    case NetworkEvent.Type.Connect:
                        IsConnected = true;
                        Debug.Log("[Net] Relay connect event.");
                        break;
                    case NetworkEvent.Type.Disconnect:
                        IsConnected = false;
                        relayPeerConnection = default;
                        Debug.Log("[Net] Relay disconnected.");
                        break;
                }
            }

            return packets.ToArray();
        }

        /// <summary>
        /// Returns the local IP address (first IPv4 non-loopback) for display in lobby.
        /// </summary>
        public static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                for (int i = 0; i < host.AddressList.Length; i++)
                {
                    if (host.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ip = host.AddressList[i].ToString();
                        if (!ip.StartsWith("127."))
                            return ip;
                    }
                }
            }
            catch (Exception) { }
            return "127.0.0.1";
        }

        public void Shutdown()
        {
            isRunning = false;
            IsConnected = false;

            if (mode == TransportMode.DirectUDP)
            {
                if (udpClient != null) { udpClient.Close(); udpClient = null; }
                if (receiveThread != null && receiveThread.IsAlive) { receiveThread.Join(500); receiveThread = null; }
            }
            else
            {
                if (relayDriver.IsCreated) relayDriver.Dispose();
            }

            Debug.Log("[Net] Shutdown.");
        }

        // --- Internal ---

        private void StartReceiveThread()
        {
            isRunning = true;
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        private void ReceiveLoop()
        {
            while (isRunning)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udpClient.Receive(ref sender);

                    // Host: lock onto first sender as remote endpoint
                    if (IsHost && remoteEndPoint == null)
                    {
                        remoteEndPoint = sender;
                        IsConnected = true;
                        Debug.Log("[Net] Remote connected from " + sender.Address + ":" + sender.Port);
                    }

                    lock (queueLock)
                    {
                        incomingQueue.Enqueue(data);
                    }
                }
                catch (SocketException)
                {
                    // Socket closed during Receive — expected on shutdown
                    if (!isRunning) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }
    }
}