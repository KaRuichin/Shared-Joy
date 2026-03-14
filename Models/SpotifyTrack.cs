namespace Shared_Joy.Models;

/// <summary>
/// Spotify 歌曲模型
/// </summary>
public class SpotifyTrack
{
    /// <summary>Spotify 歌曲 ID</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>歌曲名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>艺术家列表（逗号分隔）</summary>
    public string Artists { get; set; } = string.Empty;

    /// <summary>专辑名称</summary>
    public string AlbumName { get; set; } = string.Empty;

    /// <summary>专辑封面图片 URL</summary>
    public string AlbumImageUrl { get; set; } = string.Empty;

    /// <summary>Spotify URI（如 spotify:track:xxx）</summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>歌曲时长（毫秒）</summary>
    public int DurationMs { get; set; }
}
