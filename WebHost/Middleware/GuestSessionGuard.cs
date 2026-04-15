using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using Shared_Joy.Services;

namespace Shared_Joy.WebHost.Middleware;

/// <summary>
/// 访客会话校验中间件
/// —— 除 /api/auth 外的所有 /api/ 请求需验证访客会话令牌（Authorization header）
/// </summary>
public class GuestSessionGuard : IConcern
{
    private readonly ISessionManager _sessionManager;

    public IHandler Content { get; }

    public GuestSessionGuard(IHandler content, ISessionManager sessionManager)
    {
        Content = content;
        _sessionManager = sessionManager;
    }

    public ValueTask PrepareAsync() => Content.PrepareAsync();

    public async ValueTask<IResponse?> HandleAsync(IRequest request)
    {
        var path = request.Target.Current?.Value ?? string.Empty;

        // /api/auth 不需要会话验证（访客尚未登录）
        // 非 /api/ 路径也不需要验证（静态文件）
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
        {
            return await Content.HandleAsync(request);
        }

        // 会话未启动时拒绝所有 API 请求
        if (!_sessionManager.IsSessionActive)
        {
            return request.Respond()
                .Status(ResponseStatus.ServiceUnavailable)
                .Build();
        }

        // 从 Authorization header 提取令牌: "Bearer {token}"
        var authHeader = request.Headers.TryGetValue("Authorization", out var auth) ? auth : null;

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return request.Respond()
                .Status(ResponseStatus.Unauthorized)
                .Build();
        }

        var token = authHeader["Bearer ".Length..];

        if (!_sessionManager.ValidateGuestToken(token))
        {
            return request.Respond()
                .Status(ResponseStatus.Unauthorized)
                .Build();
        }

        return await Content.HandleAsync(request);
    }
}

/// <summary>
/// GuestSessionGuard 的 GenHTTP ConcernBuilder，持有 ISessionManager 引用
/// </summary>
public class GuestSessionGuardBuilder : IConcernBuilder
{
    private readonly ISessionManager _sessionManager;

    public GuestSessionGuardBuilder(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public IConcern Build(IHandler content) => new GuestSessionGuard(content, _sessionManager);
}
