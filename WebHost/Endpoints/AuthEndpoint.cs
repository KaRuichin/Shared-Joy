using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Webservices;
using Shared_Joy.Services;

namespace Shared_Joy.WebHost.Endpoints;

/// <summary>
/// PIN 验证 API 端点
/// POST /api/auth — 验证 PIN 码，注册访客并返回会话令牌
/// </summary>
public class AuthEndpoint
{
    private readonly ISessionManager _sessionManager;

    public AuthEndpoint(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// POST /api/auth
    /// 请求体: { "pin": "123456" }
    /// 成功: { "success": true, "token": "...", "guestId": "..." }
    /// 失败: { "success": false, "error": "Invalid PIN" }
    /// </summary>
    [ResourceMethod(RequestMethod.Post)]
    public AuthResponse Authenticate(AuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Pin))
        {
            return new AuthResponse { Success = false, Error = "PIN is required" };
        }

        if (!_sessionManager.ValidatePin(request.Pin))
        {
            return new AuthResponse { Success = false, Error = "Invalid PIN" };
        }

        // 生成唯一访客标识并注册
        var guestId = $"guest_{Guid.NewGuid():N}";
        var guest = _sessionManager.RegisterGuest(guestId);

        return new AuthResponse
        {
            Success = true,
            Token = guest.SessionToken,
            GuestId = guest.GuestId
        };
    }
}

public class AuthRequest
{
    public string Pin { get; set; } = string.Empty;
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? GuestId { get; set; }
    public string? Error { get; set; }
}
