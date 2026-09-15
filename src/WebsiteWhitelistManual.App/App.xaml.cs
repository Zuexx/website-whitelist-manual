// src/WebsiteWhitelistManual.App/App.xaml.cs
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebsiteWhitelistManual.App.Services;
using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Services;

namespace WebsiteWhitelistManual.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
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
            })
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
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
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
