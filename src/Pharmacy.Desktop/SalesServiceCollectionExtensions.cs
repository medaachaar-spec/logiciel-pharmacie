using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Customers.Commands;
using Pharmacy.Application.Customers.Queries;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Catalog.Queries;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Queries;
using Pharmacy.Application.Purchasing.Commands;
using Pharmacy.Application.Purchasing.Queries;
using Pharmacy.Application.Sales.Catalog.Queries;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Queries;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Application.Sales.Finalization.Queries;
using Pharmacy.Application.Sales.Suspensions;
using Pharmacy.Desktop.Components.Pos;
using Pharmacy.Desktop.Components.Catalog;
using Pharmacy.Desktop.Components.Customers;
using Pharmacy.Desktop.Components.Inventory;
using Pharmacy.Desktop.Components.Purchasing;
using Pharmacy.Desktop.Components.Security;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Purchasing;
using Pharmacy.Infrastructure.Sales.InMemory;
using Pharmacy.Infrastructure.Sales;
using Pharmacy.Infrastructure.Security;
using Pharmacy.Infrastructure.Reporting;
using Pharmacy.Desktop.Components.Reporting;
using Pharmacy.Desktop.Components.Operations;
using Pharmacy.Infrastructure.Operations;
using Pharmacy.Infrastructure.Branding;
using Pharmacy.Desktop.Components.Branding;
using Pharmacy.Desktop.Components.Dashboard;

namespace Pharmacy.Desktop;

public static class SalesServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogBackedSales(this IServiceCollection services, string? databaseEndpoint = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSqlCatalogAdapters(databaseEndpoint);
        services.AddSqlInventoryAdapters();
        services.AddSqlPurchasingAdapters();
        services.AddSqlSalesAdapters();
        services.AddLocalSqlSecurity();
        services.AddSqlReporting();
        services.AddOperationalServices();
        services.AddSqlPharmacyBranding();
        services.AddSingleton<ISaleDraftStore, InMemorySaleDraftStore>();
        services.AddSingleton<ISaleDraftIdGenerator, InMemorySaleDraftIdGenerator>();
        services.AddSingleton<ISaleFinalizationPreparationStore, InMemorySaleFinalizationPreparationStore>();

        services.AddTransient<SearchSellableProductsHandler>();
        services.AddTransient<GetSellableProductByIdHandler>();
        services.AddTransient<GetSellableProductByCodeHandler>();
        services.AddTransient<CreateSaleDraftHandler>();
        services.AddTransient<GetSaleDraftHandler>();
        services.AddTransient<AddProductToSaleDraftHandler>();
        services.AddTransient<ChangeSaleItemQuantityHandler>();
        services.AddTransient<RemoveSaleItemHandler>();
        services.AddTransient<ClearSaleDraftHandler>();
        services.AddTransient<RestoreSuspendedSaleDraftHandler>();
        services.AddTransient<PrepareSaleFinalizationHandler>();
        services.AddTransient<FinalizeSaleHandler>();
        services.AddTransient<GetSaleByIdHandler>();
        services.AddTransient<GetRecentSalesHandler>();
        services.AddTransient<GetSaleReturnPreparationHandler>();
        services.AddTransient<ReturnSaleHandler>();
        services.AddTransient<SuspendedSaleWorkflow>();

        services.AddTransient<SearchCatalogProductsHandler>();
        services.AddTransient<GetCatalogProductByIdHandler>();
        services.AddTransient<ListCatalogCategoriesHandler>();
        services.AddTransient<GetCatalogDiagnosticsHandler>();
        services.AddTransient<CreateCatalogProductHandler>();
        services.AddTransient<UpdateCatalogProductHandler>();
        services.AddTransient<SetCatalogProductActivationHandler>();
        services.AddTransient<AddCatalogBarcodeHandler>();
        services.AddTransient<UpdateCatalogBarcodeHandler>();
        services.AddTransient<ChangeCatalogProductPriceHandler>();
        services.AddTransient<CreateCatalogCategoryHandler>();
        services.AddTransient<UpdateCatalogCategoryHandler>();
        services.AddTransient<SetCatalogCategoryActivationHandler>();

        services.AddTransient<InventoryQueryHandlers>();
        services.AddTransient<InventoryCommandHandlers>();
        services.AddTransient<PurchasingQueryHandlers>();
        services.AddTransient<PurchasingCommandHandlers>();
        services.AddTransient<CustomerQueryHandlers>();
        services.AddTransient<CustomerCommandHandlers>();

        services.AddScoped<DemoTicketPrintService>();
        services.AddScoped<SalesPosCoordinator>();
        services.AddScoped<SalesReturnCoordinator>();
        services.AddScoped<CatalogCoordinator>();
        services.AddScoped<InventoryCoordinator>();
        services.AddScoped<PurchasingCoordinator>();
        services.AddScoped<CustomersCoordinator>();
        services.AddScoped<SecurityCoordinator>();
        services.AddScoped<ReportingCoordinator>();
        services.AddScoped<OperationsCoordinator>();
        services.AddScoped<BrandingCoordinator>();
        services.AddScoped<DashboardCoordinator>();
        return services;
    }
}
