namespace Pharmacy.Application.Security;

public static class SecurityPermissions
{
    public const string SalesCreate = "sales.create";
    public const string SalesViewHistory = "sales.view_history";
    public const string SalesReturn = "sales.return";
    public const string SalesRefund = "sales.refund";
    public const string InventoryView = "inventory.view";
    public const string InventoryAdjust = "inventory.adjust";
    public const string InventoryFinalizeCount = "inventory.finalize_count";
    public const string PurchasingView = "purchasing.view";
    public const string PurchasingReceive = "purchasing.receive";
    public const string CatalogView = "catalog.view";
    public const string CatalogManage = "catalog.manage";
    public const string CatalogChangePrice = "catalog.change_price";
    public const string CustomersView = "customers.view";
    public const string CustomersManage = "customers.manage";
    public const string CustomersManageCredit = "customers.manage_credit";
    public const string CashView = "cash.view";
    public const string SecurityManageUsers = "security.manage_users";
    public const string SecurityManageRoles = "security.manage_roles";
    public const string AuditView = "audit.view";
    public const string ReportsView = "reports.view";

    public static IReadOnlyList<string> All { get; } =
    [
        SalesCreate,
        SalesViewHistory,
        SalesReturn,
        SalesRefund,
        InventoryView,
        InventoryAdjust,
        InventoryFinalizeCount,
        PurchasingView,
        PurchasingReceive,
        CatalogView,
        CatalogManage,
        CatalogChangePrice,
        CustomersView,
        CustomersManage,
        CustomersManageCredit,
        CashView,
        SecurityManageUsers,
        SecurityManageRoles,
        AuditView,
        ReportsView
    ];

    public static IReadOnlySet<string> Sensitive { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        SalesReturn,
        SalesRefund,
        InventoryAdjust,
        InventoryFinalizeCount,
        PurchasingReceive,
        CatalogChangePrice,
        CustomersManageCredit,
        SecurityManageUsers,
        SecurityManageRoles
    };
}
