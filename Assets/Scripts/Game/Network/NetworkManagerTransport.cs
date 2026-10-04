using UnityEngine;

namespace NodeWar.Network
{
    /// <summary>
    /// NetworkManager as the lockstep core's transport. A destroyed manager
    /// (Unity's overloaded null) sends nothing and receives nothing.
    /// </summary>
    internal sealed class NetworkManagerTransport : ILockstepTransport
    {
        private static readonly byte[][] None = new byte[0][];
        private readonly NetworkManager manager;

        public NetworkManagerTransport(NetworkManager manager) { this.manager = manager; }

        public void Send(byte[] data)
        {
            if (manager == null) return;
            manager.Send(data);
        }

        public byte[][] ReceiveAll()
        {
            if (manager == null) return None;
            return manager.ReceiveAll() ?? None;
        }

        public void Flush()
        {
            if (manager == null) return;
            manager.Flush();
        }
    }

    /// <summary>Routes the lockstep core's log to the Unity console.</summary>
    internal static class UnityLockstepLog
    {
        public static void Write(LockstepLogLevel level, string message)
        {
            switch (level)
            {
                case LockstepLogLevel.Warning: Debug.LogWarning(message); break;
                case LockstepLogLevel.Error: Debug.LogError(message); break;
                default: Debug.Log(message); break;
            }
        }
    }
}
