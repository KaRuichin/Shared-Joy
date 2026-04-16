using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared_Joy.Models;
using Shared_Joy.Services;

namespace Shared_Joy.ViewModels;

/// <summary>
/// 队列管理 ViewModel —— 主持人手动调整/移除/锁定队列
///
/// 支持以下操作：
/// 1. 实时刷新队列（从投票引擎拉取最新排序）
/// 2. 置顶指定歌曲（通过手动调整投票权重或重排）
/// 3. 移除指定歌曲（从投票池删除）
/// 4. 锁定下一首（绕过票数排序，强制指定歌曲先入Spotify队列）
/// 5. 清空整个队列
/// 6. 显示播放历史（从 SQLite 数据库查询）
///
/// 与 QueueSyncService 的交互：
/// - 当设置了 LockedNextTrack 时，QueueSyncService 优先推送该歌曲
/// - 移除/清空操作直接作用于投票引擎，QueueSyncService 下次同步时会感知变化
/// </summary>
public partial class QueueManagementViewModel : ObservableObject
{
    private readonly IVotingEngine _votingEngine;
    private readonly ISessionManager _sessionManager;
    private readonly IDatabaseService _database;

    // 队列刷新定时器（页面显示时启动，用于实时更新）
    private IDispatcherTimer? _queueRefreshTimer;

    public QueueManagementViewModel(IVotingEngine votingEngine, ISessionManager sessionManager, IDatabaseService database)
    {
        _votingEngine = votingEngine;
        _sessionManager = sessionManager;
        _database = database;
    }

    #region 可观察属性

    /// <summary>当前投票队列</summary>
    [ObservableProperty]
    private List<VoteItem> _voteQueue = [];

    /// <summary>锁定的下一首歌曲（优先于票数排序推送）</summary>
    [ObservableProperty]
    private VoteItem? _lockedNextTrack;

    /// <summary>播放历史记录列表（最新的歌曲在前）</summary>
    [ObservableProperty]
    private List<PlayHistory> _playHistoryList = [];

    #endregion

    #region 生命周期（由页面调用）

    /// <summary>页面出现时启动队列刷新定时器，并加载播放历史</summary>
    public void OnPageAppearing()
    {
        if (_queueRefreshTimer is null)
        {
            _queueRefreshTimer = Application.Current!.Dispatcher.CreateTimer();
            _queueRefreshTimer.Interval = TimeSpan.FromSeconds(2);
            _queueRefreshTimer.Tick += (s, e) => RefreshQueueInternal();
        }
        _queueRefreshTimer.Start();
        RefreshQueueInternal();

        // 加载播放历史
        _ = LoadPlayHistoryAsync();
    }

    /// <summary>页面消失时停止队列刷新定时器</summary>
    public void OnPageDisappearing()
    {
        _queueRefreshTimer?.Stop();
    }

    #endregion

    #region 队列操作命令

    /// <summary>
    /// 加载当前会话的播放历史
    /// </summary>
    private async Task LoadPlayHistoryAsync()
    {
        try
        {
            var sessionId = _sessionManager.SessionId;
            if (string.IsNullOrEmpty(sessionId))
            {
                PlayHistoryList = [];
                return;
            }

            var history = await _database.GetPlayHistoryAsync(sessionId);
            PlayHistoryList = history;

            System.Diagnostics.Debug.WriteLine($"[QueueManagement] 加载播放历史: {history.Count} 条记录");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QueueManagement] 加载历史异常: {ex.Message}");
            PlayHistoryList = [];
        }
    }

    /// <summary>
    /// 刷新队列 —— 从投票引擎拉取最新排序并更新 UI
    /// 由定时器自动触发或手动点击"Refresh"按钮触发
    /// </summary>
    [RelayCommand]
    private void RefreshQueue()
    {
        RefreshQueueInternal();
    }

    /// <summary>内部刷新实现</summary>
    private void RefreshQueueInternal()
    {
        if (!_sessionManager.IsSessionActive)
        {
            VoteQueue = [];
            return;
        }

        VoteQueue = _votingEngine.GetRankedQueue();
    }

    /// <summary>
    /// 置顶歌曲 —— 通过临时锁定该歌曲使其成为下一首
    /// 
    /// 实现方案：将指定歌曲设为 LockedNextTrack，QueueSyncService 会优先推送它
    /// 推送后自动清除锁定状态
    /// </summary>
    [RelayCommand]
    private void MoveToTop(string trackId)
    {
        // 在当前队列中查找该歌曲
        var track = VoteQueue.FirstOrDefault(v => v.Track.Id == trackId);
        if (track is null)
            return;

        // 设置为锁定下一首（这将覆盖票数排序）
        LockedNextTrack = track;
        System.Diagnostics.Debug.WriteLine($"[QueueMgmt] 已锁定下一首: {track.Track.Name}");
    }

    /// <summary>
    /// 移除歌曲 —— 从投票池删除指定歌曲（所有人的投票）
    /// 
    /// 实现方案：调用 VotingEngine.RemoveTrack()，同时清除该歌曲的锁定状态（如果有的话）
    /// </summary>
    [RelayCommand]
    private void RemoveTrack(string trackId)
    {
        var success = _votingEngine.RemoveTrack(trackId);
        if (!success)
        {
            System.Diagnostics.Debug.WriteLine($"[QueueMgmt] 移除失败（不存在）: {trackId}");
            return;
        }

        // 如果移除的歌曲正好是锁定的，清除锁定状态
        if (LockedNextTrack?.Track.Id == trackId)
        {
            LockedNextTrack = null;
        }

        // 刷新队列显示
        RefreshQueueInternal();
        System.Diagnostics.Debug.WriteLine($"[QueueMgmt] 已移除歌曲: {trackId}");
    }

    /// <summary>
    /// 锁定下一首 —— 显式设置需要立即推送的歌曲
    /// 
    /// 实现方案：设置 LockedNextTrack，QueueSyncService 的下一轮同步会优先处理它
    /// UI 通过另外的按钮（如长按或菜单）调用此方法
    /// </summary>
    [RelayCommand]
    private void LockNextTrack(string trackId)
    {
        var track = VoteQueue.FirstOrDefault(v => v.Track.Id == trackId);
        if (track is null)
            return;

        LockedNextTrack = track;
        System.Diagnostics.Debug.WriteLine($"[QueueMgmt] 已手动锁定: {track.Track.Name}");
    }

    /// <summary>
    /// 清空待投票队列 —— 删除所有投票项（但保留播放历史记录）
    /// 
    /// 实现方案：调用 VotingEngine.Clear()
    /// </summary>
    [RelayCommand]
    private void ClearQueue()
    {
        _votingEngine.Clear();
        LockedNextTrack = null;
        VoteQueue = [];

        System.Diagnostics.Debug.WriteLine("[QueueMgmt] 队列已清空");
    }

    #endregion

    /// <summary>
    /// 为 LockedNextTrack 提供公开访问（供 QueueSyncService 读取）
    /// </summary>
    public VoteItem? GetLockedNextTrack()
    {
        return LockedNextTrack;
    }

    /// <summary>
    /// 清除锁定状态（供 QueueSyncService 调用，在推送后清除）
    /// </summary>
    public void ClearLockedTrack()
    {
        LockedNextTrack = null;
    }
}
