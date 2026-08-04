using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Inventory.Contracts;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Application.Sales.Models;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Inventory;

public sealed class SqlInventoryStockAvailabilityReader : IStockAvailabilityReader
{
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly IInventoryDateProvider dateProvider;

    public SqlInventoryStockAvailabilityReader(
        IDbContextFactory<PharmacyDbContext> contextFactory,
        IInventoryDateProvider dateProvider)
    {
        this.contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        this.dateProvider = dateProvider ?? throw new ArgumentNullException(nameof(dateProvider));
    }

    public async Task<Result<StockAvailability>> GetAvailabilityAsync(
        ProductId productId,
        SaleQuantity requestedQuantity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(productId);
        ArgumentNullException.ThrowIfNull(requestedQuantity);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var currentDate = dateProvider.CurrentDate;
            var productExists = await context.ProductLocalSettings.AsNoTracking().AnyAsync(settings =>
                settings.ProductId == productId.Value
                && settings.Product.IsActive
                && settings.IsActive
                && settings.IsSellable,
                cancellationToken);

            if (!productExists)
            {
                return Result<StockAvailability>.Success(new StockAvailability(productId, 0, false));
            }

            var available = await context.StockBalances.AsNoTracking()
                .Where(balance => balance.ProductId == productId.Value
                    && balance.AvailableQuantity > 0
                    && balance.StockLocation.IsActive
                    && (balance.StockBatch == null || (balance.StockBatch.IsActive
                        && (balance.StockBatch.ExpiryDate == null || balance.StockBatch.ExpiryDate >= currentDate))))
                .SumAsync(balance => balance.AvailableQuantity, cancellationToken);
            return Result<StockAvailability>.Success(new StockAvailability(
                productId,
                available,
                available >= requestedQuantity.Value));
        }
        catch (Exception exception) when (InventorySqlFailure.IsDatabaseUnavailable(exception))
        {
            return Result<StockAvailability>.Failure(InventoryErrors.DatabaseUnavailable);
        }
    }
}
