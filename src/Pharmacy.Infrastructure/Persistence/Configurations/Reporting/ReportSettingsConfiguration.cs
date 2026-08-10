using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Infrastructure.Persistence.Entities.Reporting;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Reporting;

internal sealed class ReportSettingsConfiguration : IEntityTypeConfiguration<ReportSettingsEntity>
{
    public void Configure(EntityTypeBuilder<ReportSettingsEntity> builder)
    {
        builder.ToTable("ReportSettings", ReportingSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_reporting_ReportSettings_Singleton", "[Id] = 1");
            table.HasCheckConstraint("CK_reporting_ReportSettings_ExpiryHorizon", "[ExpiryHorizonDays] IS NULL OR [ExpiryHorizonDays] >= 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_reporting_ReportSettings");
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.LowStockAlertsEnabled).IsRequired();
        builder.Property(entity => entity.ExpiryAlertsEnabled).IsRequired();
        builder.Property(entity => entity.ExpiryHorizonDays);
        builder.Property(entity => entity.UpdatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasData(new
        {
            Id = 1L,
            LowStockAlertsEnabled = false,
            ExpiryAlertsEnabled = false,
            ExpiryHorizonDays = (int?)null,
            UpdatedAtUtc = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
