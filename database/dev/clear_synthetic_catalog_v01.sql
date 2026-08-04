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
    THROW 51020, 'Refus du nettoyage : la base cible doit être Pharmacy_Development.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @DemoProducts table (ProductId bigint NOT NULL PRIMARY KEY);

    INSERT INTO @DemoProducts (ProductId)
    SELECT ProductId
    FROM catalog.ProductLocalSettings
    WHERE InternalCode LIKE N'DEMO-%';

    DELETE history
    FROM catalog.ProductPriceHistories AS history
    JOIN @DemoProducts AS demo ON demo.ProductId = history.ProductId;

    DELETE barcode
    FROM catalog.ProductBarcodes AS barcode
    JOIN @DemoProducts AS demo ON demo.ProductId = barcode.ProductId;

    DELETE settings
    FROM catalog.ProductLocalSettings AS settings
    JOIN @DemoProducts AS demo ON demo.ProductId = settings.ProductId;

    DELETE product
    FROM catalog.Products AS product
    JOIN @DemoProducts AS demo ON demo.ProductId = product.Id;

    DELETE category
    FROM catalog.ProductCategories AS category
    WHERE category.Code LIKE N'DEMO-%'
      AND NOT EXISTS
      (
          SELECT 1
          FROM catalog.ProductLocalSettings AS settings
          WHERE settings.CategoryId = category.Id
      );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT N'Seules les références DEMO-* et leurs dépendances ont été supprimées.' AS Result;
