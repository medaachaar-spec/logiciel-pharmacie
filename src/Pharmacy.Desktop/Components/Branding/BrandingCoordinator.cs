using System.Text;
using Pharmacy.Application.Branding;
using Pharmacy.Application.Branding.Contracts;
using Pharmacy.Application.Branding.Models;

namespace Pharmacy.Desktop.Components.Branding;

public sealed class BrandingCoordinator
{
    public const string BundledLogoPath = "branding/pharmacie-diar-douha-logo.png";
    private readonly IPharmacyBrandingService service;
    private readonly RuntimeWindowTitle? windowTitle;
    private int operationInProgress;

    public BrandingCoordinator(
        IPharmacyBrandingService service,
        RuntimeWindowTitle? windowTitle = null)
    {
        this.service = service;
        this.windowTitle = windowTitle;
        Current = PharmacyBrandingDefaults.CreateView();
        RefreshDerivedValues();
    }

    public event Action? Changed;

    public PharmacyBrandingView Current { get; private set; }
    public string LogoSource { get; private set; } = BundledLogoPath;
    public string ThemeStyle { get; private set; } = string.Empty;
    public string? Message { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsUsingFallback { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Current = PharmacyBrandingDefaults.CreateView();
        IsUsingFallback = true;
        RefreshDerivedValues();

        var result = await service.GetAsync(cancellationToken);
        if (result.IsFailure)
        {
            Message = result.Error.Message;
            NotifyChanged();
            return;
        }

        Current = result.Value;
        IsUsingFallback = false;
        Message = null;
        RefreshDerivedValues();
        NotifyChanged();
    }

    public async Task<bool> UpdateAsync(
        string pharmacyName,
        string primaryColor,
        byte[]? logoContent,
        string? logoMimeType,
        string? logoFileName,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref operationInProgress, 1, 0) != 0) return false;
        IsBusy = true;
        Message = null;
        NotifyChanged();
        try
        {
            var result = await service.UpdateAsync(new UpdatePharmacyBrandingRequest(
                Guid.NewGuid(),
                pharmacyName,
                primaryColor,
                logoContent,
                logoMimeType,
                logoFileName,
                Current.RowVersion.ToArray()), cancellationToken);
            if (result.IsFailure)
            {
                Message = result.Error.Message;
                return false;
            }

            Current = result.Value;
            IsUsingFallback = false;
            Message = "Identité de la pharmacie enregistrée.";
            RefreshDerivedValues();
            return true;
        }
        finally
        {
            IsBusy = false;
            Interlocked.Exchange(ref operationInProgress, 0);
            NotifyChanged();
        }
    }

    public static string BuildThemeStyle(string primaryColor)
    {
        var palette = BrandingThemePalette.FromPrimary(primaryColor);
        var builder = new StringBuilder(320);
        builder.Append("--pharmacy-primary:").Append(palette.Primary).Append(';')
            .Append("--pharmacy-primary-dark:").Append(palette.PrimaryDark).Append(';')
            .Append("--pharmacy-primary-active:").Append(palette.PrimaryActive).Append(';')
            .Append("--pharmacy-primary-soft:").Append(palette.PrimarySoft).Append(';')
            .Append("--pharmacy-primary-border:").Append(palette.PrimaryBorder).Append(';')
            .Append("--pharmacy-on-primary:").Append(palette.OnPrimary).Append(';')
            .Append("--pharmacy-focus:").Append(palette.Focus).Append(';');
        return builder.ToString();
    }

    private void RefreshDerivedValues()
    {
        LogoSource = Current.LogoContent is { Length: > 0 } && !string.IsNullOrWhiteSpace(Current.LogoMimeType)
            ? $"data:{Current.LogoMimeType};base64,{Convert.ToBase64String(Current.LogoContent)}"
            : BundledLogoPath;
        ThemeStyle = BuildThemeStyle(Current.PrimaryColor);
        windowTitle?.SetPharmacyName(Current.PharmacyName);
    }

    private void NotifyChanged() => Changed?.Invoke();
}
