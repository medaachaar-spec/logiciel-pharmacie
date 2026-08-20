using System.Text.RegularExpressions;
using Pharmacy.Application.Branding.Contracts;
using Pharmacy.Application.Branding.Errors;
using Pharmacy.Application.Branding.Models;
using Pharmacy.Application.Security;
using Pharmacy.Desktop.Components.Branding;
using Pharmacy.Domain.Common;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class PharmacyBrandingUiTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void BRAND003_LoginRendersPharmacyName()
    {
        var entry = DesktopText("Components", "Security", "SecurityEntry.razor");
        var mark = DesktopText("Components", "Branding", "PharmacyBrandMark.razor");

        Assert.Contains("<PharmacyBrandMark Variant=\"login\"", entry, StringComparison.Ordinal);
        Assert.Contains("Branding.Current.PharmacyName", mark, StringComparison.Ordinal);
    }

    [Fact]
    public void BRAND004_LoginRendersConfiguredLogo()
    {
        var mark = DesktopText("Components", "Branding", "PharmacyBrandMark.razor");

        Assert.Contains("src=\"@Branding.LogoSource\"", mark, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain", DesktopText("wwwroot", "css", "security.css"), StringComparison.Ordinal);
    }

    [Fact]
    public void BRAND005_SidebarRendersPharmacyName()
    {
        var sidebar = DesktopText("Components", "Layout", "AppSidebar.razor");

        Assert.Contains("<PharmacyBrandMark Variant=\"sidebar\"", sidebar, StringComparison.Ordinal);
        Assert.DoesNotContain("Logiciel Pharmacie</strong>", sidebar, StringComparison.Ordinal);
    }

    [Fact]
    public void BRAND006_SidebarRendersLogo()
    {
        var sidebar = DesktopText("Components", "Layout", "AppSidebar.razor");
        var css = DesktopText("wwwroot", "css", "security.css");

        Assert.Contains("PharmacyBrandMark", sidebar, StringComparison.Ordinal);
        Assert.Contains(".pharmacy-brand-mark--sidebar .pharmacy-brand-mark__logo", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BRAND019_MissingLogoAndReadFailureFallbackDoNotBreakLoginOrSidebar()
    {
        var service = new AlternatingBrandingService();
        var coordinator = new BrandingCoordinator(service);

        await coordinator.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Valeur temporaire", coordinator.Current.PharmacyName);
        await coordinator.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Pharmacie Diar Douha", coordinator.Current.PharmacyName);
        Assert.Equal(BrandingCoordinator.BundledLogoPath, coordinator.LogoSource);
        Assert.True(coordinator.IsUsingFallback);
        Assert.NotNull(coordinator.Message);
    }

    [Fact]
    public void BRAND020_LongPharmacyNameDoesNotBreakShellStructure()
    {
        var css = DesktopText("wwwroot", "css", "security.css");

        Assert.Contains("text-overflow: ellipsis", css, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere", css, StringComparison.Ordinal);
        Assert.Contains("min-width: 0", css, StringComparison.Ordinal);
    }

    [Fact]
    public void BRAND021_CustomForbiddenJargonRemainsAbsent()
    {
        var sources = new[]
        {
            DesktopText("Components", "Security", "SecurityEntry.razor"),
            DesktopText("Components", "Branding", "PharmacyBrandMark.razor"),
            DesktopText("Components", "Layout", "AppSidebar.razor")
        };
        var visible = string.Join(Environment.NewLine, sources.SelectMany(source =>
            Regex.Matches(source, @">(?<value>[^<]+)<", RegexOptions.CultureInvariant)
                .Select(match => match.Groups["value"].Value)
                .Where(value => !value.Contains('@', StringComparison.Ordinal))));

        Assert.DoesNotMatch(new Regex(@"\b(Demo|Démo|Prototype|Sprint|Local|SQL|Pilot)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), visible);
    }

    [Fact]
    public void BRAND022_BusinessNavigationAndPermissionsRemainUnchanged()
    {
        var permissionCount = SecurityPermissions.All.Count;
        var catalog = DesktopText("Components", "Navigation", "AppNavigationCatalog.cs");
        var navigationCount = Regex.Matches(catalog, @"new(?:\s+AppNavigationItem)?\s*\(\s*""[^""]+""").Count;

        Assert.Equal(28, permissionCount);
        Assert.Equal(13, navigationCount);
        Assert.DoesNotContain("BrandingManage", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeThemeKeepsPrimaryTextContrastReadable()
    {
        var palette = BrandingThemePalette.FromPrimary("#0D9A48");

        Assert.Equal("#0D9A48", palette.Primary);
        Assert.Equal("#111827", palette.OnPrimary);
        Assert.True(Contrast(palette.Primary, palette.OnPrimary) >= 4.5);
        Assert.True(Contrast("#FFFFFF", palette.Focus) >= 3);
    }

    private static double Contrast(string left, string right)
    {
        static double Luminance(string value)
        {
            static double Linear(byte channel)
            {
                var normalized = channel / 255d;
                return normalized <= 0.04045 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
            }
            var red = Convert.ToByte(value.Substring(1, 2), 16);
            var green = Convert.ToByte(value.Substring(3, 2), 16);
            var blue = Convert.ToByte(value.Substring(5, 2), 16);
            return 0.2126 * Linear(red) + 0.7152 * Linear(green) + 0.0722 * Linear(blue);
        }
        var first = Luminance(left);
        var second = Luminance(right);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private sealed class AlternatingBrandingService : IPharmacyBrandingService
    {
        private int reads;

        public Task<Result<PharmacyBrandingView>> GetAsync(CancellationToken cancellationToken = default)
        {
            reads++;
            return Task.FromResult(reads == 1
                ? Result<PharmacyBrandingView>.Success(new PharmacyBrandingView(
                    "Valeur temporaire", "#246B45", [1, 2, 3], "image/png", "temp.png", DateTime.UtcNow, [1]))
                : Result<PharmacyBrandingView>.Failure(PharmacyBrandingErrors.DatabaseUnavailable));
        }

        public Task<Result<PharmacyBrandingView>> UpdateAsync(
            UpdatePharmacyBrandingRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static string DesktopText(params string[] segments) => File.ReadAllText(Path.Combine(
        new[] { RepositoryRoot, "src", "Pharmacy.Desktop" }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx"))) return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("Racine du dépôt introuvable.");
    }
}
