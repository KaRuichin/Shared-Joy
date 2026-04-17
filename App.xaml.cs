using Shared_Joy.Services;

namespace Shared_Joy
{
    public partial class App : Application
    {
        public App(ISpotifyAuthService spotifyAuth, INotificationService notificationService)
        {
            InitializeComponent();

            // 应用启动时立即从 SecureStorage/Preferences 恢复 Spotify Token，
            // 避免用户需要访问 SettingsPage 才能激活已登录状态
            _ = spotifyAuth.TryRestoreTokenAsync();

            // 初始化通知渠道并申请权限（同时激活 Singleton，使事件订阅生效）
            _ = notificationService.InitializeAsync();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}