namespace Pharmacy.Infrastructure.Persistence.Entities.Cash;

internal sealed class CashLedgerEntryEntity
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
