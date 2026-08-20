using System.Windows;
using Pharmacy.Infrastructure.Deployment;

namespace Pharmacy.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        InitializeComponent();
        Title = $"Logiciel Pharmacie — {PilotReleaseInfo.Version}";
        BlazorView.Services = services;
    }
}
