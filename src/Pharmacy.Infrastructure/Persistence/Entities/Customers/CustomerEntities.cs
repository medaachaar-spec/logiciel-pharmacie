namespace Pharmacy.Infrastructure.Persistence.Entities.Customers;

internal sealed class CustomerEntity
{
    public long Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; }
    public bool CreditAllowed { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<CustomerLedgerEntryEntity> LedgerEntries { get; } = new List<CustomerLedgerEntryEntity>();
}

internal sealed class CustomerLedgerEntryEntity
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public long CustomerId { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public CustomerEntity Customer { get; set; } = null!;
}
