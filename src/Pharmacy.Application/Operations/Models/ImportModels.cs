namespace Pharmacy.Application.Operations.Models;

public enum ControlledImportType { Catalogue, OpeningStock, Lots }
public enum ImportIssueSeverity { Warning, Error }

public sealed record CsvImportMapping(
    string ReferenceColumn,
    string DesignationColumn,
    string? SalePriceColumn = null,
    string? PurchasePriceColumn = null,
    string? BarcodeColumn = null,
    string? CategoryColumn = null,
    string? QuantityColumn = null,
    string? LotColumn = null,
    string? ExpiryColumn = null,
    string? DefaultCategoryCode = null,
    string? StockLocationCode = null);

public sealed record ImportIssueView(int RowNumber, string Code, ImportIssueSeverity Severity, string? Column, string Message);

public sealed record ImportPreviewRow(
    int RowNumber,
    string Reference,
    string Designation,
    decimal? SalePrice,
    decimal? PurchasePrice,
    string? Barcode,
    string? CategoryCode,
    int? Quantity,
    string? LotNumber,
    DateOnly? ExpiryDate,
    string Action);

public sealed record PreviewImportRequest(
    string FileName,
    byte[] Content,
    ControlledImportType Type,
    CsvImportMapping Mapping,
    int PreviewLimit = 100);

public sealed record ImportPreviewView(
    string SafeFileName,
    string Sha256,
    ControlledImportType Type,
    int TotalRows,
    IReadOnlyList<string> Headers,
    IReadOnlyList<ImportPreviewRow> Rows,
    IReadOnlyList<ImportIssueView> Issues,
    bool CanConfirm);

public sealed record ConfirmImportRequest(
    Guid OperationId,
    string FileName,
    byte[] Content,
    ControlledImportType Type,
    CsvImportMapping Mapping);

public sealed record ImportReportView(
    long ImportJobId,
    Guid OperationId,
    string SafeFileName,
    string Sha256,
    ControlledImportType Type,
    int ReadCount,
    int CreatedCount,
    int UpdatedCount,
    int IgnoredCount,
    int RejectedCount,
    bool WasReplay,
    DateTime CompletedAtUtc,
    IReadOnlyList<ImportIssueView> Issues);
