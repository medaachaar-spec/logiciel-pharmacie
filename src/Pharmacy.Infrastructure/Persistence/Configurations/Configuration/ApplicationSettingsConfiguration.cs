using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Application.Configuration;
using Pharmacy.Infrastructure.Persistence.Entities.Configuration;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Configuration;

internal sealed class ApplicationSettingsConfiguration
    : IEntityTypeConfiguration<ApplicationSettingsEntity>
{
    public void Configure(EntityTypeBuilder<ApplicationSettingsEntity> builder)
    {
        builder.ToTable("ApplicationSettings", ConfigurationSchema.Name, table =>
            table.HasCheckConstraint(
                "CK_configuration_ApplicationSettings_Singleton",
                "[Id] = 1"));
        builder.HasKey(entity => entity.Id)
            .HasName("PK_configuration_ApplicationSettings");
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.ReceiptPrintingEnabled).IsRequired();
        builder.Property(entity => entity.UpdatedAtUtc)
            .HasColumnType("datetime2(7)")
            .IsRequired();
        builder.Property(entity => entity.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
        builder.HasData(new
        {
            Id = 1L,
            ReceiptPrintingEnabled = ReceiptPrintingSettingsDefaults.ReceiptPrintingEnabled,
            UpdatedAtUtc = new DateTime(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
