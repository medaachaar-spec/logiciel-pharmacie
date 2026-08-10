using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Cash;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Cash;

internal sealed class CashLedgerEntryConfiguration : IEntityTypeConfiguration<CashLedgerEntryEntity>
{
    public void Configure(EntityTypeBuilder<CashLedgerEntryEntity> builder)
    {
        builder.ToTable("CashLedgerEntries", CashSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_cash_CashLedgerEntries_EntryType", "[EntryType] IN (N'SaleCashIn', N'CreditPaymentCashIn', N'RefundCashOut')");
            table.HasCheckConstraint("CK_cash_CashLedgerEntries_Amount_NotZero", "[Amount] <> 0");
            table.HasCheckConstraint("CK_cash_CashLedgerEntries_Source_NotBlank", "LEN(LTRIM(RTRIM([SourceType]))) > 0 AND LEN(LTRIM(RTRIM([SourceReference]))) > 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_cash_CashLedgerEntries");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.OperationId).IsRequired();
        builder.Property(entity => entity.EntryType).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Amount).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.SourceType).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.SourceReference).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.OccurredAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.HasIndex(entity => entity.OperationId).IsUnique().HasDatabaseName("UX_cash_CashLedgerEntries_OperationId");
        builder.HasIndex(entity => new { entity.OccurredAtUtc, entity.Id }).HasDatabaseName("IX_cash_CashLedgerEntries_Timeline");
    }
}
