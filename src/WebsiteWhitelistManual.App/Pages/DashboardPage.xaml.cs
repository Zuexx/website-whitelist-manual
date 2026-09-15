// src/WebsiteWhitelistManual.App/Pages/DashboardPage.xaml.cs
using System.Windows.Controls;
using WebsiteWhitelistManual.App.ViewModels;

namespace WebsiteWhitelistManual.App.Pages;

public partial class DashboardPage : Page
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage(DashboardViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
        InitializeComponent();
    }

    private void OnViewFullPolicyClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new PolicyDetailDialog(_viewModel.Snapshot) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }
}
