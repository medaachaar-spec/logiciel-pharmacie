namespace Pharmacy.Application.Security;

public static class SecurityPermissions
{
    public const string SalesCreate = "sales.create";
    public const string SalesViewHistory = "sales.view_history";
    public const string SalesViewAllHistory = "sales.view_all_history";
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
    public const string SecurityViewEmployeeActivity = "security.view_employee_activity";
    public const string AuditView = "audit.view";
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";
    public const string ReportsManageSettings = "reports.manage_settings";
    public const string BackupView = "backup.view";
    public const string BackupCreate = "backup.create";
    public const string BackupRestore = "backup.restore";
    public const string ImportView = "import.view";
    public const string ImportExecute = "import.execute";
    public const string DevicesConfigure = "devices.configure";

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
        ReportsView,
        ReportsExport,
        ReportsManageSettings,
        BackupView,
        BackupCreate,
        BackupRestore,
        ImportView,
        ImportExecute,
        DevicesConfigure,
        SalesViewAllHistory,
        SecurityViewEmployeeActivity
    ];

    public static IReadOnlySet<string> Sensitive { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        SalesViewAllHistory,
        SalesReturn,
        SalesRefund,
        InventoryAdjust,
        InventoryFinalizeCount,
        PurchasingReceive,
        CatalogChangePrice,
        CustomersManageCredit,
        SecurityManageUsers,
        SecurityManageRoles,
        ReportsManageSettings,
        BackupRestore,
        ImportExecute,
        DevicesConfigure,
        SecurityViewEmployeeActivity
    };
}
