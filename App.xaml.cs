using Shared_Joy.Services;

namespace Shared_Joy
{
    public partial class App : Application
    {
        public App(ISpotifyAuthService spotifyAuth, INotificationService notificationService)
        {
            InitializeComponent();

            // 恢复 Spotify Token
            _ = spotifyAuth.TryRestoreTokenAsync();

            // 仅注入即可：NotificationService 构造时完成事件订阅 + 通知渠道创建。
            // 运行时权限（Android 13+）需要 Activity 可见后申请，由 DashboardPage.OnAppearing 负责。
            _ = notificationService;

#if WINDOWS
            // 未打包 Windows 应用需要显式注册，才能通过 AppNotificationManager 发送 Toast
            Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Register();
#endif
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}