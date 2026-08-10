using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Pharmacy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecurityRbacAuditPersistenceV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "security");

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: true),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    WorkstationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_AuditEvents", x => x.Id);
                    table.CheckConstraint("CK_audit_AuditEvents_Action_NotBlank", "LEN(LTRIM(RTRIM([Action]))) > 0");
                    table.CheckConstraint("CK_audit_AuditEvents_Module_NotBlank", "LEN(LTRIM(RTRIM([Module]))) > 0");
                    table.CheckConstraint("CK_audit_AuditEvents_Result", "[Result] IN (N'Success', N'Failure', N'Denied')");
                });

            migrationBuilder.CreateTable(
                name: "AuthenticationEvents",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: true),
                    UsernameAttemptHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    WorkstationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    WasSuccessful = table.Column<bool>(type: "bit", nullable: false),
                    FailureReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_AuthenticationEvents", x => x.Id);
                    table.CheckConstraint("CK_security_AuthenticationEvents_EventType", "[EventType] IN (N'Login', N'Logout', N'Lock', N'Unlock', N'Reauthentication', N'PasswordChange', N'Recovery')");
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_Permissions", x => x.Id);
                    table.CheckConstraint("CK_security_Permissions_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_security_Permissions_Module_NotBlank", "LEN(LTRIM(RTRIM([Module]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_Roles", x => x.Id);
                    table.CheckConstraint("CK_security_Roles_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_security_Roles_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Login = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NormalizedLogin = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    DisabledAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DisabledByUserId = table.Column<long>(type: "bigint", nullable: true),
                    SecurityStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_UserAccounts", x => x.Id);
                    table.CheckConstraint("CK_security_UserAccounts_DisplayName_NotBlank", "LEN(LTRIM(RTRIM([DisplayName]))) > 0");
                    table.CheckConstraint("CK_security_UserAccounts_FailedLoginCount", "[FailedLoginCount] >= 0 AND [FailedLoginCount] <= 5");
                    table.CheckConstraint("CK_security_UserAccounts_Login_NotBlank", "LEN(LTRIM(RTRIM([Login]))) > 0");
                    table.CheckConstraint("CK_security_UserAccounts_NormalizedLogin_NotBlank", "LEN(LTRIM(RTRIM([NormalizedLogin]))) > 0");
                    table.CheckConstraint("CK_security_UserAccounts_PasswordHash_NotBlank", "LEN([PasswordHash]) > 0");
                });

            migrationBuilder.CreateTable(
                name: "AuditEventDetails",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditEventId = table.Column<long>(type: "bigint", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OldValueRedacted = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    NewValueRedacted = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_AuditEventDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_AuditEventDetails_AuditEvents_AuditEventId",
                        column: x => x.AuditEventId,
                        principalSchema: "audit",
                        principalTable: "AuditEvents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_security_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "security",
                        principalTable: "Permissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_security_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "security",
                        principalTable: "Roles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RecoveryCodes",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_RecoveryCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_security_RecoveryCodes_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "security",
                        principalTable: "UserAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserAccountId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    AssignedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_security_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "security",
                        principalTable: "Roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_security_UserRoles_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalSchema: "security",
                        principalTable: "UserAccounts",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                schema: "security",
                table: "Permissions",
                columns: new[] { "Id", "Code", "Description", "IsSensitive", "Module", "Name" },
                values: new object[,]
                {
                    { 1L, "sales.create", "Permission explicite sales.create.", false, "sales", "sales.create" },
                    { 2L, "sales.view_history", "Permission explicite sales.view_history.", false, "sales", "sales.view_history" },
                    { 3L, "sales.return", "Permission explicite sales.return.", true, "sales", "sales.return" },
                    { 4L, "sales.refund", "Permission explicite sales.refund.", true, "sales", "sales.refund" },
                    { 5L, "inventory.view", "Permission explicite inventory.view.", false, "inventory", "inventory.view" },
                    { 6L, "inventory.adjust", "Permission explicite inventory.adjust.", true, "inventory", "inventory.adjust" },
                    { 7L, "inventory.finalize_count", "Permission explicite inventory.finalize_count.", true, "inventory", "inventory.finalize_count" },
                    { 8L, "purchasing.view", "Permission explicite purchasing.view.", false, "purchasing", "purchasing.view" },
                    { 9L, "purchasing.receive", "Permission explicite purchasing.receive.", true, "purchasing", "purchasing.receive" },
                    { 10L, "catalog.view", "Permission explicite catalog.view.", false, "catalog", "catalog.view" },
                    { 11L, "catalog.manage", "Permission explicite catalog.manage.", false, "catalog", "catalog.manage" },
                    { 12L, "catalog.change_price", "Permission explicite catalog.change_price.", true, "catalog", "catalog.change_price" },
                    { 13L, "customers.view", "Permission explicite customers.view.", false, "customers", "customers.view" },
                    { 14L, "customers.manage", "Permission explicite customers.manage.", false, "customers", "customers.manage" },
                    { 15L, "customers.manage_credit", "Permission explicite customers.manage_credit.", true, "customers", "customers.manage_credit" },
                    { 16L, "cash.view", "Permission explicite cash.view.", false, "cash", "cash.view" },
                    { 17L, "security.manage_users", "Permission explicite security.manage_users.", true, "security", "security.manage_users" },
                    { 18L, "security.manage_roles", "Permission explicite security.manage_roles.", true, "security", "security.manage_roles" },
                    { 19L, "audit.view", "Permission explicite audit.view.", false, "audit", "audit.view" },
                    { 20L, "reports.view", "Permission explicite reports.view.", false, "reports", "reports.view" }
                });

            migrationBuilder.InsertData(
                schema: "security",
                table: "Roles",
                columns: new[] { "Id", "Code", "Description", "IsActive", "IsSystemRole", "Name" },
                values: new object[] { 1L, "OWNER", "Compte propriétaire partagé du pilote ; attribution individuelle limitée.", true, true, "Propriétaire" });

            migrationBuilder.InsertData(
                schema: "security",
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 1L, 1L, 1L },
                    { 2L, 2L, 1L },
                    { 3L, 3L, 1L },
                    { 4L, 4L, 1L },
                    { 5L, 5L, 1L },
                    { 6L, 6L, 1L },
                    { 7L, 7L, 1L },
                    { 8L, 8L, 1L },
                    { 9L, 9L, 1L },
                    { 10L, 10L, 1L },
                    { 11L, 11L, 1L },
                    { 12L, 12L, 1L },
                    { 13L, 13L, 1L },
                    { 14L, 14L, 1L },
                    { 15L, 15L, 1L },
                    { 16L, 16L, 1L },
                    { 17L, 17L, 1L },
                    { 18L, 18L, 1L },
                    { 19L, 19L, 1L },
                    { 20L, 20L, 1L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_AuditEventDetails_AuditEventId",
                schema: "audit",
                table: "AuditEventDetails",
                column: "AuditEventId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_AuditEvents_ActorUserId",
                schema: "audit",
                table: "AuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_AuditEvents_CorrelationId",
                schema: "audit",
                table: "AuditEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_AuditEvents_Entity",
                schema: "audit",
                table: "AuditEvents",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_AuditEvents_OperationId",
                schema: "audit",
                table: "AuditEvents",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_AuditEvents_Timeline",
                schema: "audit",
                table: "AuditEvents",
                columns: new[] { "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_security_AuthenticationEvents_CorrelationId",
                schema: "security",
                table: "AuthenticationEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_security_AuthenticationEvents_Timeline",
                schema: "security",
                table: "AuthenticationEvents",
                columns: new[] { "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_security_Permissions_Code",
                schema: "security",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_security_RecoveryCodes_User_Expiry",
                schema: "security",
                table: "RecoveryCodes",
                columns: new[] { "UserAccountId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_security_RecoveryCodes_CodeHash",
                schema: "security",
                table: "RecoveryCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                schema: "security",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UX_security_RolePermissions_Role_Permission",
                schema: "security",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_security_Roles_Code",
                schema: "security",
                table: "Roles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_security_UserAccounts_NormalizedLogin",
                schema: "security",
                table: "UserAccounts",
                column: "NormalizedLogin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "security",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UX_security_UserRoles_User_Role",
                schema: "security",
                table: "UserRoles",
                columns: new[] { "UserAccountId", "RoleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEventDetails",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "AuthenticationEvents",
                schema: "security");

            migrationBuilder.DropTable(
                name: "RecoveryCodes",
                schema: "security");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "security");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "security");

            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "security");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "security");

            migrationBuilder.DropTable(
                name: "UserAccounts",
                schema: "security");
        }
    }
}
