using Pharmacy.Application.Security;

namespace Pharmacy.Desktop.Components.Navigation;

public static class PermissionAwarePageDescription
{
    public static string Resolve(
        string? pageId,
        string defaultDescription,
        Func<string, bool> hasPermission)
    {
        ArgumentNullException.ThrowIfNull(defaultDescription);
        ArgumentNullException.ThrowIfNull(hasPermission);

        return pageId switch
        {
            "catalog" when hasPermission(SecurityPermissions.CatalogManage) =>
                "Rechercher et gérer les produits.",
            "catalog" =>
                "Rechercher et consulter les produits.",
            "customers" when hasPermission(SecurityPermissions.CustomersManage)
                && hasPermission(SecurityPermissions.CustomersManageCredit) =>
                "Gérer les clients, crédits et règlements.",
            "customers" when hasPermission(SecurityPermissions.CustomersManage) =>
                "Rechercher et gérer les clients.",
            "customers" when hasPermission(SecurityPermissions.CustomersManageCredit) =>
                "Consulter les clients et gérer les crédits et règlements.",
            "customers" =>
                "Rechercher et consulter les clients.",
            _ => defaultDescription
        };
    }
}
