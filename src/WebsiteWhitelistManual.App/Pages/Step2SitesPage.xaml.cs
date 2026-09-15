// src/WebsiteWhitelistManual.App/Pages/Step2SitesPage.xaml.cs
using System.Windows.Controls;
using WebsiteWhitelistManual.App.ViewModels;

namespace WebsiteWhitelistManual.App.Pages;

public partial class Step2SitesPage : Page
{
    public Step2SitesPage(Step2SitesViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
