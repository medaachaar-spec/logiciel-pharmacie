using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pharmacy.Application.Security;
using Pharmacy.Infrastructure.Persistence.Entities.Security;

namespace Pharmacy.Infrastructure.Persistence.Configurations.Security;

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccountEntity>
{
    public void Configure(EntityTypeBuilder<UserAccountEntity> builder)
    {
        builder.ToTable("UserAccounts", SecuritySchema.Name, table =>
        {
            table.HasCheckConstraint("CK_security_UserAccounts_Login_NotBlank", "LEN(LTRIM(RTRIM([Login]))) > 0");
            table.HasCheckConstraint("CK_security_UserAccounts_NormalizedLogin_NotBlank", "LEN(LTRIM(RTRIM([NormalizedLogin]))) > 0");
            table.HasCheckConstraint("CK_security_UserAccounts_DisplayName_NotBlank", "LEN(LTRIM(RTRIM([DisplayName]))) > 0");
            table.HasCheckConstraint("CK_security_UserAccounts_PasswordHash_NotBlank", "LEN([PasswordHash]) > 0");
            table.HasCheckConstraint("CK_security_UserAccounts_FailedLoginCount", "[FailedLoginCount] >= 0 AND [FailedLoginCount] <= 5");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_security_UserAccounts");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.Login).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.NormalizedLogin).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.DisplayName).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.PasswordHash).HasColumnType("nvarchar(512)").HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.IsActive).IsRequired();
        builder.Property(entity => entity.FailedLoginCount).IsRequired();
        builder.Property(entity => entity.LockedUntilUtc).HasColumnType("datetime2(7)");
        builder.Property(entity => entity.MustChangePassword).IsRequired();
        builder.Property(entity => entity.LastLoginAtUtc).HasColumnType("datetime2(7)");
        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.UpdatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.DisabledAtUtc).HasColumnType("datetime2(7)");
        builder.Property(entity => entity.DisabledByUserId);
        builder.Property(entity => entity.SecurityStamp).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => entity.NormalizedLogin).IsUnique().HasDatabaseName("UX_security_UserAccounts_NormalizedLogin");
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<RoleEntity>
{
    public void Configure(EntityTypeBuilder<RoleEntity> builder)
    {
        builder.ToTable("Roles", SecuritySchema.Name, table =>
        {
            table.HasCheckConstraint("CK_security_Roles_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
            table.HasCheckConstraint("CK_security_Roles_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_security_Roles");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.Code).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Name).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Description).HasColumnType("nvarchar(512)").HasMaxLength(512);
        builder.Property(entity => entity.IsSystemRole).IsRequired();
        builder.Property(entity => entity.IsActive).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => entity.Code).IsUnique().HasDatabaseName("UX_security_Roles_Code");
        builder.HasData(
            new
            {
                Id = 1L,
                Code = SecurityRolePresets.OwnerCode,
                Name = "Propriétaire",
                Description = "Compte propriétaire partagé du pilote ; attribution individuelle limitée.",
                IsSystemRole = true,
                IsActive = true
            },
            new
            {
                Id = 2L,
                Code = SecurityRolePresets.ManagerCode,
                Name = "Manager",
                Description = "Responsable des opérations courantes et de la gestion des employés.",
                IsSystemRole = true,
                IsActive = true
            },
            new
            {
                Id = 3L,
                Code = SecurityRolePresets.SellerCode,
                Name = "Vendeur",
                Description = "Vente et consultations nécessaires au point de vente.",
                IsSystemRole = true,
                IsActive = true
            });
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<PermissionEntity>
{
    public void Configure(EntityTypeBuilder<PermissionEntity> builder)
    {
        builder.ToTable("Permissions", SecuritySchema.Name, table =>
        {
            table.HasCheckConstraint("CK_security_Permissions_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
            table.HasCheckConstraint("CK_security_Permissions_Module_NotBlank", "LEN(LTRIM(RTRIM([Module]))) > 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_security_Permissions");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.Code).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.Name).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Module).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Description).HasColumnType("nvarchar(512)").HasMaxLength(512);
        builder.Property(entity => entity.IsSensitive).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion();
        builder.HasIndex(entity => entity.Code).IsUnique().HasDatabaseName("UX_security_Permissions_Code");

        builder.HasData(SecurityPermissions.All.Select((code, index) => new
        {
            Id = (long)index + 1,
            Code = code,
            Name = code,
            Module = code.Split('.')[0],
            Description = $"Permission explicite {code}.",
            IsSensitive = SecurityPermissions.Sensitive.Contains(code)
        }));
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRoleEntity>
{
    public void Configure(EntityTypeBuilder<UserRoleEntity> builder)
    {
        builder.ToTable("UserRoles", SecuritySchema.Name);
        builder.HasKey(entity => entity.Id).HasName("PK_security_UserRoles");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.AssignedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.HasOne(entity => entity.UserAccount).WithMany(user => user.UserRoles)
            .HasForeignKey(entity => entity.UserAccountId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_security_UserRoles_UserAccounts_UserAccountId");
        builder.HasOne(entity => entity.Role).WithMany(role => role.UserRoles)
            .HasForeignKey(entity => entity.RoleId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_security_UserRoles_Roles_RoleId");
        builder.HasIndex(entity => new { entity.UserAccountId, entity.RoleId }).IsUnique()
            .HasDatabaseName("UX_security_UserRoles_User_Role");
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermissionEntity>
{
    public void Configure(EntityTypeBuilder<RolePermissionEntity> builder)
    {
        builder.ToTable("RolePermissions", SecuritySchema.Name);
        builder.HasKey(entity => entity.Id).HasName("PK_security_RolePermissions");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.HasOne(entity => entity.Role).WithMany(role => role.RolePermissions)
            .HasForeignKey(entity => entity.RoleId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_security_RolePermissions_Roles_RoleId");
        builder.HasOne(entity => entity.Permission).WithMany(permission => permission.RolePermissions)
            .HasForeignKey(entity => entity.PermissionId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_security_RolePermissions_Permissions_PermissionId");
        builder.HasIndex(entity => new { entity.RoleId, entity.PermissionId }).IsUnique()
            .HasDatabaseName("UX_security_RolePermissions_Role_Permission");
        builder.HasData(SecurityPermissions.All.Select((_, index) => new
        {
            Id = (long)index + 1,
            RoleId = 1L,
            PermissionId = (long)index + 1
        }));

        builder.HasData(SecurityRolePresets.ManagerPermissions.Select((code, index) => new
        {
            Id = 100L + index,
            RoleId = 2L,
            PermissionId = PermissionId(code)
        }));

        builder.HasData(SecurityRolePresets.SellerPermissions.Select((code, index) => new
        {
            Id = 200L + index,
            RoleId = 3L,
            PermissionId = PermissionId(code)
        }));
    }

    private static long PermissionId(string code) =>
        SecurityPermissions.All.Select((value, index) => (value, index))
            .Single(item => item.value == code).index + 1L;
}

internal sealed class UserPermissionOverrideConfiguration : IEntityTypeConfiguration<UserPermissionOverrideEntity>
{
    public void Configure(EntityTypeBuilder<UserPermissionOverrideEntity> builder)
    {
        builder.ToTable("UserPermissionOverrides", SecuritySchema.Name, table =>
        {
            table.HasCheckConstraint("CK_security_UserPermissionOverrides_Effect", "[Effect] IN (N'Allow', N'Deny')");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_security_UserPermissionOverrides");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.Effect).HasColumnType("nvarchar(16)").HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.UpdatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(entity => entity.UserAccount).WithMany(user => user.PermissionOverrides)
            .HasForeignKey(entity => entity.UserAccountId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_security_UserPermissionOverrides_UserAccounts_UserAccountId");
        builder.HasOne(entity => entity.Permission).WithMany(permission => permission.UserOverrides)
            .HasForeignKey(entity => entity.PermissionId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_security_UserPermissionOverrides_Permissions_PermissionId");
        builder.HasIndex(entity => new { entity.UserAccountId, entity.PermissionId }).IsUnique()
            .HasDatabaseName("UX_security_UserPermissionOverrides_User_Permission");
        builder.HasIndex(entity => entity.UpdatedByUserId)
            .HasDatabaseName("IX_security_UserPermissionOverrides_UpdatedByUserId");
    }
}

internal sealed class RecoveryCodeConfiguration : IEntityTypeConfiguration<RecoveryCodeEntity>
{
    public void Configure(EntityTypeBuilder<RecoveryCodeEntity> builder)
    {
        builder.ToTable("RecoveryCodes", SecuritySchema.Name);
        builder.HasKey(entity => entity.Id).HasName("PK_security_RecoveryCodes");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.CodeHash).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.ExpiresAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.UsedAtUtc).HasColumnType("datetime2(7)");
        builder.HasOne(entity => entity.UserAccount).WithMany(user => user.RecoveryCodes)
            .HasForeignKey(entity => entity.UserAccountId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_security_RecoveryCodes_UserAccounts_UserAccountId");
        builder.HasIndex(entity => entity.CodeHash).IsUnique().HasDatabaseName("UX_security_RecoveryCodes_CodeHash");
        builder.HasIndex(entity => new { entity.UserAccountId, entity.ExpiresAtUtc }).HasDatabaseName("IX_security_RecoveryCodes_User_Expiry");
    }
}

internal sealed class AuthenticationEventConfiguration : IEntityTypeConfiguration<AuthenticationEventEntity>
{
    public void Configure(EntityTypeBuilder<AuthenticationEventEntity> builder)
    {
        builder.ToTable("AuthenticationEvents", SecuritySchema.Name, table =>
        {
            table.HasCheckConstraint("CK_security_AuthenticationEvents_EventType", "[EventType] IN (N'Login', N'Logout', N'Lock', N'Unlock', N'Reauthentication', N'PasswordChange', N'Recovery')");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_security_AuthenticationEvents");
        builder.Property(entity => entity.Id).UseIdentityColumn();
        builder.Property(entity => entity.EventType).HasColumnType("nvarchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.UsernameAttemptHash).HasColumnType("nvarchar(128)").HasMaxLength(128);
        builder.Property(entity => entity.WorkstationId).HasColumnType("nvarchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.OccurredAtUtc).HasColumnType("datetime2(7)").IsRequired();
        builder.Property(entity => entity.FailureReasonCode).HasColumnType("nvarchar(128)").HasMaxLength(128);
        builder.HasIndex(entity => new { entity.OccurredAtUtc, entity.Id }).HasDatabaseName("IX_security_AuthenticationEvents_Timeline");
        builder.HasIndex(entity => entity.CorrelationId).HasDatabaseName("IX_security_AuthenticationEvents_CorrelationId");
    }
}
