using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

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
            if (udpClient == null || remoteEndPoint == null) return;

            try
            {
                udpClient.Send(data, data.Length, remoteEndPoint);
            }
            catch (SocketException e)
            {
                Debug.LogWarning("[Net] Send failed: " + e.Message);
            }
        }

        /// <summary>
        /// Drain all packets received since last call. Returns empty array if none.
        /// Call from main thread only (Update loop).
        /// </summary>
        public byte[][] ReceiveAll()
        {
            lock (queueLock)
            {
                if (incomingQueue.Count == 0)
                    return Array.Empty<byte[]>();

                byte[][] packets = incomingQueue.ToArray();
                incomingQueue.Clear();
                return packets;
            }
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

            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }

            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(500);
                receiveThread = null;
            }

            Debug.Log("[Net] Shutdown complete.");
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