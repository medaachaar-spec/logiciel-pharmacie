using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Operations.Contracts;
using Pharmacy.Application.Operations.Errors;
using Pharmacy.Application.Operations.Models;
using Pharmacy.Application.Security;
using Pharmacy.Application.Security.Contracts;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Inventory;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Persistence.Entities.Catalog;
using Pharmacy.Infrastructure.Persistence.Entities.Inventory;
using Pharmacy.Infrastructure.Persistence.Entities.Operations;

namespace Pharmacy.Infrastructure.Operations;

public sealed class SqlControlledImportService : IControlledImportService
{
    private const int MaximumBytes = 25 * 1024 * 1024;
    private const int MaximumRows = 100_000;
    private readonly IDbContextFactory<PharmacyDbContext> contextFactory;
    private readonly IPermissionAuthorizer authorizer;
    private readonly IBusinessAuditRecorder audit;
    private readonly ISecurityClock clock;

    public SqlControlledImportService(IDbContextFactory<PharmacyDbContext> contextFactory, IPermissionAuthorizer authorizer, IBusinessAuditRecorder audit, ISecurityClock clock)
    { this.contextFactory = contextFactory; this.authorizer = authorizer; this.audit = audit; this.clock = clock; }

    public async Task<Result<ImportPreviewView>> PreviewAsync(PreviewImportRequest request, CancellationToken cancellationToken = default)
    {
        var denied = authorizer.Authorize(SecurityPermissions.ImportView);
        if (denied.IsFailure) return Result<ImportPreviewView>.Failure(denied.Error);
        return await Task.Run(() => Parse(request, includeAllRows: false), cancellationToken);
    }

    public async Task<Result<ImportReportView>> ConfirmAsync(ConfirmImportRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty) return Result<ImportReportView>.Failure(OperationsErrors.InvalidOperationId);
        var denied = authorizer.Authorize(SecurityPermissions.ImportExecute, true);
        if (denied.IsFailure) { await AuditFailureQuietlyAsync(request.OperationId, denied.Error.Code, cancellationToken); return Result<ImportReportView>.Failure(denied.Error); }
        var parsed = await Task.Run(
            () => Parse(new PreviewImportRequest(request.FileName, request.Content, request.Type, request.Mapping, MaximumRows), includeAllRows: true),
            cancellationToken);
        if (parsed.IsFailure) return Result<ImportReportView>.Failure(parsed.Error);
        if (!parsed.Value.CanConfirm) { await AuditFailureQuietlyAsync(request.OperationId, OperationsErrors.ImportValidationFailed.Code, cancellationToken); return Result<ImportReportView>.Failure(OperationsErrors.ImportValidationFailed); }
        var fingerprint = Fingerprint(request.OperationId, parsed.Value.Sha256, request.Type, request.Mapping);

