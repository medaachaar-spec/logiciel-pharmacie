using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Customers;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Customers;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerEntity>
{
    public void Configure(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.ToTable("Customers", CustomersSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_customers_Customers_Reference_NotBlank", "LEN(LTRIM(RTRIM([Reference]))) > 0");
            table.HasCheckConstraint("CK_customers_Customers_FullName_NotBlank", "LEN(LTRIM(RTRIM([FullName]))) > 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_customers_Customers");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.Reference).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.FullName).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Phone).HasColumnType("nvarchar(64)").HasMaxLength(64);
        builder.Property(entity => entity.Note).HasColumnType("nvarchar(512)").HasMaxLength(512);
        builder.Property(entity => entity.IsActive).IsRequired();
        builder.Property(entity => entity.CreditAllowed).IsRequired();
        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.UpdatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => entity.Reference).IsUnique().HasDatabaseName("UX_customers_Customers_Reference");
        builder.HasIndex(entity => new { entity.FullName, entity.Id }).HasDatabaseName("IX_customers_Customers_FullName_Id");
        builder.HasIndex(entity => entity.Phone).HasDatabaseName("IX_customers_Customers_Phone");
    }
}

internal sealed class CustomerLedgerEntryConfiguration : IEntityTypeConfiguration<CustomerLedgerEntryEntity>
{
    public void Configure(EntityTypeBuilder<CustomerLedgerEntryEntity> builder)
    {
        builder.ToTable("CustomerLedgerEntries", CustomersSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_customers_CustomerLedgerEntries_EntryType", "[EntryType] IN (N'CreditSale', N'CreditPayment', N'CreditAdjustmentIncrease', N'CreditAdjustmentDecrease', N'RefundImpact')");
            table.HasCheckConstraint("CK_customers_CustomerLedgerEntries_Amount_NonNegative", "[Amount] >= 0");
            table.HasCheckConstraint("CK_customers_CustomerLedgerEntries_Source_NotBlank", "LEN(LTRIM(RTRIM([SourceType]))) > 0 AND LEN(LTRIM(RTRIM([SourceReference]))) > 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_customers_CustomerLedgerEntries");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.OperationId).IsRequired();
        builder.Property(entity => entity.CustomerId).IsRequired();
        builder.Property(entity => entity.EntryType).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Amount).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(entity => entity.SourceType).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.SourceReference).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.OccurredAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.HasOne(entity => entity.Customer).WithMany(customer => customer.LedgerEntries)
            .HasForeignKey(entity => entity.CustomerId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_customers_CustomerLedgerEntries_Customers_CustomerId");
        builder.HasIndex(entity => entity.OperationId).IsUnique().HasDatabaseName("UX_customers_CustomerLedgerEntries_OperationId");
        builder.HasIndex(entity => new { entity.CustomerId, entity.OccurredAtUtc, entity.Id })
            .HasDatabaseName("IX_customers_CustomerLedgerEntries_Customer_Timeline");
    }
}
