// src/WebsiteWhitelistManual.App/Pages/Step4ConfirmPage.xaml.cs
using System.Windows.Controls;
using WebsiteWhitelistManual.App.ViewModels;

namespace WebsiteWhitelistManual.App.Pages;

public partial class Step4ConfirmPage : Page
{
    public Step4ConfirmPage(Step4ConfirmViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
