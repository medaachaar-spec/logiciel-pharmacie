using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Application.Sales.Catalog.Queries;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Drafts.Commands;
using Pharmacy.Application.Sales.Drafts.Queries;
using Pharmacy.Application.Sales.Finalization.Commands;
using Pharmacy.Desktop.Components.Pos;
using Pharmacy.Infrastructure.Sales.InMemory;

namespace Pharmacy.Desktop;

public static class SalesServiceCollectionExtensions
{
    public static IServiceCollection AddInMemorySalesAdapters(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IProductCatalogReader, InMemoryProductCatalogReader>();
        services.AddSingleton<IStockAvailabilityReader, InMemoryStockAvailabilityReader>();
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

        services.AddScoped<DemoTicketPrintService>();
        services.AddScoped<SalesPosCoordinator>();
        return services;
    }
}
