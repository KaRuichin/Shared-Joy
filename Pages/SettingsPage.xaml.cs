using Shared_Joy.ViewModels;

namespace Shared_Joy.Pages;

/// <summary>
/// 设置页面 —— Spotify 配置 + 应用设置
/// </summary>
public partial class SettingsPage : ContentPage
{
    private const string AndroidCallback = "sharedjoy://callback";
    private const string WindowsCallback = "http://127.0.0.1:5432/callback";

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnCopyAndroidCallbackTapped(object? sender, TappedEventArgs e)
    {
        await Clipboard.SetTextAsync(AndroidCallback);
        await DisplayAlert("Copied", "Android callback URL copied.", "OK");
    }

    private async void OnCopyWindowsCallbackTapped(object? sender, TappedEventArgs e)
    {
        await Clipboard.SetTextAsync(WindowsCallback);
        await DisplayAlert("Copied", "Windows callback URL copied.", "OK");
    }
}
