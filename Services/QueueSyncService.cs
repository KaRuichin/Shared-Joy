namespace Shared_Joy.Services;

/// <summary>
/// 队列同步服务实现 —— 定时将高票歌曲推送至 Spotify 播放队列
/// </summary>
public class QueueSyncService : IQueueSyncService
{
    private readonly IVotingEngine _votingEngine;
    private readonly ISpotifyApiService _spotifyApi;
    private readonly IDatabaseService _database;

    public QueueSyncService(
        IVotingEngine votingEngine,
        ISpotifyApiService spotifyApi,
        IDatabaseService database)
    {
        _votingEngine = votingEngine;
        _spotifyApi = spotifyApi;
        _database = database;
    }

    public bool IsRunning { get; private set; }

    public Task StartAsync()
    {
        // TODO: Phase 5 实现后台同步启动
        throw new NotImplementedException();
    }

    public Task StopAsync()
    {
        // TODO: Phase 5 实现后台同步停止
        throw new NotImplementedException();
    }
}
