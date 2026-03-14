using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared_Joy.Services;

namespace Shared_Joy.ViewModels;

/// <summary>
/// 设置 ViewModel —— Spotify 配置 + 应用设置
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISpotifyAuthService _authService;

    public SettingsViewModel(ISpotifyAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Spotify Client ID</summary>
    [ObservableProperty]
    private string _clientId = string.Empty;

    /// <summary>是否已登录 Spotify</summary>
    [ObservableProperty]
    private bool _isLoggedIn;

    /// <summary>Spotify 用户名</summary>
    [ObservableProperty]
    private string _userName = string.Empty;

    /// <summary>保存 Client ID</summary>
    [RelayCommand]
    private void SaveClientId()
    {
        // TODO: Phase 2 实现保存到 Preferences
    }

    /// <summary>登录 Spotify</summary>
    [RelayCommand]
    private async Task LoginAsync()
    {
        // TODO: Phase 2 实现 OAuth 认证
        await Task.CompletedTask;
    }

    /// <summary>注销 Spotify</summary>
    [RelayCommand]
    private async Task LogoutAsync()
    {
        // TODO: Phase 2 实现注销
        await Task.CompletedTask;
    }
}
