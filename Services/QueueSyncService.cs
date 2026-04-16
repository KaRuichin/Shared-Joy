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
    /// 单次同步：将投票最高且尚未推送过的歌曲添加到 Spotify 队列
    ///
    /// 防重复推送的核心机制：
    ///   - _pushedTrackIds 记录本次会话已推送过的所有 trackId
    ///   - 每首歌只会被推送一次（推送后立即加入集合 + 从投票池移除）
    ///   - SemaphoreSlim 保证 TriggerSyncAsync 和 SyncLoopAsync 不会并发执行此方法
    /// </summary>
    private async Task TrySyncAsync()
    {
        // 获取当前播放状态（无活跃设备则无法入队）
        var playback = await _spotifyApi.GetCurrentPlaybackAsync();
        if (playback is null)
            return;

        // 获取投票排行榜
        var ranked = _votingEngine.GetRankedQueue();
        if (ranked.Count == 0)
            return;

        // 找到票数最高且本次会话尚未推送过的歌曲
        var candidate = ranked.FirstOrDefault(v => !_pushedTrackIds.Contains(v.Track.Id));
        if (candidate is null)
            return;

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

        // 立即标记为已推送并从投票池移除，防止下一轮同步重复推送
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
