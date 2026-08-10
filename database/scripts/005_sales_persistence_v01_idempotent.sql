BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    ALTER TABLE [inventory].[StockMovements] DROP CONSTRAINT [CK_inventory_StockMovements_Direction];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    IF SCHEMA_ID(N'sales') IS NULL EXEC(N'CREATE SCHEMA [sales];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE TABLE [sales].[Payments] (
        [Id] bigint NOT NULL IDENTITY,
        [SaleId] bigint NOT NULL,
        [OperationId] uniqueidentifier NOT NULL,
        [PaymentMethod] nvarchar(32) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [Amount] decimal(19,4) NOT NULL,
        [TenderedAmount] decimal(19,4) NULL,
        [ChangeGiven] decimal(19,4) NOT NULL,
        [ExternalCardConfirmed] bit NOT NULL,
        [ConfirmedAtUtc] datetime2(7) NOT NULL,
        CONSTRAINT [PK_sales_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_sales_Payments_Amounts_NonNegative] CHECK ([Amount] >= 0 AND ([TenderedAmount] IS NULL OR [TenderedAmount] >= 0) AND [ChangeGiven] >= 0),
        CONSTRAINT [CK_sales_Payments_Card_Coherent] CHECK (([PaymentMethod] <> N'ExternalCard') OR ([ExternalCardConfirmed] = 1 AND [TenderedAmount] IS NULL AND [ChangeGiven] = 0)),
        CONSTRAINT [CK_sales_Payments_Cash_Coherent] CHECK (([PaymentMethod] <> N'Cash') OR ([TenderedAmount] IS NOT NULL AND [TenderedAmount] >= [Amount] AND [ChangeGiven] = [TenderedAmount] - [Amount] AND [ExternalCardConfirmed] = 0)),
        CONSTRAINT [CK_sales_Payments_PaymentMethod] CHECK ([PaymentMethod] IN (N'Cash', N'ExternalCard')),
        CONSTRAINT [CK_sales_Payments_Status_Completed] CHECK ([Status] = N'Completed')
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE TABLE [sales].[SaleLineAllocations] (
        [Id] bigint NOT NULL IDENTITY,
        [SaleLineId] bigint NOT NULL,
        [StockBalanceId] bigint NOT NULL,
        [StockBatchId] bigint NULL,
        [Quantity] int NOT NULL,
        [BatchNumberSnapshot] nvarchar(128) NULL,
        [ExpiryDateSnapshot] date NULL,
        CONSTRAINT [PK_sales_SaleLineAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_sales_SaleLineAllocations_Quantity_Positive] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_sales_SaleLineAllocations_StockBalances_StockBalanceId] FOREIGN KEY ([StockBalanceId]) REFERENCES [inventory].[StockBalances] ([Id]),
        CONSTRAINT [FK_sales_SaleLineAllocations_StockBatches_StockBatchId] FOREIGN KEY ([StockBatchId]) REFERENCES [inventory].[StockBatches] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE TABLE [sales].[SaleLines] (
        [Id] bigint NOT NULL IDENTITY,
        [SaleId] bigint NOT NULL,
        [ProductId] bigint NOT NULL,
        [ProductReferenceSnapshot] nvarchar(128) NOT NULL,
        [ProductNameSnapshot] nvarchar(256) NOT NULL,
        [PresentationSnapshot] nvarchar(256) NULL,
        [UnitPrice] decimal(19,4) NOT NULL,
        [Quantity] int NOT NULL,
        [LineTotal] decimal(19,4) NOT NULL,
        CONSTRAINT [PK_sales_SaleLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_sales_SaleLines_Amounts_NonNegative] CHECK ([UnitPrice] >= 0 AND [LineTotal] >= 0),
        CONSTRAINT [CK_sales_SaleLines_Name_NotBlank] CHECK (LEN(LTRIM(RTRIM([ProductNameSnapshot]))) > 0),
        CONSTRAINT [CK_sales_SaleLines_Quantity_Positive] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_sales_SaleLines_Reference_NotBlank] CHECK (LEN(LTRIM(RTRIM([ProductReferenceSnapshot]))) > 0),
        CONSTRAINT [CK_sales_SaleLines_Total_Coherent] CHECK ([LineTotal] = [UnitPrice] * [Quantity]),
        CONSTRAINT [FK_sales_SaleLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [catalog].[Products] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE TABLE [sales].[SaleOperations] (
        [Id] bigint NOT NULL IDENTITY,
        [OperationId] uniqueidentifier NOT NULL,
        [OperationFingerprint] nvarchar(64) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [ResultSaleId] bigint NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL,
        [CompletedAtUtc] datetime2(7) NULL,
        CONSTRAINT [PK_sales_SaleOperations] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_sales_SaleOperations_OperationId] UNIQUE ([OperationId]),
        CONSTRAINT [CK_sales_SaleOperations_Completion_Coherent] CHECK (([Status] = N'Processing' AND [ResultSaleId] IS NULL AND [CompletedAtUtc] IS NULL) OR ([Status] = N'Completed' AND [ResultSaleId] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL)),
        CONSTRAINT [CK_sales_SaleOperations_Fingerprint_NotBlank] CHECK (LEN(LTRIM(RTRIM([OperationFingerprint]))) > 0),
        CONSTRAINT [CK_sales_SaleOperations_Status] CHECK ([Status] IN (N'Processing', N'Completed'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE TABLE [sales].[Sales] (
        [Id] bigint NOT NULL IDENTITY,
        [Reference] nvarchar(64) NOT NULL,
        [OperationId] uniqueidentifier NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [CompletedAtUtc] datetime2(7) NOT NULL,
        [GrandTotal] decimal(19,4) NOT NULL,
        [PaidTotal] decimal(19,4) NOT NULL,
        [ChangeGiven] decimal(19,4) NOT NULL,
        [TotalQuantity] bigint NOT NULL,
        [LineCount] int NOT NULL,
        [PaymentMethod] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_sales_Sales] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_sales_Sales_Amounts_NonNegative] CHECK ([GrandTotal] >= 0 AND [PaidTotal] >= 0 AND [ChangeGiven] >= 0),
        CONSTRAINT [CK_sales_Sales_Counts_Positive] CHECK ([TotalQuantity] > 0 AND [LineCount] > 0),
        CONSTRAINT [CK_sales_Sales_PaymentMethod] CHECK ([PaymentMethod] IN (N'Cash', N'ExternalCard')),
        CONSTRAINT [CK_sales_Sales_Reference_NotBlank] CHECK (LEN(LTRIM(RTRIM([Reference]))) > 0),
        CONSTRAINT [CK_sales_Sales_Status_Completed] CHECK ([Status] = N'Completed'),
        CONSTRAINT [CK_sales_Sales_Totals_Coherent] CHECK ([PaidTotal] = [GrandTotal]),
        CONSTRAINT [FK_sales_Sales_SaleOperations_OperationId] FOREIGN KEY ([OperationId]) REFERENCES [sales].[SaleOperations] ([OperationId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    EXEC(N'ALTER TABLE [inventory].[StockMovements] ADD CONSTRAINT [CK_inventory_StockMovements_Direction] CHECK ((([MovementType] IN (N''Opening'', N''ManualIncrease'', N''InventoryIncrease'', N''PurchaseReceipt'') AND [QuantityAfter] = [QuantityBefore] + [Quantity]) OR ([MovementType] IN (N''ManualDecrease'', N''InventoryDecrease'', N''Sale'') AND [QuantityAfter] = [QuantityBefore] - [Quantity])))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_sales_Payments_SaleId] ON [sales].[Payments] ([SaleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_sales_Payments_OperationId] ON [sales].[Payments] ([OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_sales_SaleLineAllocations_StockBalanceId] ON [sales].[SaleLineAllocations] ([StockBalanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_sales_SaleLineAllocations_StockBatchId] ON [sales].[SaleLineAllocations] ([StockBatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_sales_SaleLineAllocations_Line_Balance] ON [sales].[SaleLineAllocations] ([SaleLineId], [StockBalanceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_sales_SaleLines_ProductId] ON [sales].[SaleLines] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_sales_SaleLines_SaleId] ON [sales].[SaleLines] ([SaleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_sales_SaleOperations_CreatedAtUtc] ON [sales].[SaleOperations] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_sales_SaleOperations_ResultSaleId] ON [sales].[SaleOperations] ([ResultSaleId]) WHERE [ResultSaleId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE INDEX [IX_sales_Sales_CompletedAtUtc_Id] ON [sales].[Sales] ([CompletedAtUtc], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_sales_Sales_OperationId] ON [sales].[Sales] ([OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    CREATE UNIQUE INDEX [UX_sales_Sales_Reference] ON [sales].[Sales] ([Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    ALTER TABLE [sales].[Payments] ADD CONSTRAINT [FK_sales_Payments_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [sales].[Sales] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    ALTER TABLE [sales].[SaleLineAllocations] ADD CONSTRAINT [FK_sales_SaleLineAllocations_SaleLines_SaleLineId] FOREIGN KEY ([SaleLineId]) REFERENCES [sales].[SaleLines] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    ALTER TABLE [sales].[SaleLines] ADD CONSTRAINT [FK_sales_SaleLines_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [sales].[Sales] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    ALTER TABLE [sales].[SaleOperations] ADD CONSTRAINT [FK_sales_SaleOperations_Sales_ResultSaleId] FOREIGN KEY ([ResultSaleId]) REFERENCES [sales].[Sales] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810103858_SalesPersistenceV01'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260810103858_SalesPersistenceV01', N'10.0.10');
END;

COMMIT;
GO
