using Shared_Joy.Models;

namespace Shared_Joy.Services;

/// <summary>
/// 本地系统通知服务 —— 订阅 IVotingEngine.NewTrackAdded，有新点歌时推送通知
///
/// Android : 通过 NotificationManagerCompat + 通知渠道实现
/// Windows : 通过 WinRT ToastNotificationManager 实现
/// </summary>
public class NotificationService : INotificationService
{
    private static int _notificationId = 1000;
    private const string ChannelId = "shared_joy_votes";

    public NotificationService(IVotingEngine votingEngine)
    {
        votingEngine.NewTrackAdded += OnNewTrackAdded;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
#if ANDROID
        CreateNotificationChannel();
        await RequestAndroidPermissionAsync();
#else
        await Task.CompletedTask;
#endif
    }

    /// <inheritdoc/>
    public async Task SendAsync(string title, string body)
    {
#if ANDROID
        SendAndroidNotification(title, body);
#elif WINDOWS
        SendWindowsToast(title, body);
#endif
        await Task.CompletedTask;
    }

    // ── 事件处理 ──────────────────────────────────────────────────────────────

    private void OnNewTrackAdded(object? sender, SpotifyTrack track)
    {
        var title = "New Song Request 🎵";
        var body = $"{track.Name}  —  {track.Artists}";

        // 从后台线程安全发送
        _ = Task.Run(async () => await SendAsync(title, body));
    }

    // ── Android ───────────────────────────────────────────────────────────────

#if ANDROID
    private static void CreateNotificationChannel()
    {
        if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.O)
            return;

        var channel = new Android.App.NotificationChannel(
            ChannelId,
            "Song Requests",
            Android.App.NotificationImportance.Default)
        {
            Description = "Notifications for new guest song requests"
        };

        var manager = Android.App.Application.Context
            .GetSystemService(Android.Content.Context.NotificationService)
            as Android.App.NotificationManager;

        manager?.CreateNotificationChannel(channel);
    }

    private static async Task RequestAndroidPermissionAsync()
    {
        // POST_NOTIFICATIONS 是 Android 13（API 33）新增的运行时权限
        if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.Tiramisu)
            return;

        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        if (status != PermissionStatus.Granted)
            await Permissions.RequestAsync<Permissions.PostNotifications>();
    }

    private static void SendAndroidNotification(string title, string body)
    {
        try
        {
            var context = Android.App.Application.Context;
            var notificationManager = AndroidX.Core.App.NotificationManagerCompat.From(context);

            // 检查权限（Android 13+）
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu &&
                Android.App.Application.Context.CheckSelfPermission(
                    Android.Manifest.Permission.PostNotifications)
                != Android.Content.PM.Permission.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[Notification] 缺少 POST_NOTIFICATIONS 权限，跳过");
                return;
            }

            var notification = new AndroidX.Core.App.NotificationCompat.Builder(context, ChannelId)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
                .SetPriority(AndroidX.Core.App.NotificationCompat.PriorityDefault)
                .SetAutoCancel(true)
                .Build();

            notificationManager.Notify(System.Threading.Interlocked.Increment(ref _notificationId), notification);
            System.Diagnostics.Debug.WriteLine($"[Notification] Android 通知已发送: {title}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notification] Android 通知异常: {ex.Message}");
        }
    }
#endif

    // ── Windows ───────────────────────────────────────────────────────────────

#if WINDOWS
    private static void SendWindowsToast(string title, string body)
    {
        try
        {
            // 构建 Toast XML（ToastText02 模板：大标题 + 正文）
            var template = Windows.UI.Notifications.ToastNotificationManager
                .GetTemplateContent(Windows.UI.Notifications.ToastTemplateType.ToastText02);

            var textNodes = template.GetElementsByTagName("text");
            textNodes[0].AppendChild(template.CreateTextNode(title));
            textNodes[1].AppendChild(template.CreateTextNode(body));

            var toast = new Windows.UI.Notifications.ToastNotification(template);

            // CreateToastNotifier() 对于 MAUI Windows（打包或非打包）均可尝试
            Windows.UI.Notifications.ToastNotificationManager
                .CreateToastNotifier()
                .Show(toast);

            System.Diagnostics.Debug.WriteLine($"[Notification] Windows Toast 已发送: {title}");
        }
        catch (Exception ex)
        {
            // 未打包的应用在某些系统版本下可能不支持 Toast，静默忽略
            System.Diagnostics.Debug.WriteLine($"[Notification] Windows Toast 异常（已忽略）: {ex.Message}");
        }
    }
#endif
}
