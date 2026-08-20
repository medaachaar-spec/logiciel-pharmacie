using Microsoft.EntityFrameworkCore;
using Pharmacy.Infrastructure.Persistence;
using Xunit;

namespace Pharmacy.IntegrationTests;

public sealed class SecurityArchitectureTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void SecurityMigrationSeedsOnlyReferenceRbacDataAndNoCredential()
    {
        var migration = File.ReadAllText(PathAt("src", "Pharmacy.Infrastructure", "Persistence", "Migrations", "20260810145717_SecurityRbacAuditPersistenceV01.cs"));
        Assert.Contains("SecurityRbacAuditPersistenceV01", migration, StringComparison.Ordinal);
        Assert.Contains("table: \"Permissions\"", migration, StringComparison.Ordinal);
        Assert.Contains("table: \"Roles\"", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("table: \"UserAccounts\",\r\n                columns:", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHasher", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=", migration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityModelHasNormalizedLoginAndNoCascadeHistory()
    {
        var builder = new DbContextOptionsBuilder<PharmacyDbContext>();
        builder.UsePharmacySqlServer("Server=not-used.invalid;Database=ModelOnly;Integrated Security=True;Encrypt=True");
        using var context = new PharmacyDbContext(builder.Options);
        var user = context.Model.GetEntityTypes().Single(entity => entity.GetTableName() == "UserAccounts");
        Assert.True(user.GetIndexes().Single(index => index.Properties.Single().Name == "NormalizedLogin").IsUnique);
        Assert.All(context.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()), foreignKey =>
            Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void DesktopSecurityGateAndBackendPermissionChecksArePresent()
    {
        var root = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "Components", "RootComponent.razor"));
        var navigation = File.ReadAllText(PathAt("src", "Pharmacy.Desktop", "Components", "Layout", "AppSidebar.razor"));
        var application = AllText(PathAt("src", "Pharmacy.Application", "Security"));
        Assert.Contains("Session.IsAuthenticated", root, StringComparison.Ordinal);
        Assert.Contains("Session.IsLocked", root, StringComparison.Ordinal);
        Assert.Contains("LockIfInactive", root, StringComparison.Ordinal);
        Assert.Contains(".Where(Allowed)", navigation, StringComparison.Ordinal);
        Assert.Contains("AuthorizedRouteView", root, StringComparison.Ordinal);
        Assert.Contains("PermissionAuthorizer", application, StringComparison.Ordinal);
        Assert.Contains("ReauthenticationRequired", application, StringComparison.Ordinal);
    }

    [Fact]
    public void SecuritySourcesContainNoRemoteIdentityOrSensitiveLogging()
    {
        var source = AllText(PathAt("src", "Pharmacy.Application", "Security"))
            + AllText(PathAt("src", "Pharmacy.Infrastructure", "Security"));
        Assert.DoesNotContain("http://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Azure", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OAuth", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LogInformation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LogDebug", source, StringComparison.Ordinal);
    }

    private static string AllText(string path) => string.Join(Environment.NewLine,
        Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
    private static string PathAt(params string[] parts) => Path.Combine(new[] { Root }.Concat(parts).ToArray());
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pharmacy.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
