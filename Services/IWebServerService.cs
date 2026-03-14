namespace Shared_Joy.Services;

/// <summary>
/// 嵌入式 Web 服务器接口
/// </summary>
public interface IWebServerService
{
    /// <summary>服务器是否正在运行</summary>
    bool IsRunning { get; }

    /// <summary>当前监听端口</summary>
    int Port { get; }

    /// <summary>启动服务器</summary>
    Task StartAsync(int port = 8080);

    /// <summary>停止服务器</summary>
    Task StopAsync();
}
