using Shared_Joy.Models;

namespace Shared_Joy.Services;

/// <summary>
/// Spotify Web API 交互服务接口
/// </summary>
public interface ISpotifyApiService
{
    /// <summary>搜索歌曲</summary>
    Task<List<SpotifyTrack>> SearchTracksAsync(string query, int limit = 20);

    /// <summary>获取当前播放状态</summary>
    Task<PlaybackState?> GetCurrentPlaybackAsync();

    /// <summary>将歌曲添加到播放队列（可指定 deviceId）</summary>
    Task<bool> AddToQueueAsync(string trackUri, string? deviceId = null);

    /// <summary>开始播放</summary>
    Task<bool> PlayAsync();

    /// <summary>暂停播放</summary>
    Task<bool> PauseAsync();

    /// <summary>跳到下一首</summary>
    Task<bool> SkipNextAsync();

    /// <summary>获取播放队列</summary>
    Task<List<SpotifyTrack>> GetQueueAsync();
}
