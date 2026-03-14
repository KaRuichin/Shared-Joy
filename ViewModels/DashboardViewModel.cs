using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared_Joy.Models;
using Shared_Joy.Services;

namespace Shared_Joy.ViewModels;

/// <summary>
/// 主面板 ViewModel —— 当前播放/QR码/PIN/投票队列/会话控制
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly ISessionManager _sessionManager;
    private readonly IVotingEngine _votingEngine;
    private readonly ISpotifyApiService _spotifyApi;
    private readonly IWebServerService _webServer;

    public DashboardViewModel(
        ISessionManager sessionManager,
        IVotingEngine votingEngine,
        ISpotifyApiService spotifyApi,
        IWebServerService webServer)
    {
        _sessionManager = sessionManager;
        _votingEngine = votingEngine;
        _spotifyApi = spotifyApi;
        _webServer = webServer;
    }

    /// <summary>当前播放状态</summary>
    [ObservableProperty]
    private PlaybackState? _currentPlayback;

    /// <summary>QR 码图片</summary>
    [ObservableProperty]
    private ImageSource? _qrCodeImage;

    /// <summary>当前 PIN 码</summary>
    [ObservableProperty]
    private string _pinCode = string.Empty;

    /// <summary>在线访客数量</summary>
    [ObservableProperty]
    private int _guestCount;

    /// <summary>会话是否已启动</summary>
    [ObservableProperty]
    private bool _isSessionActive;

    /// <summary>投票队列</summary>
    [ObservableProperty]
    private List<VoteItem> _voteQueue = [];

    /// <summary>启动会话</summary>
    [RelayCommand]
    private void StartSession()
    {
        // TODO: Phase 8 实现启动会话逻辑
    }

    /// <summary>结束会话</summary>
    [RelayCommand]
    private void EndSession()
    {
        // TODO: Phase 8 实现结束会话逻辑
    }

    /// <summary>播放/暂停切换</summary>
    [RelayCommand]
    private async Task TogglePlaybackAsync()
    {
        // TODO: Phase 8 实现播放控制
        await Task.CompletedTask;
    }

    /// <summary>跳到下一首</summary>
    [RelayCommand]
    private async Task SkipNextAsync()
    {
        // TODO: Phase 8 实现跳过
        await Task.CompletedTask;
    }
}
