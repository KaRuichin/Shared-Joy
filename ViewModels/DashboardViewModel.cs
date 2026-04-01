using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared_Joy.Models;
using Shared_Joy.Services;

namespace Shared_Joy.ViewModels;

/// <summary>
/// 主面板 ViewModel —— 当前播放/QR码/PIN/投票队列/会话控制
/// 
/// 播放状态通过定时轮询（3 秒）从 Spotify API 获取，
/// 进度条在两次轮询之间通过本地计时器（每 500ms）平滑更新。
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly ISessionManager _sessionManager;
    private readonly IVotingEngine _votingEngine;
    private readonly ISpotifyApiService _spotifyApi;
    private readonly ISpotifyAuthService _spotifyAuth;
    private readonly IWebServerService _webServer;

    // 播放状态轮询定时器（每 3 秒从 Spotify API 拉取）
    private IDispatcherTimer? _playbackPollTimer;
    // 进度条平滑更新定时器（每 500ms 本地递增）
    private IDispatcherTimer? _progressTimer;
    // 上次从 API 获取进度的时间戳（用于本地插值计算）
    private DateTime _lastProgressFetchTime;

    public DashboardViewModel(
        ISessionManager sessionManager,
        IVotingEngine votingEngine,
        ISpotifyApiService spotifyApi,
        ISpotifyAuthService spotifyAuth,
        IWebServerService webServer)
    {
        _sessionManager = sessionManager;
        _votingEngine = votingEngine;
        _spotifyApi = spotifyApi;
        _spotifyAuth = spotifyAuth;
        _webServer = webServer;
    }

    #region 可观察属性

    /// <summary>当前播放状态（原始数据）</summary>
    [ObservableProperty]
    private PlaybackState? _currentPlayback;

    /// <summary>当前歌曲名称</summary>
    [ObservableProperty]
    private string _trackName = string.Empty;

    /// <summary>当前艺术家</summary>
    [ObservableProperty]
    private string _trackArtists = string.Empty;

    /// <summary>当前专辑名称</summary>
    [ObservableProperty]
    private string _albumName = string.Empty;

    /// <summary>当前专辑封面 URL</summary>
    [ObservableProperty]
    private string _albumImageUrl = string.Empty;

    /// <summary>是否正在播放</summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>是否有歌曲在播放（控制 UI 可见性）</summary>
    [ObservableProperty]
    private bool _hasTrack;

    /// <summary>播放进度（毫秒）</summary>
    [ObservableProperty]
    private double _progressMs;

    /// <summary>歌曲总时长（毫秒）</summary>
    [ObservableProperty]
    private double _durationMs;

    /// <summary>播放进度比例（0.0 ~ 1.0，用于 ProgressBar 绑定）</summary>
    [ObservableProperty]
    private double _progressRatio;

    /// <summary>播放进度文本（如 "1:23 / 3:45"）</summary>
    [ObservableProperty]
    private string _progressText = "0:00 / 0:00";

    /// <summary>播放设备名称</summary>
    [ObservableProperty]
    private string _deviceName = string.Empty;

    /// <summary>播放/暂停按钮文本</summary>
    [ObservableProperty]
    private string _playPauseButtonText = "▶ Play";

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

    #endregion

    #region 生命周期

    /// <summary>
    /// 页面出现时启动播放状态轮询
    /// 由 DashboardPage.OnAppearing 调用
    /// </summary>
    public void StartPolling()
    {
        // 播放状态轮询（每 3 秒）
        if (_playbackPollTimer is null)
        {
            _playbackPollTimer = Application.Current!.Dispatcher.CreateTimer();
            _playbackPollTimer.Interval = TimeSpan.FromSeconds(3);
            _playbackPollTimer.Tick += async (s, e) => await PollPlaybackStateAsync();
        }
        _playbackPollTimer.Start();

        // 进度条平滑更新（每 500ms）
        if (_progressTimer is null)
        {
            _progressTimer = Application.Current!.Dispatcher.CreateTimer();
            _progressTimer.Interval = TimeSpan.FromMilliseconds(500);
            _progressTimer.Tick += (s, e) => UpdateProgressLocally();
        }
        _progressTimer.Start();

        // 立即拉取一次
        _ = PollPlaybackStateAsync();
    }

    /// <summary>
    /// 页面消失时停止轮询
    /// 由 DashboardPage.OnDisappearing 调用
    /// </summary>
    public void StopPolling()
    {
        _playbackPollTimer?.Stop();
        _progressTimer?.Stop();
    }

    #endregion

    #region 播放状态轮询

    /// <summary>
    /// 从 Spotify API 拉取当前播放状态并更新 UI 属性
    /// </summary>
    private async Task PollPlaybackStateAsync()
    {
        if (!_spotifyAuth.IsAuthenticated)
            return;

        try
        {
            var playback = await _spotifyApi.GetCurrentPlaybackAsync();
            CurrentPlayback = playback;

            if (playback?.CurrentTrack is not null)
            {
                var track = playback.CurrentTrack;
                TrackName = track.Name;
                TrackArtists = track.Artists;
                AlbumName = track.AlbumName;
                AlbumImageUrl = track.AlbumImageUrl;
                IsPlaying = playback.IsPlaying;
                HasTrack = true;
                DurationMs = track.DurationMs;
                ProgressMs = playback.ProgressMs;
                DeviceName = playback.DeviceName;
                _lastProgressFetchTime = DateTime.UtcNow;

                UpdateProgressDisplay();
            }
            else
            {
                // 无播放内容
                HasTrack = false;
                TrackName = string.Empty;
                TrackArtists = string.Empty;
                AlbumName = string.Empty;
                AlbumImageUrl = string.Empty;
                IsPlaying = false;
                ProgressMs = 0;
                DurationMs = 0;
                DeviceName = string.Empty;
                UpdateProgressDisplay();
            }

            PlayPauseButtonText = IsPlaying ? "⏸ Pause" : "▶ Play";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] 轮询播放状态异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 在两次 API 轮询之间，本地递增进度（仅在播放中时）
    /// 使进度条平滑移动，而非每 3 秒跳一次
    /// </summary>
    private void UpdateProgressLocally()
    {
        if (!IsPlaying || !HasTrack || DurationMs <= 0)
            return;

        // 根据上次 API 返回的进度 + 本地经过的时间计算当前进度
        var elapsed = (DateTime.UtcNow - _lastProgressFetchTime).TotalMilliseconds;
        var estimatedProgress = ProgressMs + elapsed;

        // 不超过总时长
        if (estimatedProgress > DurationMs)
            estimatedProgress = DurationMs;

        // 仅更新显示，不修改 ProgressMs（避免与 API 数据冲突）
        ProgressRatio = DurationMs > 0 ? estimatedProgress / DurationMs : 0;
        ProgressText = $"{FormatTime((int)estimatedProgress)} / {FormatTime((int)DurationMs)}";
    }

    /// <summary>
    /// 根据当前 ProgressMs 和 DurationMs 更新进度显示
    /// </summary>
    private void UpdateProgressDisplay()
    {
        ProgressRatio = DurationMs > 0 ? ProgressMs / DurationMs : 0;
        ProgressText = $"{FormatTime((int)ProgressMs)} / {FormatTime((int)DurationMs)}";
    }

    /// <summary>
    /// 将毫秒格式化为 "m:ss" 格式
    /// </summary>
    private static string FormatTime(int ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
    }

    #endregion

    #region 播放控制命令

    /// <summary>播放/暂停切换</summary>
    [RelayCommand]
    private async Task TogglePlaybackAsync()
    {
        try
        {
            bool success;
            if (IsPlaying)
            {
                success = await _spotifyApi.PauseAsync();
                if (success) IsPlaying = false;
            }
            else
            {
                success = await _spotifyApi.PlayAsync();
                if (success) IsPlaying = true;
            }

            PlayPauseButtonText = IsPlaying ? "⏸ Pause" : "▶ Play";

            // 立即刷新播放状态
            if (success)
                await PollPlaybackStateAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] 播放控制异常: {ex.Message}");
        }
    }

    /// <summary>跳到下一首</summary>
    [RelayCommand]
    private async Task SkipNextAsync()
    {
        try
        {
            var success = await _spotifyApi.SkipNextAsync();
            if (success)
            {
                // 短暂延迟后刷新，让 Spotify 有时间切换歌曲
                await Task.Delay(500);
                await PollPlaybackStateAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] 跳过异常: {ex.Message}");
        }
    }

    #endregion

    #region 会话控制

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

    #endregion
}
