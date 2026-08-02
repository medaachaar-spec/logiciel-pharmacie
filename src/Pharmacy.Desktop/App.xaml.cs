using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Pharmacy.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += HandleDispatcherUnhandledException;

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();

#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        serviceProvider = services.BuildServiceProvider();

        var mainWindow = new MainWindow(serviceProvider);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= HandleDispatcherUnhandledException;
        serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void HandleDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
#if DEBUG
        var message = $"Une erreur non gérée est survenue :{Environment.NewLine}{Environment.NewLine}{e.Exception}";
#else
        const string message = "Une erreur inattendue est survenue. Fermez puis relancez l’application.";
#endif

        MessageBox.Show(
            message,
            "Logiciel Pharmacie — erreur",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
