using Shared_Joy.ViewModels;

namespace Shared_Joy.Pages;

/// <summary>
/// 主面板页面 —— 当前播放/QR码/PIN/投票队列/会话控制
/// 
/// 页面出现时启动播放状态轮询，消失时停止，避免后台无谓请求。
/// </summary>
public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.StartPolling();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopPolling();
    }
}
