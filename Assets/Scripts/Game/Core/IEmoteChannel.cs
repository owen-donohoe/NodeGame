namespace NodeWar.Core
{
    public interface IEmoteChannel
    {
        void Send(EmoteType emote);
        event System.Action<int, EmoteType> EmoteReceived;
    }
}
