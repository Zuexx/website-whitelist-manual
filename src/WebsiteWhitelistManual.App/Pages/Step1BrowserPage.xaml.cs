// src/WebsiteWhitelistManual.App/Pages/Step1BrowserPage.xaml.cs
using System.Windows.Controls;
using WebsiteWhitelistManual.App.ViewModels;

namespace WebsiteWhitelistManual.App.Pages;

public partial class Step1BrowserPage : Page
{
    public Step1BrowserPage(Step1BrowserViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
