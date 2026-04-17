using Shared_Joy.Models;
using SQLite;

namespace Shared_Joy.Services;

/// <summary>
/// SQLite 数据库访问服务实现
/// 
/// 数据库路径：{AppDataDirectory}/sharedjoy.db
/// 表结构：PlayHistory（播放历史）、VoteRecord（投票记录）
/// </summary>
public class DatabaseService : IDatabaseService
{
    private SQLiteAsyncConnection? _connection;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// 初始化数据库连接并创建表结构
    /// 线程安全，可多次调用（幂等）
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_connection is not null)
            return;

        await _initLock.WaitAsync();
        try
        {
            // 双重检查锁定
            if (_connection is not null)
                return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "sharedjoy.db");
            _connection = new SQLiteAsyncConnection(dbPath);

            // 创建表（如已存在则跳过）
            await _connection.CreateTableAsync<PlayHistory>();
            await _connection.CreateTableAsync<VoteRecord>();

            System.Diagnostics.Debug.WriteLine($"[Database] 数据库已初始化: {dbPath}");
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ── 播放历史 ──

    /// <summary>
    /// 记录一首歌曲的播放历史
    /// </summary>
    public async Task RecordPlayHistoryAsync(string trackId, string trackName, string artists, string sessionId)
    {
        var db = await GetConnectionAsync();
        // 插入新记录
        var record = new PlayHistory
        {
            TrackId = trackId,
            TrackName = trackName,
            Artists = artists,
            PlayedAt = DateTime.UtcNow,
            SessionId = sessionId
        };

        await db.InsertAsync(record);
        System.Diagnostics.Debug.WriteLine($"[Database] 记录播放历史: {trackName} - {artists}");
    }

    /// <summary>
    /// 获取指定会话的播放历史（按播放时间降序）
    /// </summary>
    public async Task<List<PlayHistory>> GetPlayHistoryAsync(string sessionId)
    {
        var db = await GetConnectionAsync();

        return await db.Table<PlayHistory>()
            .Where(h => h.SessionId == sessionId)
            .OrderByDescending(h => h.PlayedAt)
            .ToListAsync();
    }

    /// <summary>
    /// 获取全部播放历史（跨会话，按播放时间降序）
    /// </summary>
    public async Task<List<PlayHistory>> GetAllPlayHistoryAsync(int limit = 100)
    {
        var db = await GetConnectionAsync();

        return await db.Table<PlayHistory>()
            .OrderByDescending(h => h.PlayedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// 清除全部播放历史记录
    /// </summary>
    public async Task ClearAllPlayHistoryAsync()
    {
        var db = await GetConnectionAsync();
        var count = await db.ExecuteAsync("DELETE FROM PlayHistory");
        System.Diagnostics.Debug.WriteLine($"[Database] 已清除全部播放历史: {count} 条");
    }

    // ── 投票记录 ──

    /// <summary>
    /// 记录一次投票
    /// </summary>
    public async Task RecordVoteAsync(string trackId, string guestId, string sessionId)
    {
        var db = await GetConnectionAsync();

        var record = new VoteRecord
        {
            TrackId = trackId,
            GuestId = guestId,
            SessionId = sessionId,
            VotedAt = DateTime.UtcNow
        };

        await db.InsertAsync(record);
        System.Diagnostics.Debug.WriteLine($"[Database] 记录投票: Guest={guestId}, Track={trackId}");
    }

    /// <summary>
    /// 删除指定访客对指定歌曲的投票记录
    /// </summary>
    public async Task RemoveVoteAsync(string trackId, string guestId, string sessionId)
    {
        var db = await GetConnectionAsync();

        // 查找匹配的投票记录并删除
        var records = await db.Table<VoteRecord>()
            .Where(v => v.TrackId == trackId && v.GuestId == guestId && v.SessionId == sessionId)
            .ToListAsync();

        foreach (var record in records)
        {
            await db.DeleteAsync(record);
        }

        System.Diagnostics.Debug.WriteLine($"[Database] 删除投票: Guest={guestId}, Track={trackId}, 删除 {records.Count} 条");
    }

    /// <summary>
    /// 清除指定会话的所有投票记录
    /// </summary>
    public async Task ClearVotesAsync(string sessionId)
    {
        var db = await GetConnectionAsync();

        // sqlite-net 不支持批量条件删除，使用 ExecuteAsync 执行原生 SQL
        var count = await db.ExecuteAsync(
            "DELETE FROM VoteRecord WHERE SessionId = ?", sessionId);

        System.Diagnostics.Debug.WriteLine($"[Database] 清空会话 {sessionId} 的投票记录，删除 {count} 条");
    }

    /// <summary>
    /// 获取指定会话的所有投票记录
    /// </summary>
    public async Task<List<VoteRecord>> GetVoteRecordsAsync(string sessionId)
    {
        var db = await GetConnectionAsync();

        return await db.Table<VoteRecord>()
            .Where(v => v.SessionId == sessionId)
            .OrderByDescending(v => v.VotedAt)
            .ToListAsync();
    }

    /// <summary>
    /// 检查指定访客是否已对某歌曲投票
    /// </summary>
    public async Task<bool> HasVotedAsync(string trackId, string guestId, string sessionId)
    {
        var db = await GetConnectionAsync();

        var count = await db.Table<VoteRecord>()
            .Where(v => v.TrackId == trackId && v.GuestId == guestId && v.SessionId == sessionId)
            .CountAsync();

        return count > 0;
    }

    // ── 内部辅助 ──

    /// <summary>
    /// 获取已初始化的数据库连接（自动调用 InitializeAsync）
    /// </summary>
    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is null)
            await InitializeAsync();

        return _connection!;
    }
}
