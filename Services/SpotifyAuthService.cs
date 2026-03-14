namespace Shared_Joy.Services;

/// <summary>
/// Spotify OAuth 2.0 PKCE 认证服务实现
/// </summary>
public class SpotifyAuthService : ISpotifyAuthService
{
    public bool IsAuthenticated => false;

    public Task<bool> AuthenticateAsync()
    {
        // TODO: Phase 2 实现 PKCE 认证流程
        throw new NotImplementedException();
    }

    public Task<string?> GetAccessTokenAsync()
    {
        // TODO: Phase 2 实现获取/刷新访问令牌
        throw new NotImplementedException();
    }

    public Task LogoutAsync()
    {
        // TODO: Phase 2 实现注销
        throw new NotImplementedException();
    }

    public Task<bool> TryRestoreTokenAsync()
    {
        // TODO: Phase 2 实现令牌恢复
        throw new NotImplementedException();
    }
}
