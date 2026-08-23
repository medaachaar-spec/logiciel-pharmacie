namespace Pharmacy.Application.Security;

public static class SecurityRolePresets
{
    public const string OwnerCode = "OWNER";
    public const string ManagerCode = "MANAGER";
    public const string SellerCode = "SELLER";

    public static IReadOnlyList<string> ManagerPermissions { get; } =
        SecurityPermissions.All.Where(code => !SecurityPermissions.OwnerExclusive.Contains(code)).ToArray();

    public static IReadOnlyList<string> SellerPermissions { get; } =
    [
        SecurityPermissions.SalesCreate,
        SecurityPermissions.SalesViewHistory,
        SecurityPermissions.CatalogView,
        SecurityPermissions.InventoryView,
        SecurityPermissions.CustomersView
    ];
}
