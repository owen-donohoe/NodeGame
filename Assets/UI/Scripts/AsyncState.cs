namespace NodeWar.Lobby
{
    public enum AsyncStatus { Loading, Ready, Empty, Offline, Error }
    public enum AsyncResult { Loading, Retry, Ready, Empty, Offline, Error }

    /// <summary>Time is supplied by the caller, in milliseconds.</summary>
    public sealed class AsyncState
    {
        public AsyncStatus Status { get; }
        public long StartedAt { get; }
        public bool HasContent { get; }
        public bool IsStale { get; }
        public bool IsOffline { get; }
        public bool ShowBanner => HasContent && IsStale;

        public AsyncState(long now = 0) : this(AsyncStatus.Loading, now, false, false, false) { }

        private AsyncState(AsyncStatus status, long startedAt, bool hasContent, bool stale, bool offline)
        {
            Status = status; StartedAt = startedAt; HasContent = hasContent; IsStale = stale; IsOffline = offline;
        }

        public bool ShowLoading(long now) => Status == AsyncStatus.Loading && now >= StartedAt && now - StartedAt >= 300;

        public AsyncState Reduce(AsyncResult result, long now)
        {
            switch (result)
            {
                case AsyncResult.Loading:
                case AsyncResult.Retry:
                    return new AsyncState(AsyncStatus.Loading, now, HasContent, IsStale, IsOffline);
                case AsyncResult.Ready:
                    return new AsyncState(AsyncStatus.Ready, now, true, false, false);
                case AsyncResult.Empty:
                    return new AsyncState(AsyncStatus.Empty, now, false, false, false);
                default:
                    bool offline = result == AsyncResult.Offline;
                    return new AsyncState(HasContent ? AsyncStatus.Ready : offline ? AsyncStatus.Offline : AsyncStatus.Error,
                        now, HasContent, HasContent, offline);
            }
        }
    }
}
