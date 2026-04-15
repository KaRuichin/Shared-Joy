namespace Shared_Joy.Models;

/// <summary>
/// 播放状态模型
/// </summary>
public class PlaybackState
{
    /// <summary>是否正在播放</summary>
    public bool IsPlaying { get; set; }

    /// <summary>当前播放歌曲</summary>
    public SpotifyTrack? CurrentTrack { get; set; }

    /// <summary>播放进度（毫秒）</summary>
    public int ProgressMs { get; set; }

    /// <summary>设备名称</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>设备 ID（用于将歌曲精确加入同一设备队列）</summary>
    public string DeviceId { get; set; } = string.Empty;
}
