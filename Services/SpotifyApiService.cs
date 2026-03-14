using Shared_Joy.Models;

namespace Shared_Joy.Services;

/// <summary>
/// Spotify Web API 交互服务实现
/// </summary>
public class SpotifyApiService : ISpotifyApiService
{
    private readonly ISpotifyAuthService _authService;

    public SpotifyApiService(ISpotifyAuthService authService)
    {
        _authService = authService;
    }

    public Task<List<SpotifyTrack>> SearchTracksAsync(string query, int limit = 20)
    {
        // TODO: Phase 3 实现搜索
        throw new NotImplementedException();
    }

    public Task<PlaybackState?> GetCurrentPlaybackAsync()
    {
        // TODO: Phase 3 实现获取播放状态
        throw new NotImplementedException();
    }

    public Task<bool> AddToQueueAsync(string trackUri)
    {
        // TODO: Phase 3 实现添加到队列
        throw new NotImplementedException();
    }

    public Task<bool> PlayAsync()
    {
        // TODO: Phase 3 实现播放
        throw new NotImplementedException();
    }

    public Task<bool> PauseAsync()
    {
        // TODO: Phase 3 实现暂停
        throw new NotImplementedException();
    }

    public Task<bool> SkipNextAsync()
    {
        // TODO: Phase 3 实现跳过
        throw new NotImplementedException();
    }

    public Task<List<SpotifyTrack>> GetQueueAsync()
    {
        // TODO: Phase 3 实现获取队列
        throw new NotImplementedException();
    }
}
