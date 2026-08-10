SET NOCOUNT ON;

IF DB_NAME() <> N'Pharmacy_Development'
    THROW 51030, 'La vérification Sprint 10 doit cibler Pharmacy_Development.', 1;

IF OBJECT_ID(N'catalog.Products', N'U') IS NULL
    OR OBJECT_ID(N'catalog.ProductLocalSettings', N'U') IS NULL
    OR OBJECT_ID(N'catalog.ProductBarcodes', N'U') IS NULL
    OR OBJECT_ID(N'catalog.ProductPriceHistories', N'U') IS NULL
    OR OBJECT_ID(N'catalog.ProductCategories', N'U') IS NULL
    THROW 51031, 'Une table Catalogue attendue est absente.', 1;

IF COL_LENGTH(N'catalog.Products', N'IsActive') IS NULL
    THROW 51032, 'La colonne catalog.Products.IsActive est absente.', 1;

DECLARE @DemoProductCount int =
(
    SELECT COUNT(*)
    FROM catalog.ProductLocalSettings
    WHERE InternalCode IN
    (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
     N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010')
);

IF @DemoProductCount <> 10
    THROW 51033, 'Le Catalogue doit contenir exactement dix références DEMO-* pour cette validation.', 1;

IF EXISTS
(
    SELECT InternalCode
    FROM catalog.ProductLocalSettings
    WHERE InternalCode IN
    (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
     N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010')
    GROUP BY InternalCode
    HAVING COUNT(*) <> 1
)
    THROW 51034, 'Une référence Demo est dupliquée.', 1;

IF EXISTS
(
    SELECT NormalizedValue
    FROM catalog.ProductBarcodes
    WHERE NormalizedValue LIKE N'DEMO-BARCODE-%'
    GROUP BY NormalizedValue
    HAVING COUNT(*) <> 1
)
    THROW 51035, 'Un barcode Demo est dupliqué.', 1;

IF EXISTS
(
    SELECT settings.ProductId
    FROM catalog.ProductLocalSettings AS settings
    WHERE settings.InternalCode IN
    (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
     N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010')
      AND NOT EXISTS
      (
          SELECT 1
          FROM catalog.ProductPriceHistories AS history
          WHERE history.ProductId = settings.ProductId
      )
)
    THROW 51036, 'Un produit Demo ne possède aucun historique de prix.', 1;

IF EXISTS
(
    SELECT 1
    FROM catalog.ProductLocalSettings AS settings
    JOIN catalog.Products AS product ON product.Id = settings.ProductId
    WHERE settings.InternalCode IN
    (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
     N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010')
      AND (product.RowVersion IS NULL OR settings.RowVersion IS NULL)
)
    THROW 51037, 'Une rowversion produit ou paramètres locaux est absente.', 1;

SELECT
    DB_NAME() AS DatabaseName,
    d.compatibility_level AS CompatibilityLevel,
    CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')) AS ProductVersion,
    CONVERT(nvarchar(128), SERVERPROPERTY('Edition')) AS Edition
FROM sys.databases AS d
WHERE d.name = DB_NAME();

SELECT MigrationId, ProductVersion
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;

SELECT
    (SELECT COUNT(*) FROM catalog.ProductLocalSettings WHERE InternalCode IN
     (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
      N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010')) AS DemoProducts,
    (SELECT COUNT(*)
     FROM catalog.ProductLocalSettings AS settings
     JOIN catalog.Products AS product ON product.Id = settings.ProductId
     WHERE settings.InternalCode IN
     (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
      N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010')
       AND product.IsActive = 1 AND settings.IsActive = 1) AS ActiveDemoProducts,
    (SELECT COUNT(DISTINCT CategoryId) FROM catalog.ProductLocalSettings WHERE InternalCode IN
     (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
      N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010')) AS DemoCategories,
    (SELECT COUNT(*)
     FROM catalog.ProductBarcodes AS barcode
     JOIN catalog.ProductLocalSettings AS settings ON settings.ProductId = barcode.ProductId
     WHERE settings.InternalCode IN
     (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
      N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010')) AS DemoBarcodes,
    (SELECT COUNT(*)
     FROM catalog.ProductPriceHistories AS history
     JOIN catalog.ProductLocalSettings AS settings ON settings.ProductId = history.ProductId
     WHERE settings.InternalCode IN
     (N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
      N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010')) AS DemoPriceHistoryRows;

SELECT
    settings.InternalCode,
    product.Name,
    product.PackageDescription,
    category.Name AS CategoryName,
    settings.CurrentSalePrice,
    settings.CurrencyCode,
    product.IsActive,
    settings.IsActive AS IsLocallyActive,
    barcode.Value AS PrimaryBarcode,
    sys.fn_varbintohexstr(product.RowVersion) AS ProductRowVersion,
    sys.fn_varbintohexstr(settings.RowVersion) AS SettingsRowVersion
FROM catalog.ProductLocalSettings AS settings
JOIN catalog.Products AS product ON product.Id = settings.ProductId
JOIN catalog.ProductCategories AS category ON category.Id = settings.CategoryId
LEFT JOIN catalog.ProductBarcodes AS barcode
    ON barcode.ProductId = settings.ProductId
    AND barcode.IsPrimary = 1
    AND barcode.IsActive = 1
WHERE settings.InternalCode IN
(N'DEMO-000001', N'DEMO-000002', N'DEMO-000003', N'DEMO-000004', N'DEMO-000005',
 N'DEMO-000006', N'DEMO-000007', N'DEMO-000008', N'DEMO-000009', N'DEMO-000010')
ORDER BY settings.InternalCode;
