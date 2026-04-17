using Shared_Joy.Services;

namespace Shared_Joy
{
    public partial class App : Application
    {
        public App(ISpotifyAuthService spotifyAuth)
        {
            InitializeComponent();

            // 应用启动时立即从 SecureStorage/Preferences 恢复 Spotify Token，
            // 避免用户需要访问 SettingsPage 才能激活已登录状态
            _ = spotifyAuth.TryRestoreTokenAsync();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}