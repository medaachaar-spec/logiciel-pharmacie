using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Application.Branding;
using Pharmacy.Infrastructure.Persistence.Entities.Configuration;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Configuration;

internal sealed class PharmacyBrandingConfiguration : IEntityTypeConfiguration<PharmacyBrandingEntity>
{
    public void Configure(EntityTypeBuilder<PharmacyBrandingEntity> builder)
    {
        builder.ToTable("PharmacyBranding", ConfigurationSchema.Name, table =>
        {
            table.HasCheckConstraint("CK_configuration_PharmacyBranding_Singleton", "[Id] = 1");
            table.HasCheckConstraint("CK_configuration_PharmacyBranding_Name_NotBlank", "LEN(LTRIM(RTRIM([PharmacyName]))) > 0");
            table.HasCheckConstraint("CK_configuration_PharmacyBranding_PrimaryColor", "[PrimaryColor] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'");
            table.HasCheckConstraint("CK_configuration_PharmacyBranding_Logo_Size", $"[LogoContent] IS NULL OR DATALENGTH([LogoContent]) <= {PharmacyBrandingDefaults.MaxLogoBytes}");
            table.HasCheckConstraint("CK_configuration_PharmacyBranding_Logo_Metadata", "([LogoContent] IS NULL AND [LogoMimeType] IS NULL AND [LogoFileName] IS NULL) OR ([LogoContent] IS NOT NULL AND [LogoMimeType] IS NOT NULL AND [LogoFileName] IS NOT NULL)");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_configuration_PharmacyBranding");
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.PharmacyName).HasColumnType("nvarchar(160)").HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.PrimaryColor).HasColumnType("nvarchar(7)").HasMaxLength(7).IsRequired();
        builder.Property(entity => entity.LogoContent).HasColumnType("varbinary(max)");
        builder.Property(entity => entity.LogoMimeType).HasColumnType("nvarchar(32)").HasMaxLength(32);
        builder.Property(entity => entity.LogoFileName).HasColumnType("nvarchar(255)").HasMaxLength(255);
        builder.Property(entity => entity.UpdatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasData(new
        {
            Id = 1L,
            PharmacyName = PharmacyBrandingDefaults.PharmacyName,
            PrimaryColor = PharmacyBrandingDefaults.PrimaryColor,
            LogoContent = (byte[]?)null,
            LogoMimeType = (string?)null,
            LogoFileName = (string?)null,
            UpdatedAtUtc = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
