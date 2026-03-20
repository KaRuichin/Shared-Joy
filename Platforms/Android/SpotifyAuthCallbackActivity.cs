using Android.App;
using Android.Content.PM;

namespace Shared_Joy;

/// <summary>
/// Spotify OAuth 回调专用 Activity
/// 
/// WebAuthenticator 要求使用继承自 WebAuthenticatorCallbackActivity 的 Activity
/// 来接收自定义 scheme 回调（sharedjoy://callback），并将结果传递回
/// WebAuthenticator.AuthenticateAsync() 的 awaiter。
/// </summary>
[Activity(
    NoHistory = true,
    LaunchMode = LaunchMode.SingleTop,
    Exported = true)]
[IntentFilter(
    [Android.Content.Intent.ActionView],
    Categories = [
        Android.Content.Intent.CategoryDefault,
        Android.Content.Intent.CategoryBrowsable
    ],
    DataScheme = "sharedjoy",
    DataHost = "callback")]
public class SpotifyAuthCallbackActivity : WebAuthenticatorCallbackActivity
{
}
