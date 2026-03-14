namespace Shared_Joy.Models;

/// <summary>
/// 投票项（歌曲 + 票数 + 时间戳）
/// </summary>
public class VoteItem
{
    /// <summary>关联的歌曲信息</summary>
    public SpotifyTrack Track { get; set; } = new();

    /// <summary>当前票数</summary>
    public int VoteCount { get; set; }

    /// <summary>首次获得投票的时间（用于同票数排序）</summary>
    public DateTime FirstVotedAt { get; set; }

    /// <summary>投票的访客 ID 集合</summary>
    public HashSet<string> VoterIds { get; set; } = [];
}
