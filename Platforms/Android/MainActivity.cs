using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Shared_Joy
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    // Spotify OAuth 回调 Deep Link：sharedjoy://callback
    [IntentFilter(
        [Android.Content.Intent.ActionView],
        Categories = [Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable],
        DataScheme = "sharedjoy",
        DataHost = "callback")]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
