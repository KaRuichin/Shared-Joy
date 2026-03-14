using Shared_Joy.ViewModels;

namespace Shared_Joy.Pages;

/// <summary>
/// 设置页面 —— Spotify 配置 + 应用设置
/// </summary>
public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
