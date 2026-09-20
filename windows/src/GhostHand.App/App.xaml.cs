using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HolyHand.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private const string MutexName = "Global\\HolyHand_SingleInstance_Mutex_2026";
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool createdNew;
        _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);
        if (!createdNew)
        {
            MessageBox.Show("HolyHand is already running in the system tray.", "HolyHand", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        if (_singleInstanceMutex != null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex wasn't owned
            }
            _singleInstanceMutex.Dispose();
        }
        base.OnExit(e);
    }
}
