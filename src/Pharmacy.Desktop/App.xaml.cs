using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Infrastructure.Deployment;

namespace Pharmacy.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += HandleDispatcherUnhandledException;

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
        string? databaseEndpoint;
        try
        {
            databaseEndpoint = DesktopConnectionResolver.Resolve();
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(
                exception.Message,
                "Logiciel Pharmacie — configuration SQL refusée",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(2);
            return;
        }
        services.AddCatalogBackedSales(databaseEndpoint);

#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        serviceProvider = services.BuildServiceProvider();

        if (!string.IsNullOrWhiteSpace(databaseEndpoint))
        {
            var compatibility = await DatabaseCompatibilityGuard.CheckAsync(serviceProvider);
            if (!compatibility.IsCompatible)
            {
                MessageBox.Show(
                    compatibility.Message + Environment.NewLine + Environment.NewLine +
                    "L'application ne lancera aucune migration automatiquement. Exécutez Pharmacy.Deployment.",
                    "Logiciel Pharmacie — schéma SQL incompatible",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(2);
                return;
            }
        }

        var mainWindow = new MainWindow(serviceProvider);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= HandleDispatcherUnhandledException;
        try
        {
            serviceProvider?.GetService<IAuthenticationService>()
                ?.CloseApplicationAsync(Guid.NewGuid())
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            // A failed best-effort close remains identifiable as an open/interrupted session.
        }
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
