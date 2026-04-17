using System.Collections.Concurrent;
using Shared_Joy.Models;

namespace Shared_Joy.Services;

/// <summary>
/// 投票引擎实现 —— 线程安全的歌曲投票逻辑
/// 使用 ConcurrentDictionary + lock 保证多访客并发投票的数据一致性
/// </summary>
public class VotingEngine : IVotingEngine
{
    // 内存中维护投票数据（trackId → VoteItem）
    private readonly ConcurrentDictionary<string, VoteItem> _votes = new();

    // VoteItem 内部的 VoterIds（HashSet）非线程安全，需用锁保护修改
    private readonly object _voteLock = new();

    /// <summary>某首歌曲首次被投票（新点歌）时触发</summary>
    public event EventHandler<SpotifyTrack>? NewTrackAdded;

    public bool Vote(string guestId, SpotifyTrack track)
    {
        bool isNewTrack = false;

        lock (_voteLock)
        {
            var item = _votes.GetOrAdd(track.Id, _ =>
            {
                isNewTrack = true;
                return new VoteItem
                {
                    Track = track,
                    VoteCount = 0,
                    FirstVotedAt = DateTime.UtcNow,
                    VoterIds = []
                };
            });

            // 同一访客同一歌曲只能投一票
            if (!item.VoterIds.Add(guestId))
                return false;

            item.VoteCount = item.VoterIds.Count;

            System.Diagnostics.Debug.WriteLine(
                $"[Voting] 投票: Guest={guestId}, Track={track.Name}, 当前票数={item.VoteCount}");
        }

        // 锁外触发事件，避免事件处理中的潜在死锁
        if (isNewTrack)
            NewTrackAdded?.Invoke(this, track);

        return true;
    }

    public bool Unvote(string guestId, string trackId)
    {
        lock (_voteLock)
        {
            if (!_votes.TryGetValue(trackId, out var item))
                return false;

            if (!item.VoterIds.Remove(guestId))
                return false;

            item.VoteCount = item.VoterIds.Count;

            // 票数归零时移除该歌曲
            if (item.VoteCount == 0)
                _votes.TryRemove(trackId, out _);

            System.Diagnostics.Debug.WriteLine(
                $"[Voting] 取消投票: Guest={guestId}, TrackId={trackId}, 剩余票数={item.VoteCount}");
            return true;
        }
    }

    public List<VoteItem> GetRankedQueue()
    {
        // 快照当前值，按票数降序、同票按首次投票时间升序
        return _votes.Values
            .OrderByDescending(v => v.VoteCount)
            .ThenBy(v => v.FirstVotedAt)
            .ToList();
    }

    public bool RemoveTrack(string trackId)
    {
        var removed = _votes.TryRemove(trackId, out _);
        if (removed)
            System.Diagnostics.Debug.WriteLine($"[Voting] 移除歌曲: TrackId={trackId}");
        return removed;
    }

    public void Clear()
    {
        _votes.Clear();
        System.Diagnostics.Debug.WriteLine("[Voting] 投票池已清空");
    }
}
