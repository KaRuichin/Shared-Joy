using SQLite;

namespace Shared_Joy.Models;

/// <summary>
/// 投票记录（SQLite 表实体）
/// 持久化每次投票操作，用于防重复投票和历史审计
/// </summary>
[Table("VoteRecord")]
public class VoteRecord
{
    /// <summary>自增主键</summary>
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Spotify 歌曲 ID</summary>
    [NotNull, Indexed]
    public string TrackId { get; set; } = string.Empty;

    /// <summary>投票访客 ID</summary>
    [NotNull, Indexed]
    public string GuestId { get; set; } = string.Empty;

    /// <summary>所属会话 ID</summary>
    [NotNull, Indexed]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>投票时间</summary>
    [NotNull]
    public DateTime VotedAt { get; set; }
}
