using Shared_Joy.Services;
using Shared_Joy.ViewModels;

namespace Shared_Joy.Pages;

/// <summary>
/// 主面板页面 —— 当前播放/QR码/PIN/投票队列/会话控制
///
/// 页面出现时启动播放状态轮询，消失时停止，避免后台无谓请求。
/// 首次出现时向系统申请通知权限（Activity 已可见，对话框可正常弹出）。
/// </summary>
public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private readonly INotificationService _notificationService;

    public DashboardPage(DashboardViewModel viewModel, INotificationService notificationService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _notificationService = notificationService;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.StartPolling();

        // Activity 可见后才申请通知权限（内部已做幂等，多次调用安全）
        _ = _notificationService.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopPolling();
    }

    private void OnPlayPauseClicked(object sender, EventArgs e) => Vibrate();
    private void OnNextClicked(object sender, EventArgs e) => Vibrate();
    private void OnStartSessionClicked(object sender, EventArgs e) => Vibrate();
    private void OnEndSessionClicked(object sender, EventArgs e) => Vibrate();

    private static void Vibrate()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); }
        catch { }
    }
}
