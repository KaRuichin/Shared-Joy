using SQLite;

namespace Shared_Joy.Services;

/// <summary>
/// SQLite 数据库访问服务实现
/// </summary>
public class DatabaseService : IDatabaseService
{
    private SQLiteAsyncConnection? _connection;

    public async Task InitializeAsync()
    {
        if (_connection is not null)
            return;

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "sharedjoy.db");
        _connection = new SQLiteAsyncConnection(dbPath);

        // TODO: Phase 4 创建表结构（PlayHistory, VoteRecord）
    }

    public Task RecordPlayHistoryAsync(string trackId, string trackName, string artists, string sessionId)
    {
        // TODO: Phase 4 实现播放记录
        throw new NotImplementedException();
    }

    public Task RecordVoteAsync(string trackId, string guestId, string sessionId)
    {
        // TODO: Phase 4 实现投票记录
        throw new NotImplementedException();
    }

    public Task RemoveVoteAsync(string trackId, string guestId, string sessionId)
    {
        // TODO: Phase 4 实现删除投票记录
        throw new NotImplementedException();
    }

    public Task ClearVotesAsync(string sessionId)
    {
        // TODO: Phase 4 实现清空投票
        throw new NotImplementedException();
    }
}
