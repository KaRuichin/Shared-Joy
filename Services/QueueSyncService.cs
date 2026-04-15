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
    /// 立即触发一次同步（投票后调用，加快推送响应）
    /// </summary>
    public async Task TriggerSyncAsync()
    {
        if (!IsRunning)
            return;

        try
        {
            await TrySyncAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QueueSync] 触发同步异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 后台同步循环：启动时立即执行一次同步，然后每 5 秒检查一次
    /// （改为 5 秒因为已改进推送策略，可更频繁地检查）
    /// </summary>
    private async Task SyncLoopAsync(CancellationToken ct)
    {
        // 启动时立即执行一次同步，无需等待
        try
        {
            await TrySyncAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QueueSync] 初始同步异常: {ex.Message}");
        }

        // 然后定期检查
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await TrySyncAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QueueSync] 同步异常: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 单次同步：检查 Spotify 队列状态，按需推送高票歌曲
    /// 
    /// 推送策略：当投票队列非空且队列中没有待推送歌曲时，立即推送最高票歌曲
    /// 如果已推送歌曲未在 Spotify 中出现，说明有网络延迟，稍后重试
    /// </summary>
    private async Task TrySyncAsync()
    {
        // 获取当前播放状态
        var playback = await _spotifyApi.GetCurrentPlaybackAsync();
        if (playback is null)
            return; // 无活跃设备，跳过

        // 获取投票排行榜
        var ranked = _votingEngine.GetRankedQueue();
        if (ranked.Count == 0)
            return; // 没有待投票歌曲

        // 获取 Spotify 队列，判断是否需要补充
        var spotifyQueue = await _spotifyApi.GetQueueAsync();

        // 【改进策略】更激进的推送逻辑：
        //   - 如果 Spotify 队列为空或很少，立即推送高票歌曲
        //   - 检查是否有已推送但未在队列中出现的歌曲（网络延迟场景）
        //   - 只有当队列中已经有充足的待播放歌曲时，才跳过推送

        // 检查是否有已推送歌曲已出现在 Spotify 队列中（确认推送成功）
        var confirmedInQueue = _pushedTrackIds.Where(id => spotifyQueue.Any(t => t.Id == id)).ToList();
        var unconfirmedCount = _pushedTrackIds.Count - confirmedInQueue.Count;

        // 如果队列充足（保持至少 3 首待播歌曲）且没有待确认的推送，则不急于推送
        if (spotifyQueue.Count >= 3 && unconfirmedCount == 0)
            return;

        // 找到第一首尚未推送过的歌曲
        var candidate = ranked.FirstOrDefault(v => !_pushedTrackIds.Contains(v.Track.Id));
        if (candidate is null)
            return; // 所有高票歌曲都已推送

        // 推送到 Spotify 队列：优先指定当前播放设备；失败则降级重试一次（不指定设备）
        var success = await _spotifyApi.AddToQueueAsync(candidate.Track.Uri, playback.DeviceId);
        if (!success)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[QueueSync] 指定设备入队失败，降级重试: track={candidate.Track.Name}, deviceId={playback.DeviceId}, deviceName={playback.DeviceName}");

            success = await _spotifyApi.AddToQueueAsync(candidate.Track.Uri);
            if (!success)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[QueueSync] 入队最终失败: track={candidate.Track.Name}, uri={candidate.Track.Uri}, deviceId={playback.DeviceId}, deviceName={playback.DeviceName}");
                return;
            }
        }

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
