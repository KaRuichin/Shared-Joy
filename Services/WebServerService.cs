using GenHTTP.Api.Infrastructure;
using GenHTTP.Engine.Internal;
using GenHTTP.Modules.IO;
using GenHTTP.Modules.Practices;
using GenHTTP.Modules.Webservices;
using Shared_Joy.WebHost.Endpoints;
using Shared_Joy.WebHost.Middleware;

// 避免与 Microsoft.Maui.Controls.Layout 冲突
using GLayout = GenHTTP.Modules.Layouting.Layout;

namespace Shared_Joy.Services;

/// <summary>
/// GenHTTP 嵌入式 Web 服务器实现
///
/// 路由结构：
///   /api/auth        → AuthEndpoint（PIN 验证）
///   /api/search      → SearchEndpoint（Spotify 搜索代理）
///   /api/vote        → VoteEndpoint（投票/取消投票）
///   /api/queue       → QueueEndpoint（投票排行队列）
///   /api/now-playing → NowPlayingEndpoint（当前播放状态）
///   /               → 静态文件（访客 SPA）
/// </summary>
public class WebServerService : IWebServerService
{
    private readonly IStaticWebAssetService _assetService;
    private readonly ISessionManager _sessionManager;
    private readonly ISpotifyApiService _spotifyApi;
    private readonly IVotingEngine _votingEngine;

    private IServerHost? _host;

    public WebServerService(
        IStaticWebAssetService assetService,
        ISessionManager sessionManager,
        ISpotifyApiService spotifyApi,
        IVotingEngine votingEngine)
    {
        _assetService = assetService;
        _sessionManager = sessionManager;
        _spotifyApi = spotifyApi;
        _votingEngine = votingEngine;
    }

    public bool IsRunning { get; private set; }

    public int Port { get; private set; }

    public async Task StartAsync(int port = 8080)
    {
        if (IsRunning)
            return;

        // 解包静态资源到文件系统
        await _assetService.ExtractAssetsAsync();

        // 手动构建端点实例（注入依赖），避免 AddService<T> 与 MAUI 扩展方法冲突
        var authEndpoint = new AuthEndpoint(_sessionManager);
        var searchEndpoint = new SearchEndpoint(_spotifyApi);
        var voteEndpoint = new VoteEndpoint(_votingEngine);
        var queueEndpoint = new QueueEndpoint(_votingEngine);
        var nowPlayingEndpoint = new NowPlayingEndpoint(_spotifyApi);

        // 构建 API 路由（/api 下的所有端点）
        var apiLayout = GLayout.Create()
            .Add("auth", ServiceResource.From(authEndpoint))
            .Add("search", ServiceResource.From(searchEndpoint))
            .Add("vote", ServiceResource.From(voteEndpoint))
            .Add("queue", ServiceResource.From(queueEndpoint))
            .Add("now-playing", ServiceResource.From(nowPlayingEndpoint));

        // 构建主路由：API + 静态文件
        var rootLayout = GLayout.Create()
            .Add("api", apiLayout)
            .Add(new RateLimitGuardBuilder())
            .Add(new GuestSessionGuardBuilder(_sessionManager));

        // 如果静态资源目录存在，添加静态文件服务
        if (!string.IsNullOrEmpty(_assetService.WebRootPath) &&
            Directory.Exists(_assetService.WebRootPath))
        {
            var tree = ResourceTree.FromDirectory(_assetService.WebRootPath);
            rootLayout.Add(Resources.From(tree));
        }

        // 启动 GenHTTP 服务器
        _host = Host.Create()
            .Handler(rootLayout)
            .Defaults()
            .Port((ushort)port);

        Port = port;
        IsRunning = true;

        await _host.StartAsync();

        System.Diagnostics.Debug.WriteLine($"[WebServer] 服务器已启动, 端口={port}");
    }

    public async Task StopAsync()
    {
        if (!IsRunning || _host is null)
            return;

        await _host.StopAsync();
        IsRunning = false;

        System.Diagnostics.Debug.WriteLine("[WebServer] 服务器已停止");
    }
}
