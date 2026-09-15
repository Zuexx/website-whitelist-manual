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
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
