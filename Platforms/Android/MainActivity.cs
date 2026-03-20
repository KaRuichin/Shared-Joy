using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Shared_Joy
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    // 注：Spotify OAuth 回调由 SpotifyAuthCallbackActivity 处理（WebAuthenticatorCallbackActivity）
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
