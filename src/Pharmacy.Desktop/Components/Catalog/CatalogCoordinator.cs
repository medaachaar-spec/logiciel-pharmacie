using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Catalog.Models;
using Pharmacy.Application.Catalog.Queries;
using Pharmacy.Domain.Common;

namespace Pharmacy.Desktop.Components.Catalog;

public sealed class CatalogCoordinator
{
    private readonly SearchCatalogProductsHandler searchHandler;
    private readonly GetCatalogProductByIdHandler detailsHandler;
    private readonly ListCatalogCategoriesHandler categoriesHandler;
    private readonly CreateCatalogProductHandler createProductHandler;
    private readonly UpdateCatalogProductHandler updateProductHandler;
    private readonly SetCatalogProductActivationHandler activationHandler;
    private readonly AddCatalogBarcodeHandler addBarcodeHandler;
    private readonly UpdateCatalogBarcodeHandler updateBarcodeHandler;
    private readonly ChangeCatalogProductPriceHandler changePriceHandler;
    private readonly CreateCatalogCategoryHandler createCategoryHandler;
    private readonly UpdateCatalogCategoryHandler updateCategoryHandler;
    private readonly SetCatalogCategoryActivationHandler categoryActivationHandler;
    private int operationInProgress;

    public CatalogCoordinator(
        SearchCatalogProductsHandler searchHandler,
        GetCatalogProductByIdHandler detailsHandler,
        ListCatalogCategoriesHandler categoriesHandler,
        CreateCatalogProductHandler createProductHandler,
        UpdateCatalogProductHandler updateProductHandler,
        SetCatalogProductActivationHandler activationHandler,
        AddCatalogBarcodeHandler addBarcodeHandler,
        UpdateCatalogBarcodeHandler updateBarcodeHandler,
        ChangeCatalogProductPriceHandler changePriceHandler,
        CreateCatalogCategoryHandler createCategoryHandler,
        UpdateCatalogCategoryHandler updateCategoryHandler,
        SetCatalogCategoryActivationHandler categoryActivationHandler)
    {
        this.searchHandler = searchHandler;
        this.detailsHandler = detailsHandler;
        this.categoriesHandler = categoriesHandler;
        this.createProductHandler = createProductHandler;
        this.updateProductHandler = updateProductHandler;
        this.activationHandler = activationHandler;
        this.addBarcodeHandler = addBarcodeHandler;
        this.updateBarcodeHandler = updateBarcodeHandler;
        this.changePriceHandler = changePriceHandler;
        this.createCategoryHandler = createCategoryHandler;
        this.updateCategoryHandler = updateCategoryHandler;
        this.categoryActivationHandler = categoryActivationHandler;
    }

    public IReadOnlyList<CatalogProductListItem> Products { get; private set; } =
        Array.Empty<CatalogProductListItem>();

    public IReadOnlyList<CatalogCategoryView> Categories { get; private set; } =
        Array.Empty<CatalogCategoryView>();

    public CatalogProductDetails? SelectedProduct { get; private set; }

    public bool IsBusy => Volatile.Read(ref operationInProgress) != 0;

    public bool IsInitialized { get; private set; }

    public string? Message { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? SearchText { get; private set; }

    public CatalogProductStatusFilter StatusFilter { get; private set; }

    public long? CategoryFilter { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            await LoadCategoriesCoreAsync(cancellationToken);
            await SearchCoreAsync(null, CatalogProductStatusFilter.All, null, cancellationToken);
            IsInitialized = true;
        }
        finally
        {
            End();
        }
    }

