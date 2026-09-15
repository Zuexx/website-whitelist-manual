// src/WebsiteWhitelistManual.App/App.xaml.cs
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebsiteWhitelistManual.App.Pages;
using WebsiteWhitelistManual.App.Services;
using WebsiteWhitelistManual.App.State;
using WebsiteWhitelistManual.App.ViewModels;
using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Services;

namespace WebsiteWhitelistManual.App;

public partial class App : Application
{
    // Built inside OnStartup's try/catch (not here in the constructor) so a
    // ValidateOnBuild failure is caught and shown to the user via MessageBox
    // instead of crashing unhandled before OnStartup ever runs.
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton<IWindowsRegistry, WindowsRegistryAdapter>();
                    services.AddSingleton<IProcessRunner, WindowsProcessRunner>();
                    services.AddSingleton<ILocalAccountSource, WindowsLocalAccountSource>();
                    services.AddSingleton<IRegistryPolicyReader, RegistryPolicyReader>();
                    services.AddSingleton<IRegistryPolicyWriter, RegistryPolicyWriter>();
                    services.AddSingleton<IRegistryBackupService, RegistryBackupService>();
                    services.AddSingleton<ILocalAccountInspector, LocalAccountInspector>();
                    services.AddSingleton<MainWindow>();

                    services.AddSingleton<WizardConfigurationStore>();

                    services.AddTransient<DashboardPage>();
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<Step1BrowserPage>();
                    services.AddTransient<Step1BrowserViewModel>();
                    services.AddTransient<Step2SitesPage>();
                    services.AddTransient<Step2SitesViewModel>();
                    services.AddTransient<Step3AdvancedPage>();
                    services.AddTransient<Step3AdvancedViewModel>();
                    services.AddTransient<Step4ConfirmPage>();
                    services.AddTransient<Step4ConfirmViewModel>();
                    services.AddTransient<Step5CompletePage>();
                    services.AddTransient<Step5CompleteViewModel>();
                })
                .UseDefaultServiceProvider(options =>
                {
                    options.ValidateOnBuild = true;
                    options.ValidateScopes = true;
                })
                .Build();

            await _host.StartAsync();

            // Resolve the Core services once at startup to prove the DI
            // wiring actually produces working instances, not just that the
            // container builds — ValidateOnBuild alone doesn't construct
            // anything.
            _ = _host.Services.GetRequiredService<IRegistryPolicyReader>();
            _ = _host.Services.GetRequiredService<IRegistryPolicyWriter>();
            _ = _host.Services.GetRequiredService<IRegistryBackupService>();
            _ = _host.Services.GetRequiredService<ILocalAccountInspector>();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"啟動時發生錯誤：{ex.Message}", "網站白名單設定工具", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
