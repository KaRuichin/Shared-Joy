using System.Collections.Concurrent;
using Shared_Joy.Models;

namespace Shared_Joy.Services;

/// <summary>
/// 投票引擎实现 —— 线程安全的歌曲投票逻辑
/// </summary>
public class VotingEngine : IVotingEngine
{
    // 内存中维护投票数据（trackId → VoteItem）
    private readonly ConcurrentDictionary<string, VoteItem> _votes = new();

    public bool Vote(string guestId, SpotifyTrack track)
    {
        // TODO: Phase 5 实现投票逻辑
        throw new NotImplementedException();
    }

    public bool Unvote(string guestId, string trackId)
    {
        // TODO: Phase 5 实现取消投票
        throw new NotImplementedException();
    }

    public List<VoteItem> GetRankedQueue()
    {
        // TODO: Phase 5 实现排序返回
        throw new NotImplementedException();
    }

    public bool RemoveTrack(string trackId)
    {
        // TODO: Phase 5 实现移除歌曲
        throw new NotImplementedException();
    }

    public void Clear()
    {
        _votes.Clear();
    }
}
