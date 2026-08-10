using Microsoft.Extensions.DependencyInjection;
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
using Pharmacy.Desktop.Components.Pos;
using Pharmacy.Desktop.Components.Catalog;
using Pharmacy.Desktop.Components.Inventory;
using Pharmacy.Desktop.Components.Purchasing;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Purchasing;
using Pharmacy.Infrastructure.Sales.InMemory;
using Pharmacy.Infrastructure.Sales;

namespace Pharmacy.Desktop;

public static class SalesServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogBackedSales(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSqlCatalogAdaptersFromEnvironment();
        services.AddSqlInventoryAdapters();
        services.AddSqlPurchasingAdapters();
        services.AddSqlSalesAdapters();
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
        services.AddTransient<PrepareSaleFinalizationHandler>();
        services.AddTransient<FinalizeSaleHandler>();
        services.AddTransient<GetSaleByIdHandler>();
        services.AddTransient<GetRecentSalesHandler>();

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

        services.AddScoped<DemoTicketPrintService>();
        services.AddScoped<SalesPosCoordinator>();
        services.AddScoped<CatalogCoordinator>();
        services.AddScoped<InventoryCoordinator>();
        services.AddScoped<PurchasingCoordinator>();
        return services;
    }
}
