using Pharmacy.Application.Catalog.Contracts;
using Pharmacy.Application.Catalog.Errors;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Domain.Common;

namespace Pharmacy.Application.Catalog.Commands;

public sealed class CreateCatalogProductHandler
{
    private readonly ICatalogProductStore store;

    public CreateCatalogProductHandler(ICatalogProductStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<Result<CatalogProductDetails>> HandleAsync(
        CreateCatalogProductCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var validation = CatalogCommandValidation.ValidateProductInput(
            command.Name,
            command.InternalCode,
            command.CategoryId,
            command.InitialSalePrice,
            command.QuantityScale,
            command.MinimumStock,
            command.MaximumStock);
        return validation.IsFailure
            ? Task.FromResult(Result<CatalogProductDetails>.Failure(validation.Error))
            : store.CreateAsync(command, cancellationToken);
    }
}

public sealed class UpdateCatalogProductHandler
{
    private readonly ICatalogProductStore store;

    public UpdateCatalogProductHandler(ICatalogProductStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<Result<CatalogProductDetails>> HandleAsync(
        UpdateCatalogProductCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var validation = CatalogCommandValidation.ValidateProductInput(
            command.Name,
            command.InternalCode,
            command.CategoryId,
            null,
            command.QuantityScale,
            command.MinimumStock,
            command.MaximumStock);
        return command.ProductId <= 0 || validation.IsFailure || !CatalogCommandValidation.ValidToken(command.ProductRowVersion)
            || !CatalogCommandValidation.ValidToken(command.LocalSettingsRowVersion)
            ? Task.FromResult(Result<CatalogProductDetails>.Failure(
                validation.IsFailure ? validation.Error : CatalogCommandValidation.InvalidInput))
            : store.UpdateAsync(command, cancellationToken);
    }
}

public sealed class SetCatalogProductActivationHandler
{
    private readonly ICatalogProductStore store;

    public SetCatalogProductActivationHandler(ICatalogProductStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<Result<CatalogProductDetails>> HandleAsync(
        SetCatalogProductActivationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.ProductId <= 0 || !CatalogCommandValidation.ValidToken(command.ProductRowVersion)
            || !CatalogCommandValidation.ValidToken(command.LocalSettingsRowVersion)
            ? Task.FromResult(Result<CatalogProductDetails>.Failure(CatalogCommandValidation.InvalidInput))
            : store.SetActivationAsync(command, cancellationToken);
    }
}

public sealed class AddCatalogBarcodeHandler
{
    private readonly ICatalogProductStore store;

    public AddCatalogBarcodeHandler(ICatalogProductStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<Result<CatalogProductDetails>> HandleAsync(
        AddCatalogBarcodeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.ProductId <= 0 || string.IsNullOrWhiteSpace(command.Value) || command.Value.Trim().Length > 128
            ? Task.FromResult(Result<CatalogProductDetails>.Failure(CatalogCommandValidation.InvalidInput))
            : store.AddBarcodeAsync(command, cancellationToken);
    }
}

public sealed class UpdateCatalogBarcodeHandler
{
    private readonly ICatalogProductStore store;

    public UpdateCatalogBarcodeHandler(ICatalogProductStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<Result<CatalogProductDetails>> HandleAsync(
        UpdateCatalogBarcodeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.ProductId <= 0 || command.BarcodeId <= 0 || string.IsNullOrWhiteSpace(command.Value)
            || command.Value.Trim().Length > 128 || !CatalogCommandValidation.ValidToken(command.RowVersion)
            ? Task.FromResult(Result<CatalogProductDetails>.Failure(CatalogCommandValidation.InvalidInput))
            : store.UpdateBarcodeAsync(command, cancellationToken);
    }
}

public sealed class ChangeCatalogProductPriceHandler
{
    private readonly ICatalogProductStore store;

    public ChangeCatalogProductPriceHandler(ICatalogProductStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<Result<CatalogProductDetails>> HandleAsync(
        ChangeCatalogProductPriceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.NewPrice < 0m)
        {
            return Task.FromResult(Result<CatalogProductDetails>.Failure(CatalogApplicationErrors.InvalidPrice));
        }

        return command.ProductId <= 0 || !CatalogCommandValidation.ValidToken(command.LocalSettingsRowVersion)
            ? Task.FromResult(Result<CatalogProductDetails>.Failure(CatalogCommandValidation.InvalidInput))
            : store.ChangePriceAsync(command, cancellationToken);
    }
}

public sealed class CreateCatalogCategoryHandler
{
    private readonly ICatalogCategoryStore store;

    public CreateCatalogCategoryHandler(ICatalogCategoryStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<Result<CatalogCategoryView>> HandleAsync(
        CreateCatalogCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !CatalogCommandValidation.ValidCategory(command.Code, command.Name, command.SortOrder)
            ? Task.FromResult(Result<CatalogCategoryView>.Failure(CatalogCommandValidation.InvalidInput))
            : store.CreateAsync(command, cancellationToken);
    }
}

public sealed class UpdateCatalogCategoryHandler
{
    private readonly ICatalogCategoryStore store;

    public UpdateCatalogCategoryHandler(ICatalogCategoryStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<Result<CatalogCategoryView>> HandleAsync(
        UpdateCatalogCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.CategoryId <= 0 || !CatalogCommandValidation.ValidCategory(command.Code, command.Name, command.SortOrder)
            || !CatalogCommandValidation.ValidToken(command.RowVersion)
            ? Task.FromResult(Result<CatalogCategoryView>.Failure(CatalogCommandValidation.InvalidInput))
            : store.UpdateAsync(command, cancellationToken);
    }
}

public sealed class SetCatalogCategoryActivationHandler
{
    private readonly ICatalogCategoryStore store;

    public SetCatalogCategoryActivationHandler(ICatalogCategoryStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<Result<CatalogCategoryView>> HandleAsync(
        SetCatalogCategoryActivationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.CategoryId <= 0 || !CatalogCommandValidation.ValidToken(command.RowVersion)
            ? Task.FromResult(Result<CatalogCategoryView>.Failure(CatalogCommandValidation.InvalidInput))
            : store.SetActivationAsync(command, cancellationToken);
    }
}

internal static class CatalogCommandValidation
{
    public static Error InvalidInput => CatalogApplicationErrors.ValidationFailed(
        "Les informations saisies sont invalides ou incomplètes.");

    public static Result ValidateProductInput(
        string? name,
        string? internalCode,
        long categoryId,
        decimal? price,
        byte quantityScale,
        decimal? minimumStock,
        decimal? maximumStock)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 256
            || string.IsNullOrWhiteSpace(internalCode) || internalCode.Trim().Length > 128
            || categoryId <= 0 || quantityScale > 3
            || minimumStock < 0m || maximumStock < 0m
            || (minimumStock is not null && maximumStock is not null && minimumStock > maximumStock))
        {
            return Result.Failure(InvalidInput);
        }

        return price < 0m
            ? Result.Failure(CatalogApplicationErrors.InvalidPrice)
            : Result.Success();
    }

    public static bool ValidCategory(string? code, string? name, int sortOrder) =>
        !string.IsNullOrWhiteSpace(code) && code.Trim().Length <= 64
        && !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 160
        && sortOrder >= 0;

    public static bool ValidToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(token).Length == 8;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
