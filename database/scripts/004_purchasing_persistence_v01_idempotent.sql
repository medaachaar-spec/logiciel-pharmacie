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
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803215422_CatalogPersistenceEnhancementsV01'
)
BEGIN
    ALTER TABLE [catalog].[Products] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803215422_CatalogPersistenceEnhancementsV01'
)
BEGIN
    CREATE INDEX [IX_catalog_Products_IsActive] ON [catalog].[Products] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803215422_CatalogPersistenceEnhancementsV01'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260803215422_CatalogPersistenceEnhancementsV01', N'10.0.10');
END;

COMMIT;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    IF SCHEMA_ID(N'inventory') IS NULL EXEC(N'CREATE SCHEMA [inventory];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE TABLE [inventory].[StockBatches] (
        [Id] bigint NOT NULL IDENTITY,
        [CreationOperationId] uniqueidentifier NOT NULL,
        [CreationFingerprint] nvarchar(512) NOT NULL,
        [ProductId] bigint NOT NULL,
        [BatchNumber] nvarchar(128) NULL,
        [ExpiryDate] date NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_inventory_StockBatches] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_inventory_StockBatches_BatchNumber_NotBlank] CHECK ([BatchNumber] IS NULL OR LEN(LTRIM(RTRIM([BatchNumber]))) > 0),
        CONSTRAINT [CK_inventory_StockBatches_CreationFingerprint_NotBlank] CHECK (LEN(LTRIM(RTRIM([CreationFingerprint]))) > 0),
        CONSTRAINT [FK_inventory_StockBatches_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE TABLE [inventory].[StockLocations] (
        [Id] bigint NOT NULL IDENTITY,
        [CreationOperationId] uniqueidentifier NOT NULL,
        [CreationFingerprint] nvarchar(512) NOT NULL,
        [LastOperationId] uniqueidentifier NULL,
        [LastOperationFingerprint] nvarchar(512) NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [IsActive] bit NOT NULL,
        [IsPrimary] bit NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_inventory_StockLocations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_inventory_StockLocations_Code_NotBlank] CHECK (LEN(LTRIM(RTRIM([Code]))) > 0),
        CONSTRAINT [CK_inventory_StockLocations_CreationFingerprint_NotBlank] CHECK (LEN(LTRIM(RTRIM([CreationFingerprint]))) > 0),
        CONSTRAINT [CK_inventory_StockLocations_Name_NotBlank] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE TABLE [inventory].[InventorySessions] (
        [Id] bigint NOT NULL IDENTITY,
        [CreationOperationId] uniqueidentifier NOT NULL,
        [CreationFingerprint] nvarchar(512) NOT NULL,
        [StartOperationId] uniqueidentifier NULL,
        [StartOperationFingerprint] nvarchar(512) NULL,
        [FinalizationOperationId] uniqueidentifier NULL,
        [FinalizationFingerprint] nvarchar(512) NULL,
        [CancellationOperationId] uniqueidentifier NULL,
        [CancellationFingerprint] nvarchar(512) NULL,
        [Reference] nvarchar(64) NOT NULL,
        [StockLocationId] bigint NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [StartedAtUtc] datetime2(7) NOT NULL,
        [CompletedAtUtc] datetime2(7) NULL,
        [Notes] nvarchar(512) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_inventory_InventorySessions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_inventory_InventorySessions_CompletedAtUtc] CHECK ([CompletedAtUtc] IS NULL OR [CompletedAtUtc] >= [StartedAtUtc]),
        CONSTRAINT [CK_inventory_InventorySessions_CreationFingerprint_NotBlank] CHECK (LEN(LTRIM(RTRIM([CreationFingerprint]))) > 0),
        CONSTRAINT [CK_inventory_InventorySessions_Reference_NotBlank] CHECK (LEN(LTRIM(RTRIM([Reference]))) > 0),
        CONSTRAINT [FK_inventory_InventorySessions_StockLocations_StockLocationId] FOREIGN KEY ([StockLocationId]) REFERENCES [inventory].[StockLocations] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE TABLE [inventory].[StockBalances] (
        [Id] bigint NOT NULL IDENTITY,
        [StockLocationId] bigint NOT NULL,
        [ProductId] bigint NOT NULL,
        [StockBatchId] bigint NULL,
        [AvailableQuantity] int NOT NULL,
        [UpdatedAtUtc] datetime2(7) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_inventory_StockBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_inventory_StockBalances_AvailableQuantity_NonNegative] CHECK ([AvailableQuantity] >= 0),
        CONSTRAINT [FK_inventory_StockBalances_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id]),
        CONSTRAINT [FK_inventory_StockBalances_StockBatches_StockBatchId] FOREIGN KEY ([StockBatchId]) REFERENCES [inventory].[StockBatches] ([Id]),
        CONSTRAINT [FK_inventory_StockBalances_StockLocations_StockLocationId] FOREIGN KEY ([StockLocationId]) REFERENCES [inventory].[StockLocations] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE TABLE [inventory].[StockMovements] (
        [Id] bigint NOT NULL IDENTITY,
        [OperationId] uniqueidentifier NOT NULL,
        [OperationFingerprint] nvarchar(512) NOT NULL,
        [ProductId] bigint NOT NULL,
        [StockLocationId] bigint NOT NULL,
        [StockBatchId] bigint NULL,
        [MovementType] nvarchar(32) NOT NULL,
        [Quantity] int NOT NULL,
        [QuantityBefore] int NOT NULL,
        [QuantityAfter] int NOT NULL,
        [Reason] nvarchar(256) NOT NULL,
        [SourceType] nvarchar(32) NOT NULL,
        [SourceReference] nvarchar(128) NULL,
        [OccurredAtUtc] datetime2(7) NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL,
        CONSTRAINT [PK_inventory_StockMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_inventory_StockMovements_Balances_NonNegative] CHECK ([QuantityBefore] >= 0 AND [QuantityAfter] >= 0),
        CONSTRAINT [CK_inventory_StockMovements_Direction] CHECK ((([MovementType] IN (N'Opening', N'ManualIncrease', N'InventoryIncrease') AND [QuantityAfter] = [QuantityBefore] + [Quantity]) OR ([MovementType] IN (N'ManualDecrease', N'InventoryDecrease') AND [QuantityAfter] = [QuantityBefore] - [Quantity]))),
        CONSTRAINT [CK_inventory_StockMovements_Fingerprint_NotBlank] CHECK (LEN(LTRIM(RTRIM([OperationFingerprint]))) > 0),
        CONSTRAINT [CK_inventory_StockMovements_Quantity_Positive] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_inventory_StockMovements_Reason_NotBlank] CHECK (LEN(LTRIM(RTRIM([Reason]))) > 0),
        CONSTRAINT [CK_inventory_StockMovements_SourceType_NotBlank] CHECK (LEN(LTRIM(RTRIM([SourceType]))) > 0),
        CONSTRAINT [FK_inventory_StockMovements_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id]),
        CONSTRAINT [FK_inventory_StockMovements_StockBatches_StockBatchId] FOREIGN KEY ([StockBatchId]) REFERENCES [inventory].[StockBatches] ([Id]),
        CONSTRAINT [FK_inventory_StockMovements_StockLocations_StockLocationId] FOREIGN KEY ([StockLocationId]) REFERENCES [inventory].[StockLocations] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE TABLE [inventory].[InventoryCountLines] (
        [Id] bigint NOT NULL IDENTITY,
        [InventorySessionId] bigint NOT NULL,
        [ProductId] bigint NOT NULL,
        [StockBatchId] bigint NULL,
        [ExpectedQuantity] int NOT NULL,
        [CountedQuantity] int NULL,
        [LastOperationId] uniqueidentifier NULL,
        [LastOperationFingerprint] nvarchar(512) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_inventory_InventoryCountLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_inventory_InventoryCountLines_CountedQuantity_NonNegative] CHECK ([CountedQuantity] IS NULL OR [CountedQuantity] >= 0),
        CONSTRAINT [CK_inventory_InventoryCountLines_ExpectedQuantity_NonNegative] CHECK ([ExpectedQuantity] >= 0),
        CONSTRAINT [FK_inventory_InventoryCountLines_InventorySessions_InventorySessionId] FOREIGN KEY ([InventorySessionId]) REFERENCES [inventory].[InventorySessions] ([Id]),
        CONSTRAINT [FK_inventory_InventoryCountLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id]),
        CONSTRAINT [FK_inventory_InventoryCountLines_StockBatches_StockBatchId] FOREIGN KEY ([StockBatchId]) REFERENCES [inventory].[StockBatches] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_InventoryCountLines_ProductId] ON [inventory].[InventoryCountLines] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_InventoryCountLines_StockBatchId] ON [inventory].[InventoryCountLines] ([StockBatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_inventory_InventoryCountLines_LastOperationId] ON [inventory].[InventoryCountLines] ([LastOperationId]) WHERE [LastOperationId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_inventory_InventoryCountLines_Session_Product_Batch] ON [inventory].[InventoryCountLines] ([InventorySessionId], [ProductId], [StockBatchId]) WHERE [StockBatchId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_inventory_InventoryCountLines_Session_Product_NoBatch] ON [inventory].[InventoryCountLines] ([InventorySessionId], [ProductId]) WHERE [StockBatchId] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_inventory_InventorySessions_LocationId_Status] ON [inventory].[InventorySessions] ([StockLocationId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_inventory_InventorySessions_CancellationOperationId] ON [inventory].[InventorySessions] ([CancellationOperationId]) WHERE [CancellationOperationId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_inventory_InventorySessions_CreationOperationId] ON [inventory].[InventorySessions] ([CreationOperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_inventory_InventorySessions_FinalizationOperationId] ON [inventory].[InventorySessions] ([FinalizationOperationId]) WHERE [FinalizationOperationId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_inventory_InventorySessions_Reference] ON [inventory].[InventorySessions] ([Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_inventory_InventorySessions_StartOperationId] ON [inventory].[InventorySessions] ([StartOperationId]) WHERE [StartOperationId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_inventory_StockBalances_ProductId_StockLocationId] ON [inventory].[StockBalances] ([ProductId], [StockLocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_inventory_StockBalances_StockBatchId] ON [inventory].[StockBalances] ([StockBatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_inventory_StockBalances_Location_Product_Batch] ON [inventory].[StockBalances] ([StockLocationId], [ProductId], [StockBatchId]) WHERE [StockBatchId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_inventory_StockBalances_Location_Product_NoBatch] ON [inventory].[StockBalances] ([StockLocationId], [ProductId]) WHERE [StockBatchId] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_inventory_StockBatches_ExpiryDate] ON [inventory].[StockBatches] ([ExpiryDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_inventory_StockBatches_CreationOperationId] ON [inventory].[StockBatches] ([CreationOperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_inventory_StockBatches_ProductId_BatchNumber] ON [inventory].[StockBatches] ([ProductId], [BatchNumber]) WHERE [BatchNumber] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_inventory_StockLocations_Code] ON [inventory].[StockLocations] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_inventory_StockLocations_CreationOperationId] ON [inventory].[StockLocations] ([CreationOperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_inventory_StockLocations_LastOperationId] ON [inventory].[StockLocations] ([LastOperationId]) WHERE [LastOperationId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_inventory_StockLocations_OnePrimary] ON [inventory].[StockLocations] ([IsPrimary]) WHERE [IsPrimary] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_inventory_StockMovements_LocationId_OccurredAtUtc] ON [inventory].[StockMovements] ([StockLocationId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_inventory_StockMovements_ProductId_OccurredAtUtc] ON [inventory].[StockMovements] ([ProductId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_inventory_StockMovements_StockBatchId] ON [inventory].[StockMovements] ([StockBatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_inventory_StockMovements_OperationId] ON [inventory].[StockMovements] ([OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804181233_InventoryPersistenceV01'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260804181233_InventoryPersistenceV01', N'10.0.10');
END;

COMMIT;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    ALTER TABLE [inventory].[StockMovements] DROP CONSTRAINT [CK_inventory_StockMovements_Direction];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    IF SCHEMA_ID(N'purchasing') IS NULL EXEC(N'CREATE SCHEMA [purchasing];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE TABLE [purchasing].[Suppliers] (
        [Id] bigint NOT NULL IDENTITY,
        [Code] nvarchar(32) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Phone] nvarchar(32) NULL,
        [Email] nvarchar(254) NULL,
        [Address] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(7) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_purchasing_Suppliers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_purchasing_Suppliers_Code_NotBlank] CHECK (LEN(LTRIM(RTRIM([Code]))) > 0),
        CONSTRAINT [CK_purchasing_Suppliers_Name_NotBlank] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE TABLE [purchasing].[PurchaseOrders] (
        [Id] bigint NOT NULL IDENTITY,
        [OrderNumber] nvarchar(64) NOT NULL,
        [SupplierId] bigint NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [OrderDate] date NOT NULL,
        [ExpectedDate] date NULL,
        [Notes] nvarchar(1000) NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(7) NOT NULL,
        [ConfirmedAtUtc] datetime2(7) NULL,
        [CancelledAtUtc] datetime2(7) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_purchasing_PurchaseOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_purchasing_PurchaseOrders_Number_NotBlank] CHECK (LEN(LTRIM(RTRIM([OrderNumber]))) > 0),
        CONSTRAINT [FK_purchasing_PurchaseOrders_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [purchasing].[Suppliers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE TABLE [purchasing].[GoodsReceipts] (
        [Id] bigint NOT NULL IDENTITY,
        [ReceiptNumber] nvarchar(64) NOT NULL,
        [OperationId] uniqueidentifier NOT NULL,
        [OperationFingerprint] nvarchar(1024) NOT NULL,
        [SupplierId] bigint NOT NULL,
        [PurchaseOrderId] bigint NULL,
        [StockLocationId] bigint NOT NULL,
        [ReceiptType] nvarchar(32) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [DeliveryNoteNumber] nvarchar(128) NOT NULL,
        [DocumentDate] date NOT NULL,
        [ReceivedAtUtc] datetime2(7) NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_purchasing_GoodsReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_purchasing_GoodsReceipts_DeliveryNote_NotBlank] CHECK (LEN(LTRIM(RTRIM([DeliveryNoteNumber]))) > 0),
        CONSTRAINT [CK_purchasing_GoodsReceipts_Fingerprint_NotBlank] CHECK (LEN(LTRIM(RTRIM([OperationFingerprint]))) > 0),
        CONSTRAINT [FK_purchasing_GoodsReceipts_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [purchasing].[PurchaseOrders] ([Id]),
        CONSTRAINT [FK_purchasing_GoodsReceipts_StockLocations_StockLocationId] FOREIGN KEY ([StockLocationId]) REFERENCES [inventory].[StockLocations] ([Id]),
        CONSTRAINT [FK_purchasing_GoodsReceipts_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [purchasing].[Suppliers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE TABLE [purchasing].[PurchaseOrderLines] (
        [Id] bigint NOT NULL IDENTITY,
        [PurchaseOrderId] bigint NOT NULL,
        [ProductId] bigint NOT NULL,
        [ProductCodeSnapshot] nvarchar(64) NOT NULL,
        [ProductNameSnapshot] nvarchar(200) NOT NULL,
        [OrderedQuantity] int NOT NULL,
        [ReceivedQuantity] int NOT NULL,
        [UnitPurchasePrice] decimal(19,4) NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(7) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_purchasing_PurchaseOrderLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_purchasing_PurchaseOrderLines_Price] CHECK ([UnitPurchasePrice] IS NULL OR [UnitPurchasePrice] >= 0),
        CONSTRAINT [CK_purchasing_PurchaseOrderLines_Quantities] CHECK ([OrderedQuantity] > 0 AND [ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [OrderedQuantity]),
        CONSTRAINT [FK_purchasing_PurchaseOrderLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id]),
        CONSTRAINT [FK_purchasing_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [purchasing].[PurchaseOrders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE TABLE [purchasing].[GoodsReceiptLines] (
        [Id] bigint NOT NULL IDENTITY,
        [GoodsReceiptId] bigint NOT NULL,
        [ProductId] bigint NOT NULL,
        [PurchaseOrderLineId] bigint NULL,
        [Quantity] int NOT NULL,
        [UnitPurchasePrice] decimal(19,4) NOT NULL,
        [ProductCodeSnapshot] nvarchar(64) NOT NULL,
        [ProductNameSnapshot] nvarchar(200) NOT NULL,
        [StockBatchId] bigint NULL,
        [LotNumberSnapshot] nvarchar(80) NULL,
        [ExpiryDateSnapshot] date NULL,
        [StockMovementId] bigint NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_purchasing_GoodsReceiptLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_purchasing_GoodsReceiptLines_Price] CHECK ([UnitPurchasePrice] >= 0),
        CONSTRAINT [CK_purchasing_GoodsReceiptLines_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_purchasing_GoodsReceiptLines_GoodsReceipts_GoodsReceiptId] FOREIGN KEY ([GoodsReceiptId]) REFERENCES [purchasing].[GoodsReceipts] ([Id]),
        CONSTRAINT [FK_purchasing_GoodsReceiptLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id]),
        CONSTRAINT [FK_purchasing_GoodsReceiptLines_PurchaseOrderLines_PurchaseOrderLineId] FOREIGN KEY ([PurchaseOrderLineId]) REFERENCES [purchasing].[PurchaseOrderLines] ([Id]),
        CONSTRAINT [FK_purchasing_GoodsReceiptLines_StockBatches_StockBatchId] FOREIGN KEY ([StockBatchId]) REFERENCES [inventory].[StockBatches] ([Id]),
        CONSTRAINT [FK_purchasing_GoodsReceiptLines_StockMovements_StockMovementId] FOREIGN KEY ([StockMovementId]) REFERENCES [inventory].[StockMovements] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    EXEC(N'ALTER TABLE [inventory].[StockMovements] ADD CONSTRAINT [CK_inventory_StockMovements_Direction] CHECK ((([MovementType] IN (N''Opening'', N''ManualIncrease'', N''InventoryIncrease'', N''PurchaseReceipt'') AND [QuantityAfter] = [QuantityBefore] + [Quantity]) OR ([MovementType] IN (N''ManualDecrease'', N''InventoryDecrease'') AND [QuantityAfter] = [QuantityBefore] - [Quantity])))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_GoodsReceiptLines_ProductId] ON [purchasing].[GoodsReceiptLines] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_purchasing_GoodsReceiptLines_PurchaseOrderLineId] ON [purchasing].[GoodsReceiptLines] ([PurchaseOrderLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_purchasing_GoodsReceiptLines_Receipt_Product] ON [purchasing].[GoodsReceiptLines] ([GoodsReceiptId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_purchasing_GoodsReceiptLines_StockBatchId] ON [purchasing].[GoodsReceiptLines] ([StockBatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_purchasing_GoodsReceiptLines_StockMovementId] ON [purchasing].[GoodsReceiptLines] ([StockMovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_purchasing_GoodsReceipts_PurchaseOrderId] ON [purchasing].[GoodsReceipts] ([PurchaseOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_purchasing_GoodsReceipts_StockLocationId] ON [purchasing].[GoodsReceipts] ([StockLocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_purchasing_GoodsReceipts_SupplierId_ReceivedAtUtc] ON [purchasing].[GoodsReceipts] ([SupplierId], [ReceivedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_purchasing_GoodsReceipts_OperationId] ON [purchasing].[GoodsReceipts] ([OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_purchasing_GoodsReceipts_ReceiptNumber] ON [purchasing].[GoodsReceipts] ([ReceiptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_purchasing_GoodsReceipts_Supplier_DeliveryNote] ON [purchasing].[GoodsReceipts] ([SupplierId], [DeliveryNoteNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_purchasing_PurchaseOrderLines_ProductId] ON [purchasing].[PurchaseOrderLines] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_purchasing_PurchaseOrderLines_Order_Product] ON [purchasing].[PurchaseOrderLines] ([PurchaseOrderId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_purchasing_PurchaseOrders_Status_OrderDate] ON [purchasing].[PurchaseOrders] ([Status], [OrderDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_purchasing_PurchaseOrders_SupplierId_OrderDate] ON [purchasing].[PurchaseOrders] ([SupplierId], [OrderDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_purchasing_PurchaseOrders_OrderNumber] ON [purchasing].[PurchaseOrders] ([OrderNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_purchasing_Suppliers_IsActive_Name] ON [purchasing].[Suppliers] ([IsActive], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_purchasing_Suppliers_Code] ON [purchasing].[Suppliers] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804214802_PurchasingPersistenceV01'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260804214802_PurchasingPersistenceV01', N'10.0.10');
END;

COMMIT;
GO
