namespace Shared_Joy.Services;

/// <summary>
/// GenHTTP 嵌入式 Web 服务器实现
/// </summary>
public class WebServerService : IWebServerService
{
    private readonly IStaticWebAssetService _assetService;

    public WebServerService(IStaticWebAssetService assetService)
    {
        _assetService = assetService;
    }

    public bool IsRunning { get; private set; }

    public int Port { get; private set; }

    public Task StartAsync(int port = 8080)
    {
        // TODO: Phase 6 实现 GenHTTP 服务器启动
        throw new NotImplementedException();
    }

    public Task StopAsync()
    {
        // TODO: Phase 6 实现服务器停止
        throw new NotImplementedException();
    }
}
