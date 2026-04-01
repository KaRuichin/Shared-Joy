using Shared_Joy.Models;

namespace Shared_Joy.Services;

/// <summary>
/// SQLite 数据库访问服务接口
/// </summary>
public interface IDatabaseService
{
    /// <summary>初始化数据库连接和表结构</summary>
    Task InitializeAsync();

    // ── 播放历史 ──

    /// <summary>记录播放历史</summary>
    Task RecordPlayHistoryAsync(string trackId, string trackName, string artists, string sessionId);

    /// <summary>获取指定会话的播放历史（按播放时间降序）</summary>
    Task<List<PlayHistory>> GetPlayHistoryAsync(string sessionId);

    // ── 投票记录 ──

    /// <summary>记录投票</summary>
    Task RecordVoteAsync(string trackId, string guestId, string sessionId);

    /// <summary>删除投票记录</summary>
    Task RemoveVoteAsync(string trackId, string guestId, string sessionId);

    /// <summary>清除当前会话的所有投票记录</summary>
    Task ClearVotesAsync(string sessionId);

    /// <summary>获取指定会话的所有投票记录</summary>
    Task<List<VoteRecord>> GetVoteRecordsAsync(string sessionId);

    /// <summary>检查指定访客是否已对某歌曲投票</summary>
    Task<bool> HasVotedAsync(string trackId, string guestId, string sessionId);
}
