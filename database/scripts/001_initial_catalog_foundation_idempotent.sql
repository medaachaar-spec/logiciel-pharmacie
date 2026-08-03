IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    IF SCHEMA_ID(N'catalog') IS NULL EXEC(N'CREATE SCHEMA [catalog];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    CREATE TABLE [catalog].[ProductCategories] (
        [Id] bigint NOT NULL IDENTITY,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [IsActive] bit NOT NULL,
        [DefaultTracksLots] bit NOT NULL,
        [DefaultTracksExpiry] bit NOT NULL,
        [DefaultDiscountAllowed] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_catalog_ProductCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_catalog_ProductCategories_Code_NotBlank] CHECK (LEN(LTRIM(RTRIM([Code]))) > 0),
        CONSTRAINT [CK_catalog_ProductCategories_Name_NotBlank] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0),
        CONSTRAINT [CK_catalog_ProductCategories_SortOrder_NonNegative] CHECK ([SortOrder] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    CREATE TABLE [catalog].[Products] (
        [Id] bigint NOT NULL IDENTITY,
        [Name] nvarchar(256) NOT NULL,
        [GenericName] nvarchar(256) NULL,
        [Dosage] nvarchar(128) NULL,
        [Form] nvarchar(128) NULL,
        [PackageDescription] nvarchar(256) NULL,
        [BrandName] nvarchar(160) NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL,
        [UpdatedAtUtc] datetime2(7) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_catalog_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_catalog_Products_Name_NotBlank] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0),
        CONSTRAINT [CK_catalog_Products_UpdatedAfterCreated] CHECK ([UpdatedAtUtc] >= [CreatedAtUtc])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    CREATE TABLE [catalog].[ProductBarcodes] (
        [Id] bigint NOT NULL IDENTITY,
        [ProductId] bigint NOT NULL,
        [Value] nvarchar(128) NOT NULL,
        [NormalizedValue] nvarchar(128) NOT NULL,
        [IsPrimary] bit NOT NULL,
        [BarcodeType] nvarchar(32) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_catalog_ProductBarcodes] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_catalog_ProductBarcodes_NormalizedValue_NotBlank] CHECK (LEN(LTRIM(RTRIM([NormalizedValue]))) > 0),
        CONSTRAINT [CK_catalog_ProductBarcodes_Value_NotBlank] CHECK (LEN(LTRIM(RTRIM([Value]))) > 0),
        CONSTRAINT [FK_catalog_ProductBarcodes_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    CREATE TABLE [catalog].[ProductLocalSettings] (
        [ProductId] bigint NOT NULL,
        [InternalCode] nvarchar(128) NULL,
        [CategoryId] bigint NOT NULL,
        [IsActive] bit NOT NULL,
        [IsSellable] bit NOT NULL,
        [IsPurchasable] bit NOT NULL,
        [TracksLots] bit NOT NULL,
        [TracksExpiry] bit NOT NULL,
        [DiscountAllowed] bit NOT NULL,
        [QuantityScale] tinyint NOT NULL,
        [StorageLocation] nvarchar(128) NULL,
        [MinimumStock] decimal(18,3) NULL,
        [MaximumStock] decimal(18,3) NULL,
        [CurrentSalePrice] decimal(19,4) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_catalog_ProductLocalSettings] PRIMARY KEY ([ProductId]),
        CONSTRAINT [CK_catalog_ProductLocalSettings_CurrencyCode_Length] CHECK (LEN([CurrencyCode]) = 3),
        CONSTRAINT [CK_catalog_ProductLocalSettings_CurrentSalePrice_NonNegative] CHECK ([CurrentSalePrice] >= 0),
        CONSTRAINT [CK_catalog_ProductLocalSettings_InternalCode_NotBlank] CHECK ([InternalCode] IS NULL OR LEN(LTRIM(RTRIM([InternalCode]))) > 0),
        CONSTRAINT [CK_catalog_ProductLocalSettings_QuantityScale_Range] CHECK ([QuantityScale] BETWEEN 0 AND 3),
        CONSTRAINT [CK_catalog_ProductLocalSettings_StockThresholds_NonNegative] CHECK (([MinimumStock] IS NULL OR [MinimumStock] >= 0) AND ([MaximumStock] IS NULL OR [MaximumStock] >= 0)),
        CONSTRAINT [CK_catalog_ProductLocalSettings_StockThresholds_Order] CHECK ([MinimumStock] IS NULL OR [MaximumStock] IS NULL OR [MinimumStock] <= [MaximumStock]),
        CONSTRAINT [FK_catalog_ProductLocalSettings_ProductCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [catalog].[ProductCategories] ([Id]),
        CONSTRAINT [FK_catalog_ProductLocalSettings_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    CREATE TABLE [catalog].[ProductPriceHistories] (
        [Id] bigint NOT NULL IDENTITY,
        [ProductId] bigint NOT NULL,
        [OldPrice] decimal(19,4) NULL,
        [NewPrice] decimal(19,4) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [ChangeOrigin] nvarchar(32) NOT NULL,
        [Reason] nvarchar(256) NULL,
        [EffectiveAtUtc] datetime2(7) NOT NULL,
        CONSTRAINT [PK_catalog_ProductPriceHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_catalog_ProductPriceHistories_ChangeOrigin_NotBlank] CHECK (LEN(LTRIM(RTRIM([ChangeOrigin]))) > 0),
        CONSTRAINT [CK_catalog_ProductPriceHistories_CurrencyCode_Length] CHECK (LEN([CurrencyCode]) = 3),
        CONSTRAINT [CK_catalog_ProductPriceHistories_NewPrice_NonNegative] CHECK ([NewPrice] >= 0),
        CONSTRAINT [CK_catalog_ProductPriceHistories_OldPrice_NonNegative] CHECK ([OldPrice] IS NULL OR [OldPrice] >= 0),
        CONSTRAINT [FK_catalog_ProductPriceHistories_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_catalog_ProductBarcodes_ProductId] ON [catalog].[ProductBarcodes] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_catalog_ProductBarcodes_NormalizedValue] ON [catalog].[ProductBarcodes] ([NormalizedValue]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_catalog_ProductBarcodes_OneActivePrimaryPerProduct] ON [catalog].[ProductBarcodes] ([ProductId], [IsPrimary], [IsActive]) WHERE [IsPrimary] = 1 AND [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_catalog_ProductCategories_Name] ON [catalog].[ProductCategories] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_catalog_ProductCategories_Code] ON [catalog].[ProductCategories] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_catalog_ProductLocalSettings_CategoryId] ON [catalog].[ProductLocalSettings] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_catalog_ProductLocalSettings_InternalCode] ON [catalog].[ProductLocalSettings] ([InternalCode]) WHERE [InternalCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_catalog_ProductPriceHistories_ProductId_EffectiveAtUtc] ON [catalog].[ProductPriceHistories] ([ProductId], [EffectiveAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_catalog_Products_Name] ON [catalog].[Products] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803185734_InitialCatalogPersistenceV01'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260803185734_InitialCatalogPersistenceV01', N'10.0.10');
END;

COMMIT;
GO

