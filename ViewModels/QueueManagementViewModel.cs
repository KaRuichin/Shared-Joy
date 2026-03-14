using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared_Joy.Models;
using Shared_Joy.Services;

namespace Shared_Joy.ViewModels;

/// <summary>
/// 队列管理 ViewModel —— 主持人手动调整/移除/锁定队列
/// </summary>
public partial class QueueManagementViewModel : ObservableObject
{
    private readonly IVotingEngine _votingEngine;

    public QueueManagementViewModel(IVotingEngine votingEngine)
    {
        _votingEngine = votingEngine;
    }

    /// <summary>投票队列</summary>
    [ObservableProperty]
    private List<VoteItem> _voteQueue = [];

    /// <summary>锁定的下一首歌曲</summary>
    [ObservableProperty]
    private VoteItem? _lockedNextTrack;

    /// <summary>刷新队列</summary>
    [RelayCommand]
    private void RefreshQueue()
    {
        // TODO: Phase 9 实现队列刷新
    }

    /// <summary>置顶歌曲</summary>
    [RelayCommand]
    private void MoveToTop(string trackId)
    {
        // TODO: Phase 9 实现置顶
    }

    /// <summary>移除歌曲</summary>
    [RelayCommand]
    private void RemoveTrack(string trackId)
    {
        // TODO: Phase 9 实现移除
    }

    /// <summary>锁定下一首</summary>
    [RelayCommand]
    private void LockNextTrack(string trackId)
    {
        // TODO: Phase 9 实现锁定
    }

    /// <summary>清空待投票队列</summary>
    [RelayCommand]
    private void ClearQueue()
    {
        // TODO: Phase 9 实现清空
    }
}
