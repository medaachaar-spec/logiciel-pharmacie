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

