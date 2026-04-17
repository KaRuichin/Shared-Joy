namespace Shared_Joy.Services;

/// <summary>
/// 系统通知服务接口 —— 在收到新点歌时推送本地通知
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 初始化通知渠道并申请权限（Android 13+ 需在 UI 线程调用）
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 发送本地系统通知
    /// </summary>
    Task SendAsync(string title, string body);
}
