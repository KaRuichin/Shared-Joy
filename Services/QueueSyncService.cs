namespace Shared_Joy.Services;

/// <summary>
/// 队列同步服务实现 —— 定时将高票歌曲推送至 Spotify 播放队列
///
/// 每 7 秒轮询 Spotify 播放状态，当 Spotify 队列即将耗尽时，
/// 自动取投票排名第一的歌曲添加到 Spotify 队列，并记录到播放历史。
/// </summary>
public class QueueSyncService : IQueueSyncService
{
    private readonly IVotingEngine _votingEngine;
    private readonly ISpotifyApiService _spotifyApi;
    private readonly IDatabaseService _database;
    private readonly ISessionManager _sessionManager;

    private CancellationTokenSource? _cts;
    private Task? _syncTask;

    // 已推送到 Spotify 队列的 trackId 集合，防止重复推送
    private readonly HashSet<string> _pushedTrackIds = [];

    public QueueSyncService(
        IVotingEngine votingEngine,
        ISpotifyApiService spotifyApi,
        IDatabaseService database,
        ISessionManager sessionManager)
    {
        _votingEngine = votingEngine;
        _spotifyApi = spotifyApi;
        _database = database;
        _sessionManager = sessionManager;
    }

    public bool IsRunning { get; private set; }

    public Task StartAsync()
    {
        if (IsRunning)
            return Task.CompletedTask;

        _cts = new CancellationTokenSource();
        _pushedTrackIds.Clear();
        IsRunning = true;

        _syncTask = Task.Run(() => SyncLoopAsync(_cts.Token));

        System.Diagnostics.Debug.WriteLine("[QueueSync] 后台同步已启动");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
            return;

        IsRunning = false;

        if (_cts is not null)
        {
            await _cts.CancelAsync();

            if (_syncTask is not null)
            {
                try { await _syncTask; }
                catch (OperationCanceledException) { }
            }

            _cts.Dispose();
            _cts = null;
        }

        _pushedTrackIds.Clear();
        System.Diagnostics.Debug.WriteLine("[QueueSync] 后台同步已停止");
    }

    /// <summary>
    /// 后台同步循环：每 7 秒检查一次 Spotify 播放状态和投票队列
    /// </summary>
    private async Task SyncLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TrySyncAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QueueSync] 同步异常: {ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(7), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 单次同步：检查 Spotify 队列状态，按需推送高票歌曲
    /// </summary>
    private async Task TrySyncAsync()
    {
        // 获取当前播放状态
        var playback = await _spotifyApi.GetCurrentPlaybackAsync();
        if (playback is null)
            return; // 无活跃设备，跳过

        // 获取 Spotify 队列，判断是否需要补充
        var spotifyQueue = await _spotifyApi.GetQueueAsync();
        if (spotifyQueue.Count > 2)
            return; // 队列中还有足够歌曲，不急于补充

        // 获取投票排行榜
        var ranked = _votingEngine.GetRankedQueue();
        if (ranked.Count == 0)
            return; // 没有待投票歌曲

        // 找到第一首尚未推送过的歌曲
        var candidate = ranked.FirstOrDefault(v => !_pushedTrackIds.Contains(v.Track.Id));
        if (candidate is null)
            return; // 所有高票歌曲都已推送

        // 推送到 Spotify 队列
        var success = await _spotifyApi.AddToQueueAsync(candidate.Track.Uri);
        if (!success)
            return;

        // 标记为已推送，从投票池移除
        _pushedTrackIds.Add(candidate.Track.Id);
        _votingEngine.RemoveTrack(candidate.Track.Id);

        // 记录播放历史
        var sessionId = _sessionManager.SessionId ?? "unknown";
        await _database.RecordPlayHistoryAsync(
            candidate.Track.Id,
            candidate.Track.Name,
            candidate.Track.Artists,
            sessionId);

        System.Diagnostics.Debug.WriteLine(
            $"[QueueSync] 已推送: {candidate.Track.Name} - {candidate.Track.Artists} (票数: {candidate.VoteCount})");
    }
}
