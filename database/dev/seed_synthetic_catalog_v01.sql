SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF DB_NAME() <> N'Pharmacy_Development'
    THROW 51010, 'Refus du seed : la base cible doit être Pharmacy_Development.', 1;

IF OBJECT_ID(N'catalog.Products', N'U') IS NULL
    OR COL_LENGTH(N'catalog.Products', N'IsActive') IS NULL
    THROW 51011, 'Le schéma Catalogue Sprint 10 et sa migration doivent être appliqués avant le seed.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Categories table
    (
        Code nvarchar(64) NOT NULL PRIMARY KEY,
        Name nvarchar(160) NOT NULL,
        SortOrder int NOT NULL
    );

    INSERT INTO @Categories (Code, Name, SortOrder)
    VALUES
        (N'DEMO-CAT-PRODUITS', N'Produits Demo', 10),
        (N'DEMO-CAT-HYGIENE', N'Hygiène Demo', 20),
        (N'DEMO-CAT-SOIN', N'Soin Demo', 30),
        (N'DEMO-CAT-ACCESSOIRES', N'Accessoires Demo', 40);

    INSERT INTO catalog.ProductCategories
        (Code, Name, IsActive, DefaultTracksLots, DefaultTracksExpiry, DefaultDiscountAllowed, SortOrder)
    SELECT source.Code, source.Name, 1, 0, 0, 0, source.SortOrder
    FROM @Categories AS source
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM catalog.ProductCategories AS target
        WHERE target.Code = source.Code
    );

    DECLARE @Products table
    (
        InternalCode nvarchar(128) NOT NULL PRIMARY KEY,
        Name nvarchar(256) NOT NULL,
        Presentation nvarchar(256) NOT NULL,
        SalePrice decimal(19,4) NOT NULL,
        Barcode nvarchar(128) NOT NULL,
        CategoryCode nvarchar(64) NOT NULL
    );

    INSERT INTO @Products (InternalCode, Name, Presentation, SalePrice, Barcode, CategoryCode)
    VALUES
        (N'DEMO-000001', N'Produit Démo A', N'Boîte 20 unités', 12.5000, N'DEMO-BARCODE-000001', N'DEMO-CAT-PRODUITS'),
        (N'DEMO-000002', N'Produit Démo B', N'Flacon 100 ml', 18.9000, N'DEMO-BARCODE-000002', N'DEMO-CAT-PRODUITS'),
        (N'DEMO-000003', N'Produit Démo C', N'Tube 50 g', 24.0000, N'DEMO-BARCODE-000003', N'DEMO-CAT-PRODUITS'),
        (N'DEMO-000004', N'Produit Démo D', N'Sachet 12 unités', 34.7500, N'DEMO-BARCODE-000004', N'DEMO-CAT-PRODUITS'),
        (N'DEMO-000005', N'Produit Démo E', N'Boîte 30 unités', 49.9000, N'DEMO-BARCODE-000005', N'DEMO-CAT-PRODUITS'),
        (N'DEMO-000006', N'Article Démo Hygiène A', N'Flacon 250 ml', 27.5000, N'DEMO-BARCODE-000006', N'DEMO-CAT-HYGIENE'),
        (N'DEMO-000007', N'Article Démo Parapharmacie A', N'Tube 75 ml', 39.9000, N'DEMO-BARCODE-000007', N'DEMO-CAT-SOIN'),
        (N'DEMO-000008', N'Produit Démo F', N'Boîte 10 unités', 15.2500, N'DEMO-BARCODE-000008', N'DEMO-CAT-PRODUITS'),
        (N'DEMO-000009', N'Article Démo Soin A', N'Flacon 50 ml', 55.0000, N'DEMO-BARCODE-000009', N'DEMO-CAT-SOIN'),
        (N'DEMO-000010', N'Article Démo Accessoire A', N'Unité', 8.7500, N'DEMO-BARCODE-000010', N'DEMO-CAT-ACCESSOIRES');

    DECLARE @ProductMap table
    (
        InternalCode nvarchar(128) NOT NULL PRIMARY KEY,
        ProductId bigint NOT NULL
    );

    INSERT INTO @ProductMap (InternalCode, ProductId)
    SELECT source.InternalCode, settings.ProductId
    FROM @Products AS source
    JOIN catalog.ProductLocalSettings AS settings ON settings.InternalCode = source.InternalCode;

    DECLARE @InsertedProducts table
    (
        InternalCode nvarchar(128) NOT NULL PRIMARY KEY,
        ProductId bigint NOT NULL
    );

    MERGE catalog.Products AS target
    USING
    (
        SELECT source.*
        FROM @Products AS source
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM @ProductMap AS mapped
            WHERE mapped.InternalCode = source.InternalCode
        )
    ) AS source
    ON 1 = 0
    WHEN NOT MATCHED THEN
        INSERT (Name, PackageDescription, IsActive, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.Name, source.Presentation, 1, SYSUTCDATETIME(), SYSUTCDATETIME())
    OUTPUT source.InternalCode, inserted.Id INTO @InsertedProducts (InternalCode, ProductId);

    INSERT INTO @ProductMap (InternalCode, ProductId)
    SELECT InternalCode, ProductId
    FROM @InsertedProducts;

    INSERT INTO catalog.ProductLocalSettings
        (ProductId, InternalCode, CategoryId, IsActive, IsSellable, IsPurchasable,
         TracksLots, TracksExpiry, DiscountAllowed, QuantityScale, StorageLocation,
         MinimumStock, MaximumStock, CurrentSalePrice, CurrencyCode)
    SELECT mapped.ProductId, source.InternalCode, category.Id, 1, 1, 1,
           0, 0, 0, 0, NULL, NULL, NULL, source.SalePrice, N'MAD'
    FROM @InsertedProducts AS inserted
    JOIN @ProductMap AS mapped ON mapped.InternalCode = inserted.InternalCode
    JOIN @Products AS source ON source.InternalCode = inserted.InternalCode
    JOIN catalog.ProductCategories AS category ON category.Code = source.CategoryCode;

    INSERT INTO catalog.ProductBarcodes
        (ProductId, Value, NormalizedValue, IsPrimary, BarcodeType, IsActive, CreatedAtUtc)
    SELECT mapped.ProductId, source.Barcode, UPPER(LTRIM(RTRIM(source.Barcode))), 1, N'Demo', 1, SYSUTCDATETIME()
    FROM @Products AS source
    JOIN @ProductMap AS mapped ON mapped.InternalCode = source.InternalCode
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM catalog.ProductBarcodes AS barcode
        WHERE barcode.NormalizedValue = UPPER(LTRIM(RTRIM(source.Barcode)))
    );

    INSERT INTO catalog.ProductPriceHistories
        (ProductId, OldPrice, NewPrice, CurrencyCode, ChangeOrigin, Reason, EffectiveAtUtc)
    SELECT mapped.ProductId, NULL, source.SalePrice, N'MAD', N'InitialOnboarding',
           N'Seed synthétique Catalogue V0.1', SYSUTCDATETIME()
    FROM @Products AS source
    JOIN @ProductMap AS mapped ON mapped.InternalCode = source.InternalCode
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM catalog.ProductPriceHistories AS history
        WHERE history.ProductId = mapped.ProductId
          AND history.ChangeOrigin = N'InitialOnboarding'
    );

    IF (SELECT COUNT(*) FROM @ProductMap) <> 10
        THROW 51012, 'Le seed n''a pas résolu exactement dix produits Demo.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT N'Seed Catalogue synthétique V0.1 appliqué sans duplication.' AS Result;
