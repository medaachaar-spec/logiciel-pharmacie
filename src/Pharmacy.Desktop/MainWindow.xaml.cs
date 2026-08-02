using System.Windows;

namespace Pharmacy.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        InitializeComponent();
        BlazorView.Services = services;
    }
}
