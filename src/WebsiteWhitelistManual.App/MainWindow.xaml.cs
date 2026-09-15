// src/WebsiteWhitelistManual.App/MainWindow.xaml.cs
using WebsiteWhitelistManual.App.Pages;
using Wpf.Ui.Controls;

namespace WebsiteWhitelistManual.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();

        RootNavigation.SetServiceProvider(services);

        // NavigationView's content-presenter template part only exists once
        // its own template has been applied, which happens after this
        // constructor returns — navigating here throws a NullReferenceException.
        // Loaded fires once layout/templating has actually completed.
        Loaded += (_, _) => RootNavigation.Navigate(typeof(DashboardPage));
    }
}
