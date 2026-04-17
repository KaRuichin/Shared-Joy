using Shared_Joy.Models;

namespace Shared_Joy.Services;

/// <summary>
/// 投票引擎接口 —— 管理歌曲投票逻辑（排序/去重/同步）
/// </summary>
public interface IVotingEngine
{
    /// <summary>某首歌曲首次被投票（新点歌）时触发</summary>
    event EventHandler<SpotifyTrack> NewTrackAdded;

    /// <summary>为歌曲投票</summary>
    bool Vote(string guestId, SpotifyTrack track);

    /// <summary>取消投票</summary>
    bool Unvote(string guestId, string trackId);

    /// <summary>获取按票数排序的投票队列</summary>
    List<VoteItem> GetRankedQueue();

    /// <summary>移除指定歌曲</summary>
    bool RemoveTrack(string trackId);

    /// <summary>清空所有投票</summary>
    void Clear();
}
