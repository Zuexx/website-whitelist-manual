// src/WebsiteWhitelistManual.App/MainWindow.xaml.cs
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WebsiteWhitelistManual.App.Pages;
using Wpf.Ui.Controls;

namespace WebsiteWhitelistManual.App;

public partial class MainWindow : FluentWindow
{
    private readonly IServiceProvider _services;

    public MainWindow(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();

        PageHost.Content = _services.GetRequiredService<DashboardPage>();
        DashboardNavItem.IsActive = true;
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, RoutedEventArgs args)
    {
        if (RootNavigation.SelectedItem is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        PageHost.Content = tag switch
        {
            "Dashboard" => _services.GetRequiredService<DashboardPage>(),
            "Step1" => _services.GetRequiredService<Step1BrowserPage>(),
            "Step2" => _services.GetRequiredService<Step2SitesPage>(),
            "Step3" => _services.GetRequiredService<Step3AdvancedPage>(),
            "Step4" => _services.GetRequiredService<Step4ConfirmPage>(),
            "Step5" => _services.GetRequiredService<Step5CompletePage>(),
            _ => PageHost.Content
        };
    }
}
