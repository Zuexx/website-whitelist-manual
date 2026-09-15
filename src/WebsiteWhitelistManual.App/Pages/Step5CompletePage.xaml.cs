// src/WebsiteWhitelistManual.App/Pages/Step5CompletePage.xaml.cs
using System.Windows.Controls;
using WebsiteWhitelistManual.App.ViewModels;

namespace WebsiteWhitelistManual.App.Pages;

public partial class Step5CompletePage : Page
{
    public Step5CompletePage(Step5CompleteViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
