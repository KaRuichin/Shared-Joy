using Shared_Joy.ViewModels;

namespace Shared_Joy.Pages;

/// <summary>
/// 队列管理页面 —— 主持人手动调整/移除/锁定队列
///
/// 生命周期：
/// - OnAppearing: 启动 ViewModel 的队列实时刷新定时器
/// - OnDisappearing: 停止定时器以节省资源
/// </summary>
public partial class QueueManagementPage : ContentPage
{
    private readonly QueueManagementViewModel _viewModel;

    public QueueManagementPage(QueueManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.OnPageAppearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.OnPageDisappearing();
    }

    /// <summary>
    /// 置顶按钮点击处理
    /// </summary>
    private void OnMoveToTopClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string trackId)
        {
            _viewModel.MoveToTopCommand.Execute(trackId);
        }
    }

    /// <summary>
    /// 移除按钮点击处理
    /// </summary>
    private void OnRemoveTrackClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string trackId)
        {
            _viewModel.RemoveTrackCommand.Execute(trackId);
        }
    }
}