    public async Task SearchAsync(
        string? text,
        CatalogProductStatusFilter status,
        long? categoryId,
        CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            await SearchCoreAsync(text, status, categoryId, cancellationToken);
        }
        finally
        {
            End();
        }
    }

    public async Task SelectAsync(long productId, CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var result = await detailsHandler.HandleAsync(
                new GetCatalogProductByIdQuery(productId),
                cancellationToken);
            ApplyProductResult(result, "Fiche produit chargée.");
        }
        finally
        {
            End();
        }
    }

    public Task CreateProductAsync(
        CreateCatalogProductCommand command,
        CancellationToken cancellationToken = default) =>
        RunProductMutationAsync(
            token => createProductHandler.HandleAsync(command, token),
            "Produit synthétique créé sans stock.",
            cancellationToken);

    public Task UpdateProductAsync(
        UpdateCatalogProductCommand command,
        CancellationToken cancellationToken = default) =>
        RunProductMutationAsync(
            token => updateProductHandler.HandleAsync(command, token),
            "Identité et paramètres locaux enregistrés.",
            cancellationToken);

    public Task SetActivationAsync(
        SetCatalogProductActivationCommand command,
        CancellationToken cancellationToken = default) =>
        RunProductMutationAsync(
            token => activationHandler.HandleAsync(command, token),
            command.IsActive && command.IsLocallyActive ? "Produit réactivé." : "Produit désactivé.",
            cancellationToken);

    public Task AddBarcodeAsync(
        AddCatalogBarcodeCommand command,
        CancellationToken cancellationToken = default) =>
        RunProductMutationAsync(
            token => addBarcodeHandler.HandleAsync(command, token),
            "Code-barres ajouté.",
            cancellationToken);

    public Task UpdateBarcodeAsync(
        UpdateCatalogBarcodeCommand command,
        CancellationToken cancellationToken = default) =>
        RunProductMutationAsync(
            token => updateBarcodeHandler.HandleAsync(command, token),
            "Code-barres mis à jour.",
            cancellationToken);

    public Task ChangePriceAsync(
        ChangeCatalogProductPriceCommand command,
        CancellationToken cancellationToken = default) =>
        RunProductMutationAsync(
            token => changePriceHandler.HandleAsync(command, token),
            "Prix modifié et historique ajouté atomiquement.",
            cancellationToken);

    public async Task CreateCategoryAsync(
        CreateCatalogCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var result = await createCategoryHandler.HandleAsync(command, cancellationToken);

            if (result.IsFailure)
            {
                ApplyFailure(result.Error);
                return;
            }

            await LoadCategoriesCoreAsync(cancellationToken);
            Message = "Catégorie synthétique créée.";
        }
        finally
        {
            End();
        }
    }

    public async Task UpdateCategoryAsync(
        UpdateCatalogCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var result = await updateCategoryHandler.HandleAsync(command, cancellationToken);

            if (result.IsFailure)
            {
                ApplyFailure(result.Error);
                return;
            }

            await LoadCategoriesCoreAsync(cancellationToken);
            Message = "Catégorie modifiée.";
        }
        finally
        {
            End();
        }
    }

    public async Task SetCategoryActivationAsync(
        SetCatalogCategoryActivationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var result = await categoryActivationHandler.HandleAsync(command, cancellationToken);

            if (result.IsFailure)
            {
                ApplyFailure(result.Error);
                return;
            }

            await LoadCategoriesCoreAsync(cancellationToken);
            Message = command.IsActive ? "Catégorie réactivée." : "Catégorie désactivée sans suppression.";
        }
        finally
        {
            End();
        }
    }

    public void ClearFeedback()
    {
        Message = null;
        ErrorCode = null;
    }

    private async Task RunProductMutationAsync(
        Func<CancellationToken, Task<Result<CatalogProductDetails>>> operation,
        string successMessage,
        CancellationToken cancellationToken)
    {
        if (!TryBegin())
        {
            return;
        }

        try
        {
            ClearFeedback();
            var result = await operation(cancellationToken);

            if (!ApplyProductResult(result, successMessage))
            {
                return;
            }

            await SearchCoreAsync(SearchText, StatusFilter, CategoryFilter, cancellationToken);
        }
        finally
        {
            End();
        }
    }

    private async Task SearchCoreAsync(
        string? text,
        CatalogProductStatusFilter status,
        long? categoryId,
        CancellationToken cancellationToken)
    {
        SearchText = text?.Trim();
        StatusFilter = status;
        CategoryFilter = categoryId;
        var result = await searchHandler.HandleAsync(
            new SearchCatalogProductsQuery(SearchText, status, categoryId, 50),
            cancellationToken);

        if (result.IsFailure)
        {
            Products = Array.Empty<CatalogProductListItem>();
            ApplyFailure(result.Error);
            return;
        }

        Products = result.Value;
    }

    private async Task LoadCategoriesCoreAsync(CancellationToken cancellationToken)
    {
        var result = await categoriesHandler.HandleAsync(cancellationToken);

        if (result.IsFailure)
        {
            Categories = Array.Empty<CatalogCategoryView>();
            ApplyFailure(result.Error);
            return;
        }

        Categories = result.Value;
    }

    private bool ApplyProductResult(Result<CatalogProductDetails> result, string successMessage)
    {
        if (result.IsFailure)
        {
            ApplyFailure(result.Error);
            return false;
        }

        SelectedProduct = result.Value;
        Message = successMessage;
        return true;
    }

    private void ApplyFailure(Error error)
    {
        ErrorCode = error.Code;
        Message = error.Code switch
        {
            "catalog.database.unavailable" =>
                "Catalogue SQL indisponible. Le shell reste utilisable ; vérifiez Diagnostic et la configuration locale.",
            "catalog.product.concurrency_conflict" or "catalog.category.concurrency_conflict" =>
                "Ces données ont changé depuis leur ouverture. Rechargez-les et vérifiez les changements avant de recommencer.",
            "catalog.reference.duplicate" => "Cette référence interne existe déjà.",
            "catalog.barcode.duplicate" => "Ce code-barres existe déjà ; aucun transfert automatique n’a été effectué.",
            "catalog.price.invalid" => "Le prix doit être supérieur ou égal à zéro.",
            _ => error.Message
        };
    }

    private bool TryBegin()
    {
        if (Interlocked.CompareExchange(ref operationInProgress, 1, 0) == 0)
        {
            return true;
        }

        ErrorCode = "catalog.operation.busy";
        Message = "Une opération Catalogue est déjà en cours.";
        return false;
    }

    private void End() => Volatile.Write(ref operationInProgress, 0);
}
