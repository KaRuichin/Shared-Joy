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

    // Preferences 键名（与 SpotifyAuthService 保持一致）
    private const string KeyClientId = "spotify_client_id";

    public SettingsViewModel(ISpotifyAuthService authService)
    {
        _authService = authService;

        // 监听认证状态变更
        _authService.AuthenticationChanged += OnAuthenticationChanged;

        // 从 Preferences 恢复 Client ID
        _clientId = Preferences.Get(KeyClientId, string.Empty);
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

    /// <summary>状态消息（用于显示操作反馈）</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>是否正在执行操作（防止重复点击）</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Client ID 是否已保存</summary>
    [ObservableProperty]
    private bool _isClientIdSaved;

    /// <summary>
    /// 页面出现时初始化状态
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        // 尝试恢复已保存的令牌
        var restored = await _authService.TryRestoreTokenAsync();
        if (restored)
        {
            IsLoggedIn = true;
            await LoadUserInfoAsync();
        }

        IsClientIdSaved = !string.IsNullOrWhiteSpace(Preferences.Get(KeyClientId, string.Empty));
    }

    /// <summary>保存 Client ID 到 Preferences</summary>
    [RelayCommand]
    private async Task SaveClientIdAsync()
    {
        var trimmedId = ClientId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedId))
        {
            StatusMessage = "⚠️ Please enter a valid Client ID";
            return;
        }

        Preferences.Set(KeyClientId, trimmedId);
        ClientId = trimmedId;
        IsClientIdSaved = true;
        StatusMessage = "✅ Client ID saved";

        System.Diagnostics.Debug.WriteLine($"[Settings] Client ID 已保存: {trimmedId[..Math.Min(8, trimmedId.Length)]}...");

        await Task.CompletedTask;
    }

    /// <summary>登录 Spotify（启动 PKCE 认证流程）</summary>
    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;

        // 检查 Client ID 是否已配置
        var savedClientId = Preferences.Get(KeyClientId, string.Empty);
        if (string.IsNullOrWhiteSpace(savedClientId))
        {
            StatusMessage = "⚠️ Please save your Client ID first";
            return;
        }

        IsBusy = true;
        StatusMessage = "Connecting to Spotify...";

        try
        {
            var success = await _authService.AuthenticateAsync();

            if (success)
            {
                IsLoggedIn = true;
                StatusMessage = "✅ Successfully signed in!";
                await LoadUserInfoAsync();
            }
            else
            {
                StatusMessage = "❌ Sign in failed or was cancelled";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[Settings] 登录异常: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>注销 Spotify</summary>
    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (IsBusy) return;

        IsBusy = true;

        try
        {
            await _authService.LogoutAsync();
            IsLoggedIn = false;
            UserName = string.Empty;
            StatusMessage = "Signed out";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 加载 Spotify 用户信息
    /// </summary>
    private async Task LoadUserInfoAsync()
    {
        try
        {
            var displayName = await _authService.GetUserDisplayNameAsync();
            UserName = displayName ?? "Unknown User";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] 获取用户信息异常: {ex.Message}");
            UserName = "Unknown User";
        }
    }

    /// <summary>
    /// 认证状态变更回调
    /// </summary>
    private void OnAuthenticationChanged(object? sender, bool isAuthenticated)
    {
        // 确保在 UI 线程更新
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsLoggedIn = isAuthenticated;
            if (!isAuthenticated)
            {
                UserName = string.Empty;
            }
        });
    }
}
