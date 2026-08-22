using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Desktop.Components.Branding;

namespace Pharmacy.Desktop;

public partial class MainWindow : Window
{
    private readonly RuntimeWindowTitle runtimeTitle;

    public MainWindow(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        InitializeComponent();
        runtimeTitle = services.GetRequiredService<RuntimeWindowTitle>();
        runtimeTitle.Changed += UpdateBrandedTitle;
        Closed += HandleClosed;
        UpdateBrandedTitle();
        BlazorView.Services = services;
    }

    private void UpdateBrandedTitle()
    {
        Dispatcher.Invoke(() => Title = $"{runtimeTitle.PharmacyName} — Logiciel Pharmacie");
    }

    private void HandleClosed(object? sender, EventArgs args)
    {
        runtimeTitle.Changed -= UpdateBrandedTitle;
        Closed -= HandleClosed;
    }
}
