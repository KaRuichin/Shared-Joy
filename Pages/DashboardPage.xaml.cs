using Shared_Joy.ViewModels;

namespace Shared_Joy.Pages;

/// <summary>
/// 主面板页面 —— 当前播放/QR码/PIN/投票队列/会话控制
/// </summary>
public partial class DashboardPage : ContentPage
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
