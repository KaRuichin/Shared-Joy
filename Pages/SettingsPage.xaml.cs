using Shared_Joy.ViewModels;

namespace Shared_Joy.Pages;

/// <summary>
/// 设置页面 —— Spotify 配置 + 应用设置
/// </summary>
public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.InitializeCommand.Execute(null);
    }

    /// <summary>
    /// 点击回调 URI 标签时复制文本到剪贴板，并短暂显示视觉反馈
    /// </summary>
    private async void OnCopyCallbackUri(object? sender, TappedEventArgs e)
    {
        if (sender is not Label label) return;

        var originalText = label.Text;
        await Clipboard.SetTextAsync(originalText);

        // 视觉反馈：短暂变绿 + 显示已复制
        label.TextColor = Colors.Green;
        label.Text = "✅ Copied!";

        await Task.Delay(1200);

        // 恢复原始状态
        label.Text = originalText;
        label.TextColor = Color.FromArgb("#1E90FF"); // DodgerBlue
    }
}
