using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Shared_Joy.Pages;
using Shared_Joy.Services;
using Shared_Joy.ViewModels;

namespace Shared_Joy
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // 注册服务（接口 → 实现）
            builder.Services.AddSingleton<ISpotifyAuthService, SpotifyAuthService>();
            builder.Services.AddSingleton<ISpotifyApiService, SpotifyApiService>();
            builder.Services.AddSingleton<IWebServerService, WebServerService>();
            builder.Services.AddSingleton<IStaticWebAssetService, StaticWebAssetService>();
            builder.Services.AddSingleton<IVotingEngine, VotingEngine>();
            builder.Services.AddSingleton<ISessionManager, SessionManager>();
            builder.Services.AddSingleton<IQueueSyncService, QueueSyncService>();
            builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
            builder.Services.AddSingleton<INotificationService, NotificationService>();

            // 注册 ViewModels
            builder.Services.AddTransient<DashboardViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<QueueManagementViewModel>();

            // 注册页面
            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<QueueManagementPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
