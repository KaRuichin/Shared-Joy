using System.Collections.Concurrent;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;

namespace Shared_Joy.WebHost.Middleware;

/// <summary>
/// PIN 与搜索限流中间件
/// —— 防暴力破解 PIN 码（每 IP 最多 5 次/分钟）+ 搜索 API 频率限制（每 IP 最多 30 次/分钟）
/// </summary>
public class RateLimitGuard : IConcern
{
    // IP → (时间窗口起点, 请求计数)
    private readonly ConcurrentDictionary<string, (DateTime WindowStart, int Count)> _authAttempts = new();
    private readonly ConcurrentDictionary<string, (DateTime WindowStart, int Count)> _searchAttempts = new();

    private const int AuthMaxAttempts = 5;
    private const int SearchMaxAttempts = 30;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public IHandler Content { get; }

    public RateLimitGuard(IHandler content)
    {
        Content = content;
    }

    public ValueTask PrepareAsync() => Content.PrepareAsync();

    public async ValueTask<IResponse?> HandleAsync(IRequest request)
    {
        var clientIp = request.Client.IPAddress?.ToString() ?? "unknown";
        var path = request.Target.Path.ToString();

        // 对 /api/auth 路径进行 PIN 暴力破解保护
        if (path.Contains("/api/auth", StringComparison.OrdinalIgnoreCase) &&
            request.Method.KnownMethod == RequestMethod.Post)
        {
            if (IsRateLimited(_authAttempts, clientIp, AuthMaxAttempts))
            {
                return request.Respond()
                    .Status(ResponseStatus.TooManyRequests)
                    .Header("Retry-After", "60")
                    .Build();
            }
        }

        // 对 /api/search 路径进行搜索频率限制
        if (path.Contains("/api/search", StringComparison.OrdinalIgnoreCase))
        {
            if (IsRateLimited(_searchAttempts, clientIp, SearchMaxAttempts))
            {
                return request.Respond()
                    .Status(ResponseStatus.TooManyRequests)
                    .Header("Retry-After", "60")
                    .Build();
            }
        }

        return await Content.HandleAsync(request);
    }

    private static bool IsRateLimited(
        ConcurrentDictionary<string, (DateTime WindowStart, int Count)> store,
        string key, int maxAttempts)
    {
        var now = DateTime.UtcNow;

        var entry = store.AddOrUpdate(key,
            _ => (now, 1),
            (_, existing) =>
            {
                // 窗口过期则重置
                if (now - existing.WindowStart > Window)
                    return (now, 1);
                return (existing.WindowStart, existing.Count + 1);
            });

        return entry.Count > maxAttempts;
    }
}

/// <summary>
/// RateLimitGuard 的 GenHTTP ConcernBuilder
/// </summary>
public class RateLimitGuardBuilder : IConcernBuilder
{
    public IConcern Build(IHandler content) => new RateLimitGuard(content);
}
