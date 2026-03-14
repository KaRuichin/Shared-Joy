namespace Shared_Joy.Services;

/// <summary>
/// 队列同步服务接口 —— 定时将高票歌曲推送至 Spotify 播放队列
/// </summary>
public interface IQueueSyncService
{
    /// <summary>是否正在运行</summary>
    bool IsRunning { get; }

    /// <summary>启动后台同步</summary>
    Task StartAsync();

    /// <summary>停止后台同步</summary>
    Task StopAsync();
}
