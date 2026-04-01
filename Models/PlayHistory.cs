using SQLite;

namespace Shared_Joy.Models;

/// <summary>
/// 播放历史记录（SQLite 表实体）
/// 记录每首被推送到 Spotify 队列并播放的歌曲
/// </summary>
[Table("PlayHistory")]
public class PlayHistory
{
    /// <summary>自增主键</summary>
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Spotify 歌曲 ID</summary>
    [NotNull]
    public string TrackId { get; set; } = string.Empty;

    /// <summary>歌曲名称（冗余存储，便于历史查询）</summary>
    [NotNull]
    public string TrackName { get; set; } = string.Empty;

    /// <summary>艺术家（逗号分隔）</summary>
    [NotNull]
    public string Artists { get; set; } = string.Empty;

    /// <summary>播放时间</summary>
    [NotNull]
    public DateTime PlayedAt { get; set; }

    /// <summary>所属会话 ID（用于按会话查询历史）</summary>
    [NotNull, Indexed]
    public string SessionId { get; set; } = string.Empty;
}
