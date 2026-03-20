namespace Shared_Joy.Services;

/// <summary>
/// Spotify OAuth 2.0 PKCE 认证服务接口
/// </summary>
public interface ISpotifyAuthService
{
    /// <summary>是否已认证</summary>
    bool IsAuthenticated { get; }

    /// <summary>认证状态变更事件</summary>
    event EventHandler<bool>? AuthenticationChanged;

    /// <summary>获取当前有效的访问令牌（自动刷新过期令牌）</summary>
    Task<string?> GetAccessTokenAsync();

    /// <summary>启动 OAuth 2.0 PKCE 认证流程</summary>
    Task<bool> AuthenticateAsync();

    /// <summary>注销并清除令牌</summary>
    Task LogoutAsync();

    /// <summary>尝试从存储中恢复令牌</summary>
    Task<bool> TryRestoreTokenAsync();

    /// <summary>获取当前用户显示名称</summary>
    Task<string?> GetUserDisplayNameAsync();

    /// <summary>获取当前用户头像 URL</summary>
    Task<string?> GetUserAvatarUrlAsync();
}
