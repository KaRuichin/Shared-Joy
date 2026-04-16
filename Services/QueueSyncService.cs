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

    // 上次成功推送的时间（用于防止 Spotify API 传播延迟导致的重复推送）
    private DateTime _lastPushTime = DateTime.MinValue;

    // 防止 TriggerSyncAsync 和 SyncLoopAsync 并发执行 TrySyncAsync（竞态条件）
    private readonly SemaphoreSlim _syncLock = new(1, 1);

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
        _lastPushTime = DateTime.MinValue;
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
        _lastPushTime = DateTime.MinValue;
        System.Diagnostics.Debug.WriteLine("[QueueSync] 后台同步已停止");
    }

    /// <summary>
    /// 立即触发一次同步（投票后调用，加快推送响应）
    /// </summary>
    public async Task TriggerSyncAsync()
    {
        if (!IsRunning)
            return;

        // 如果当前已有同步在运行，跳过本次触发（避免与 SyncLoopAsync 竞争）
        if (!await _syncLock.WaitAsync(0))
            return;

        try
        {
            await TrySyncAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QueueSync] 触发同步异常: {ex.Message}");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// 后台同步循环：启动时立即执行一次同步，然后每 5 秒检查一次
    /// （改为 5 秒因为已改进推送策略，可更频繁地检查）
    /// </summary>
    private async Task SyncLoopAsync(CancellationToken ct)
    {
        // 启动时立即执行一次同步，无需等待
        await _syncLock.WaitAsync(ct);
        try
        {
            await TrySyncAsync();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QueueSync] 初始同步异常: {ex.Message}");
        }
        finally
        {
            _syncLock.Release();
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

            await _syncLock.WaitAsync(ct);
            try
            {
                await TrySyncAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QueueSync] 同步异常: {ex.Message}");
            }
            finally
            {
                _syncLock.Release();
            }
        }
    }

    /// <summary>
    /// 单次同步：检查 Spotify 队列状态，按需推送高票歌曲
    ///
    /// 推送策略：
    ///   1. 只统计我们自己推送的歌曲在 Spotify 队列中的数量（忽略播放列表/专辑歌曲）
    ///      — 原来用 spotifyQueue.Count >= 3 会把播放列表的后续曲目也算进去，导致永远不推送
    ///   2. 当我们的歌曲在队列中少于 2 首时，推送下一首高票歌曲
    ///   3. 推送后设置冷却时间（10 秒），避免 Spotify API 传播延迟导致的重复推送
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

        // 获取 Spotify 队列
        var spotifyQueue = await _spotifyApi.GetQueueAsync();

        // 只统计我们自己推送的歌曲还有多少在 Spotify 队列等待播放
        // 注意：不能用 spotifyQueue.Count，那包含了播放列表/专辑的后续曲目，
        //       会导致在播放列表时始终满足"队列充足"条件，投票歌曲永远不被推送
        var ourSongsInQueue = _pushedTrackIds.Count(id => spotifyQueue.Any(t => t.Id == id));

        // 已推送但尚未出现在 Spotify 队列中（API 传播延迟），避免重复推送
        var unconfirmedCount = _pushedTrackIds.Count - ourSongsInQueue;
        if (unconfirmedCount > 0)
        {
            // 推送后 10 秒内未确认，等待下一轮再判断
            var secondsSinceLastPush = (DateTime.UtcNow - _lastPushTime).TotalSeconds;
            if (secondsSinceLastPush < 10)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[QueueSync] 等待推送确认（{unconfirmedCount} 首待确认，距上次推送 {secondsSinceLastPush:F1}s）");
                return;
            }
            // 超过 10 秒仍未确认，可能歌曲已播放完或推送失败，允许继续
            System.Diagnostics.Debug.WriteLine(
                $"[QueueSync] 推送超时未确认（{unconfirmedCount} 首），继续尝试推送");
        }

        // 我们的歌曲在 Spotify 队列中已有 2 首，等待播放后再补充
        if (ourSongsInQueue >= 2)
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
                $"[QueueSync] 指定设备入队失败，降级重试: track={candidate.Track.Name}, deviceId={playback.DeviceId}");

            success = await _spotifyApi.AddToQueueAsync(candidate.Track.Uri);
            if (!success)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[QueueSync] 入队最终失败: track={candidate.Track.Name}, uri={candidate.Track.Uri}");
                return;
            }
        }

        // 标记为已推送，记录推送时间，从投票池移除
        _pushedTrackIds.Add(candidate.Track.Id);
        _lastPushTime = DateTime.UtcNow;
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