        try
        {
            await using var strategyContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            var strategy = strategyContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
                await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
                try
                {
                    var replay = await context.ImportJobs.AsNoTracking().SingleOrDefaultAsync(job => job.OperationId == request.OperationId, cancellationToken);
                    if (replay is not null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        if (!string.Equals(replay.Fingerprint, fingerprint, StringComparison.Ordinal))
                        {
                            await AuditFailureQuietlyAsync(request.OperationId, "import.idempotence.conflict", cancellationToken);
                            return Result<ImportReportView>.Failure(new Error("import.idempotence.conflict", "Cet OperationId a déjà été utilisé avec un contenu différent."));
                        }
                        return Result<ImportReportView>.Success(MapReport(replay, Array.Empty<ImportIssueView>(), true));
                    }

                    var issues = new List<ImportIssueView>(); var created = 0; var ignored = 0;
                    if (request.Type == ControlledImportType.Catalogue)
                        (created, ignored) = await ImportCatalogueAsync(context, parsed.Value.Rows, request.Mapping, issues, cancellationToken);
                    else
                        (created, ignored) = await ImportStockAsync(context, parsed.Value.Rows, request.Type, request.Mapping, issues, request.OperationId, cancellationToken);
                    if (issues.Any(issue => issue.Severity == ImportIssueSeverity.Error))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        await AuditFailureQuietlyAsync(request.OperationId, OperationsErrors.ImportValidationFailed.Code, cancellationToken);
                        return Result<ImportReportView>.Failure(OperationsErrors.ImportValidationFailed);
                    }
                    var now = clock.UtcNow;
                    var job = new ImportJobEntity
                    {
                        OperationId = request.OperationId, Fingerprint = fingerprint, ImportType = request.Type.ToString(), Status = "Completed",
                        SourceFileName = parsed.Value.SafeFileName, SourceFileHash = parsed.Value.Sha256, ReadCount = parsed.Value.TotalRows,
                        CreatedCount = created, UpdatedCount = 0, IgnoredCount = ignored, RejectedCount = 0, StartedAtUtc = now, CompletedAtUtc = now
                    };
                    context.ImportJobs.Add(job);
                    await context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    await audit.RecordSuccessAsync("import", "ImportJob", job.Id.ToString(), "import.confirm", request.OperationId, cancellationToken);
                    return Result<ImportReportView>.Success(MapReport(job, issues, false));
                }
                catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
                {
                    try { await transaction.RollbackAsync(cancellationToken); } catch { }
                    await AuditFailureQuietlyAsync(request.OperationId, OperationsErrors.DatabaseUnavailable.Code, cancellationToken);
                    return Result<ImportReportView>.Failure(OperationsErrors.DatabaseUnavailable);
                }
                catch (DbUpdateException)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    await AuditFailureQuietlyAsync(request.OperationId, "import.persistence.conflict", cancellationToken);
                    return Result<ImportReportView>.Failure(new Error("import.persistence.conflict", "Une collision catalogue/stock a annulé l’import complet."));
                }
            });
        }
        catch (Exception exception) when (CatalogSqlFailure.IsDatabaseUnavailable(exception))
        {
            await AuditFailureQuietlyAsync(request.OperationId, OperationsErrors.DatabaseUnavailable.Code, cancellationToken);
            return Result<ImportReportView>.Failure(OperationsErrors.DatabaseUnavailable);
        }
    }

    private static Result<ImportPreviewView> Parse(PreviewImportRequest request, bool includeAllRows)
    {
        if (request is null || request.Content is null || request.Content.Length == 0 || request.Content.Length > MaximumBytes
            || !string.Equals(Path.GetExtension(request.FileName), ".csv", StringComparison.OrdinalIgnoreCase)
            || !IsSafeLogicalFileName(request.FileName))
            return Result<ImportPreviewView>.Failure(OperationsErrors.InvalidImportFile);
        string text;
        try { text = new UTF8Encoding(false, true).GetString(request.Content).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { return Result<ImportPreviewView>.Failure(OperationsErrors.InvalidImportFile); }
        var delimiter = DetectDelimiter(text);
        var records = ParseCsv(text, delimiter);
        if (records.Count < 2 || records.Count - 1 > MaximumRows) return Result<ImportPreviewView>.Failure(OperationsErrors.InvalidImportFile);
        var headers = records[0].Select(value => value.Trim()).ToArray();
        if (headers.Any(string.IsNullOrWhiteSpace) || headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            return Result<ImportPreviewView>.Failure(OperationsErrors.InvalidImportMapping);
        var mapped = MappedColumns(request.Mapping).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        var required = RequiredColumns(request.Type, request.Mapping).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (required.Distinct(StringComparer.OrdinalIgnoreCase).Count() != required.Length
            || mapped.Distinct(StringComparer.OrdinalIgnoreCase).Count() != mapped.Length
            || required.Any(name => !headers.Contains(name!, StringComparer.OrdinalIgnoreCase)))
            return Result<ImportPreviewView>.Failure(OperationsErrors.InvalidImportMapping);
        var index = headers.Select((name, position) => (name, position)).ToDictionary(x => x.name, x => x.position, StringComparer.OrdinalIgnoreCase);
        var issues = new List<ImportIssueView>(); var rows = new List<ImportPreviewRow>(); var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var barcodes = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i < records.Count; i++)
        {
            var values = records[i]; var rowNumber = i + 1;
            if (values.All(string.IsNullOrWhiteSpace)) { issues.Add(Issue(rowNumber, "import.row.empty", ImportIssueSeverity.Warning, null, "Ligne vide ignorée.")); continue; }
            if (values.Count != headers.Length) issues.Add(Issue(rowNumber, "import.row.column_count", ImportIssueSeverity.Error, null, "Le nombre de cellules ne correspond pas aux en-têtes."));
            foreach (var value in values)
                if (StartsFormula(value.Trim())) issues.Add(Issue(rowNumber, "import.formula.detected", ImportIssueSeverity.Error, null, "Toute cellule commençant par un marqueur de formule est interdite."));
            string Get(string? column) => string.IsNullOrWhiteSpace(column) || !index.TryGetValue(column, out var p) || p >= values.Count ? string.Empty : values[p].Trim();
            var reference = Get(request.Mapping.ReferenceColumn); var name = Get(request.Mapping.DesignationColumn); var barcode = NullIfEmpty(Get(request.Mapping.BarcodeColumn));
            if (reference.Length == 0) issues.Add(Issue(rowNumber, "import.reference.required", ImportIssueSeverity.Error, request.Mapping.ReferenceColumn, "Référence obligatoire."));
            if (name.Length == 0) issues.Add(Issue(rowNumber, "import.designation.required", ImportIssueSeverity.Error, request.Mapping.DesignationColumn, "Désignation obligatoire."));
            if (reference.Length > 128 || name.Length > 256) issues.Add(Issue(rowNumber, "import.text.too_long", ImportIssueSeverity.Error, null, "Référence ou désignation trop longue."));
            if (reference.Length > 0 && !references.Add(reference)) issues.Add(Issue(rowNumber, "import.reference.duplicate_file", ImportIssueSeverity.Error, request.Mapping.ReferenceColumn, "Référence dupliquée dans le fichier."));
            if (barcode is not null && (!barcode.All(char.IsDigit) || barcode.Length is < 8 or > 14)) issues.Add(Issue(rowNumber, "import.barcode.invalid", ImportIssueSeverity.Error, request.Mapping.BarcodeColumn, "Code-barres attendu : 8 à 14 chiffres."));
            if (barcode is not null && !barcodes.Add(BarcodeNormalizer.Normalize(barcode))) issues.Add(Issue(rowNumber, "import.barcode.duplicate_file", ImportIssueSeverity.Error, request.Mapping.BarcodeColumn, "Code-barres dupliqué dans le fichier."));
            var salePrice = ParseDecimal(Get(request.Mapping.SalePriceColumn), rowNumber, request.Mapping.SalePriceColumn, issues);
            var purchasePrice = ParseDecimal(Get(request.Mapping.PurchasePriceColumn), rowNumber, request.Mapping.PurchasePriceColumn, issues);
            if (!string.IsNullOrWhiteSpace(request.Mapping.PurchasePriceColumn) && purchasePrice.HasValue)
                issues.Add(Issue(rowNumber, "import.pph.source_only", ImportIssueSeverity.Warning, request.Mapping.PurchasePriceColumn, "PPH conservé en prévisualisation uniquement : aucun champ cible validé ne permet sa persistance."));
            var quantity = ParseInt(Get(request.Mapping.QuantityColumn), rowNumber, request.Mapping.QuantityColumn, issues);
            if (quantity < 0) issues.Add(Issue(rowNumber, "import.quantity.negative", ImportIssueSeverity.Error, request.Mapping.QuantityColumn, "Quantité négative interdite."));
            var expiry = ParseDate(Get(request.Mapping.ExpiryColumn), rowNumber, request.Mapping.ExpiryColumn, issues);
            var lot = NullIfEmpty(Get(request.Mapping.LotColumn));
            if (request.Type != ControlledImportType.Catalogue && (!quantity.HasValue || quantity <= 0)) issues.Add(Issue(rowNumber, "import.quantity.required", ImportIssueSeverity.Error, request.Mapping.QuantityColumn, "Quantité strictement positive requise pour le stock."));
            if (request.Type == ControlledImportType.Lots && string.IsNullOrWhiteSpace(lot)) issues.Add(Issue(rowNumber, "import.lot.required", ImportIssueSeverity.Error, request.Mapping.LotColumn, "Numéro de lot explicite requis."));
            if (request.Type == ControlledImportType.Catalogue && !salePrice.HasValue)
                issues.Add(Issue(rowNumber, "import.price.absent", ImportIssueSeverity.Warning, request.Mapping.SalePriceColumn, "PPV absent : le produit sera créé non vendable à prix technique zéro."));
            rows.Add(new(rowNumber, reference, name, salePrice, purchasePrice, barcode, NullIfEmpty(Get(request.Mapping.CategoryColumn)) ?? request.Mapping.DefaultCategoryCode, quantity, lot, expiry, request.Type == ControlledImportType.Catalogue ? "Créer si absent" : "Créer mouvement si stock initial absent"));
        }
        var previewRows = includeAllRows ? rows.ToArray() : rows.Take(Math.Clamp(request.PreviewLimit, 1, 1000)).ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(request.Content));
        return Result<ImportPreviewView>.Success(new ImportPreviewView(Path.GetFileName(request.FileName), hash, request.Type, rows.Count, headers, previewRows, issues.AsReadOnly(), !issues.Any(issue => issue.Severity == ImportIssueSeverity.Error)));
    }

    private static async Task<(int Created, int Ignored)> ImportCatalogueAsync(PharmacyDbContext context, IReadOnlyList<ImportPreviewRow> rows, CsvImportMapping mapping, List<ImportIssueView> issues, CancellationToken token)
    {
        var refs = rows.Select(row => row.Reference).ToArray();
        var existing = await context.ProductLocalSettings.Where(x => x.InternalCode != null && refs.Contains(x.InternalCode)).Select(x => x.InternalCode!).ToHashSetAsync(StringComparer.OrdinalIgnoreCase, token);
        var categoryCodes = rows.Select(row => row.CategoryCode).Where(code => !string.IsNullOrWhiteSpace(code)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var categories = await context.ProductCategories.Where(c => categoryCodes.Contains(c.Code)).ToDictionaryAsync(c => c.Code, StringComparer.OrdinalIgnoreCase, token);
        var barcodeValues = rows.Where(row => row.Barcode is not null).Select(row => BarcodeNormalizer.Normalize(row.Barcode!)).ToArray();
        var collisions = await context.ProductBarcodes.Where(b => barcodeValues.Contains(b.NormalizedValue)).Select(b => b.NormalizedValue).ToHashSetAsync(StringComparer.Ordinal, token);
        var created = 0; var ignored = 0; var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            if (existing.Contains(row.Reference)) { ignored++; issues.Add(Issue(row.RowNumber, "import.reference.exists", ImportIssueSeverity.Warning, mapping.ReferenceColumn, "Référence existante ignorée en mode CreateOnly.")); continue; }
            if (row.CategoryCode is null || !categories.TryGetValue(row.CategoryCode, out var category)) { issues.Add(Issue(row.RowNumber, "import.category.not_found", ImportIssueSeverity.Error, mapping.CategoryColumn, "Catégorie explicite ou par défaut introuvable.")); continue; }
            if (row.Barcode is not null && collisions.Contains(BarcodeNormalizer.Normalize(row.Barcode))) { issues.Add(Issue(row.RowNumber, "import.barcode.collision", ImportIssueSeverity.Error, mapping.BarcodeColumn, "Code-barres déjà attribué dans le catalogue.")); continue; }
            var product = new ProductEntity { Name = row.Designation, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
            product.LocalSettings = new ProductLocalSettingsEntity
            {
                Product = product, InternalCode = row.Reference, CategoryId = category.Id, IsActive = true,
                IsSellable = row.SalePrice.HasValue, IsPurchasable = true, TracksLots = category.DefaultTracksLots,
                TracksExpiry = category.DefaultTracksExpiry, DiscountAllowed = category.DefaultDiscountAllowed,
                QuantityScale = 0, CurrentSalePrice = row.SalePrice ?? 0m, CurrencyCode = "MAD"
            };
            if (row.SalePrice.HasValue) product.PriceHistory.Add(new ProductPriceHistoryEntity { Product = product, NewPrice = row.SalePrice.Value, CurrencyCode = "MAD", ChangeOrigin = "SupplierImport", Reason = "Import contrôlé Sprint 17", EffectiveAtUtc = now });
            if (row.Barcode is not null) product.Barcodes.Add(new ProductBarcodeEntity { Product = product, Value = row.Barcode, NormalizedValue = BarcodeNormalizer.Normalize(row.Barcode), IsPrimary = true, BarcodeType = "SupplierImport", IsActive = true, CreatedAtUtc = now });
            context.Products.Add(product); created++;
        }
        return (created, ignored);
    }

    private static async Task<(int Created, int Ignored)> ImportStockAsync(PharmacyDbContext context, IReadOnlyList<ImportPreviewRow> rows, ControlledImportType type, CsvImportMapping mapping, List<ImportIssueView> issues, Guid operationId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(mapping.StockLocationCode)) { issues.Add(Issue(0, "import.location.required", ImportIssueSeverity.Error, null, "Emplacement de stock explicite requis.")); return (0, 0); }
        var location = await context.StockLocations.SingleOrDefaultAsync(x => x.Code == mapping.StockLocationCode && x.IsActive, token);
        if (location is null) { issues.Add(Issue(0, "import.location.not_found", ImportIssueSeverity.Error, null, "Emplacement actif introuvable.")); return (0, 0); }
        var refs = rows.Select(row => row.Reference).ToArray();
        var products = await context.ProductLocalSettings.Include(x => x.Product).Where(x => x.InternalCode != null && refs.Contains(x.InternalCode)).ToDictionaryAsync(x => x.InternalCode!, StringComparer.OrdinalIgnoreCase, token);
        var created = 0; var ignored = 0; var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            if (!products.TryGetValue(row.Reference, out var settings)) { issues.Add(Issue(row.RowNumber, "import.product.not_found", ImportIssueSeverity.Error, mapping.ReferenceColumn, "Produit existant requis ; aucune création implicite depuis un import stock.")); continue; }
            if (type == ControlledImportType.OpeningStock && settings.TracksLots) { issues.Add(Issue(row.RowNumber, "import.stock.lot_required", ImportIssueSeverity.Error, mapping.ReferenceColumn, "Ce produit suit les lots ; utilisez le mode Lots.")); continue; }
            if (type == ControlledImportType.Lots && !settings.TracksLots) { issues.Add(Issue(row.RowNumber, "import.stock.lot_not_enabled", ImportIssueSeverity.Error, mapping.ReferenceColumn, "Le suivi des lots n’est pas activé pour ce produit.")); continue; }
            if (type == ControlledImportType.Lots && settings.TracksExpiry && row.ExpiryDate is null) { issues.Add(Issue(row.RowNumber, "import.expiry.required", ImportIssueSeverity.Error, mapping.ExpiryColumn, "Péremption explicite requise pour ce produit.")); continue; }
            StockBatchEntity? batch = null;
            if (type == ControlledImportType.Lots)
            {
                if (await context.StockBatches.AnyAsync(x => x.ProductId == settings.ProductId && x.BatchNumber == row.LotNumber, token)) { ignored++; issues.Add(Issue(row.RowNumber, "import.lot.exists", ImportIssueSeverity.Warning, mapping.LotColumn, "Lot existant ignoré ; aucun cumul silencieux.")); continue; }
                batch = new StockBatchEntity { CreationOperationId = Deterministic(operationId, row.RowNumber, "batch"), CreationFingerprint = $"IMPORT|{operationId:N}|{row.RowNumber}|BATCH", ProductId = settings.ProductId, BatchNumber = row.LotNumber, ExpiryDate = row.ExpiryDate, IsActive = true, CreatedAtUtc = now };
                context.StockBatches.Add(batch);
            }
            else if (await context.StockBalances.AnyAsync(x => x.StockLocationId == location.Id && x.ProductId == settings.ProductId && x.StockBatchId == null && x.AvailableQuantity > 0, token)) { ignored++; issues.Add(Issue(row.RowNumber, "import.stock.exists", ImportIssueSeverity.Warning, mapping.ReferenceColumn, "Stock existant ignoré ; l’ouverture ne cumule jamais silencieusement.")); continue; }
            var quantity = row.Quantity!.Value;
            var balance = new StockBalanceEntity { StockLocationId = location.Id, ProductId = settings.ProductId, StockBatch = batch, AvailableQuantity = quantity, UpdatedAtUtc = now };
            context.StockBalances.Add(balance);
            context.StockMovements.Add(new StockMovementEntity { OperationId = Deterministic(operationId, row.RowNumber, "movement"), OperationFingerprint = $"IMPORT|{operationId:N}|{row.RowNumber}|MOVEMENT", ProductId = settings.ProductId, StockLocationId = location.Id, StockBatch = batch, MovementType = StockMovementType.Opening, Quantity = quantity, QuantityBefore = 0, QuantityAfter = quantity, Reason = "Import stock initial contrôlé", SourceType = "ImportJob", SourceReference = operationId.ToString("N"), OccurredAtUtc = now, CreatedAtUtc = now });
            created++;
        }
        return (created, ignored);
    }

    private static IReadOnlyList<string?> RequiredColumns(ControlledImportType type, CsvImportMapping m) => type switch
    {
        ControlledImportType.Catalogue => [m.ReferenceColumn, m.DesignationColumn],
        ControlledImportType.OpeningStock => [m.ReferenceColumn, m.DesignationColumn, m.QuantityColumn],
        ControlledImportType.Lots => [m.ReferenceColumn, m.DesignationColumn, m.QuantityColumn, m.LotColumn],
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    private static IReadOnlyList<string?> MappedColumns(CsvImportMapping m) =>
        [m.ReferenceColumn, m.DesignationColumn, m.SalePriceColumn, m.PurchasePriceColumn, m.BarcodeColumn, m.CategoryColumn, m.QuantityColumn, m.LotColumn, m.ExpiryColumn];
    private static decimal? ParseDecimal(string value, int row, string? column, List<ImportIssueView> issues) { if (value.Length == 0) return null; if (StartsFormula(value)) { issues.Add(Issue(row,"import.formula.detected",ImportIssueSeverity.Error,column,"Formule interdite.")); return null; } const NumberStyles styles = NumberStyles.AllowDecimalPoint; if (decimal.TryParse(value, styles, CultureInfo.InvariantCulture, out var result) || decimal.TryParse(value, styles, CultureInfo.GetCultureInfo("fr-FR"), out result)) { if (result < 0) issues.Add(Issue(row,"import.amount.negative",ImportIssueSeverity.Error,column,"Montant négatif interdit.")); if (!FitsSqlDecimal19Scale4(result)) issues.Add(Issue(row,"import.amount.precision",ImportIssueSeverity.Error,column,"Montant incompatible avec la précision SQL decimal(19,4).")); return result; } issues.Add(Issue(row,"import.amount.invalid",ImportIssueSeverity.Error,column,"Montant invalide.")); return null; }
    private static int? ParseInt(string value, int row, string? column, List<ImportIssueView> issues) { if (value.Length == 0) return null; if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)) return result; issues.Add(Issue(row,"import.quantity.invalid",ImportIssueSeverity.Error,column,"Quantité entière invalide.")); return null; }
    private static DateOnly? ParseDate(string value, int row, string? column, List<ImportIssueView> issues) { if (value.Length == 0) return null; foreach (var format in new[]{"yyyy-MM-dd","dd/MM/yyyy"}) if (DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)) return result; issues.Add(Issue(row,"import.date.invalid",ImportIssueSeverity.Error,column,"Date attendue au format yyyy-MM-dd ou dd/MM/yyyy.")); return null; }
    private static ImportIssueView Issue(int row, string code, ImportIssueSeverity severity, string? column, string message) => new(row, code, severity, column, message);
    private static bool StartsFormula(string value) => value.Length > 0 && value[0] is '=' or '+' or '-' or '@';
    private static bool FitsSqlDecimal19Scale4(decimal value)
    {
        var scale = (decimal.GetBits(value)[3] >> 16) & 0x7F;
        return scale <= 4 && value is >= -999_999_999_999_999.9999m and <= 999_999_999_999_999.9999m;
    }
    private static bool IsSafeLogicalFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 260 || Path.GetFileName(fileName) != fileName) return false;
        return fileName.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) < 0;
    }
    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
    private static char DetectDelimiter(string text) { var first = text.Split(new[]{"\r\n","\n"}, StringSplitOptions.None)[0]; var semicolon = first.Count(c => c==';'); var comma=first.Count(c=>c==','); var tab=first.Count(c=>c=='\t'); return tab>semicolon && tab>comma?'\t':semicolon>=comma?';':','; }
    private static List<List<string>> ParseCsv(string text, char delimiter) { var rows=new List<List<string>>(); var row=new List<string>(); var cell=new StringBuilder(); var quoted=false; for(var i=0;i<text.Length;i++){var c=text[i]; if(c=='"'){if(quoted&&i+1<text.Length&&text[i+1]=='"'){cell.Append('"');i++;}else quoted=!quoted;} else if(c==delimiter&&!quoted){row.Add(cell.ToString());cell.Clear();} else if((c=='\r'||c=='\n')&&!quoted){if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;row.Add(cell.ToString());cell.Clear();rows.Add(row);row=new();} else cell.Append(c);} if(quoted) return []; if(cell.Length>0||row.Count>0){row.Add(cell.ToString());rows.Add(row);} return rows; }
    private static string Fingerprint(Guid operationId, string hash, ControlledImportType type, CsvImportMapping mapping) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{operationId:N}|{hash}|{type}|{mapping}")));
    private static Guid Deterministic(Guid operationId, int row, string suffix) { var bytes=SHA256.HashData(Encoding.UTF8.GetBytes($"{operationId:N}|{row}|{suffix}")); return new Guid(bytes[..16]); }
    private static ImportReportView MapReport(ImportJobEntity job, IReadOnlyList<ImportIssueView> issues, bool replay) => new(job.Id,job.OperationId,job.SourceFileName,job.SourceFileHash,Enum.Parse<ControlledImportType>(job.ImportType),job.ReadCount,job.CreatedCount,job.UpdatedCount,job.IgnoredCount,job.RejectedCount,replay,job.CompletedAtUtc!.Value,issues);
    private async Task AuditFailureQuietlyAsync(Guid operationId, string reasonCode, CancellationToken token) { try { await audit.RecordFailureAsync("import", "ImportJob", null, "import.confirm", operationId, reasonCode, token); } catch { } }
}
