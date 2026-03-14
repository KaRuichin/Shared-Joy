using Shared_Joy.ViewModels;

namespace Shared_Joy.Pages;

/// <summary>
/// 队列管理页面 —— 主持人手动调整/移除/锁定队列
/// </summary>
public partial class QueueManagementPage : ContentPage
{
    public QueueManagementPage(QueueManagementViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
