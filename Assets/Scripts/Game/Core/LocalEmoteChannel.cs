namespace NodeWar.Core
{
    /// <summary>Local feedback belongs to the HUD; there is no peer or bot reply.</summary>
    public class LocalEmoteChannel : IEmoteChannel
    {
        public void Send(EmoteType emote) { }

        public event System.Action<int, EmoteType> EmoteReceived
        {
            add { }
            remove { }
        }
    }
}
