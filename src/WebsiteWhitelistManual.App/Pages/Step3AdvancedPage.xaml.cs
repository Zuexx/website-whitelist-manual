// src/WebsiteWhitelistManual.App/Pages/Step3AdvancedPage.xaml.cs
using System.Windows.Controls;
using WebsiteWhitelistManual.App.ViewModels;

namespace WebsiteWhitelistManual.App.Pages;

public partial class Step3AdvancedPage : Page
{
    public Step3AdvancedPage(Step3AdvancedViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
