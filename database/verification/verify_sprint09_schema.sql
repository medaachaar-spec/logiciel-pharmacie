SET NOCOUNT ON;

SELECT
    CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS ServerName,
    COALESCE(CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')), N'(default)') AS InstanceName,
    CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')) AS ProductVersion,
    CONVERT(int, SERVERPROPERTY('ProductMajorVersion')) AS ProductMajorVersion,
    CONVERT(nvarchar(128), SERVERPROPERTY('Edition')) AS Edition,
    CONVERT(int, SERVERPROPERTY('EngineEdition')) AS EngineEdition,
    DB_NAME() AS DatabaseName;

SELECT
    MigrationId,
    ProductVersion
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;

SELECT
    schema_name(t.schema_id) AS SchemaName,
    t.name AS TableName,
    SUM(CASE WHEN p.index_id IN (0, 1) THEN p.rows ELSE 0 END) AS [RowCount]
FROM sys.tables AS t
LEFT JOIN sys.partitions AS p ON p.object_id = t.object_id
WHERE schema_name(t.schema_id) = N'catalog'
GROUP BY t.schema_id, t.name
ORDER BY t.name;

SELECT
    schema_name(t.schema_id) AS SchemaName,
    t.name AS TableName,
    c.column_id AS Ordinal,
    c.name AS ColumnName,
    ty.name AS SqlType,
    c.max_length AS MaxLengthBytes,
    c.precision AS [Precision],
    c.scale AS Scale,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    c.is_rowguidcol AS IsRowGuid,
    CASE WHEN ty.name IN (N'timestamp', N'rowversion') THEN 1 ELSE 0 END AS IsRowVersion
FROM sys.tables AS t
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
WHERE schema_name(t.schema_id) = N'catalog'
ORDER BY t.name, c.column_id;

SELECT
    schema_name(t.schema_id) AS SchemaName,
    t.name AS TableName,
    kc.name AS PrimaryKeyName,
    col.name AS ColumnName,
    ic.key_ordinal AS KeyOrdinal
FROM sys.tables AS t
JOIN sys.key_constraints AS kc
    ON kc.parent_object_id = t.object_id
    AND kc.type = N'PK'
JOIN sys.index_columns AS ic
    ON ic.object_id = kc.parent_object_id
    AND ic.index_id = kc.unique_index_id
JOIN sys.columns AS col
    ON col.object_id = ic.object_id
    AND col.column_id = ic.column_id
WHERE schema_name(t.schema_id) = N'catalog'
ORDER BY t.name, ic.key_ordinal;

SELECT
    fk.name AS ForeignKeyName,
    schema_name(parent_table.schema_id) AS ParentSchema,
    parent_table.name AS ParentTable,
    parent_column.name AS ParentColumn,
    schema_name(referenced_table.schema_id) AS ReferencedSchema,
    referenced_table.name AS ReferencedTable,
    referenced_column.name AS ReferencedColumn,
    fk.delete_referential_action_desc AS DeleteAction
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables AS parent_table ON parent_table.object_id = fk.parent_object_id
JOIN sys.columns AS parent_column
    ON parent_column.object_id = fkc.parent_object_id
    AND parent_column.column_id = fkc.parent_column_id
JOIN sys.tables AS referenced_table ON referenced_table.object_id = fk.referenced_object_id
JOIN sys.columns AS referenced_column
    ON referenced_column.object_id = fkc.referenced_object_id
    AND referenced_column.column_id = fkc.referenced_column_id
WHERE schema_name(parent_table.schema_id) = N'catalog'
ORDER BY fk.name;

SELECT
    schema_name(t.schema_id) AS SchemaName,
    t.name AS TableName,
    cc.name AS ConstraintName,
    cc.definition AS Definition
FROM sys.check_constraints AS cc
JOIN sys.tables AS t ON t.object_id = cc.parent_object_id
WHERE schema_name(t.schema_id) = N'catalog'
ORDER BY t.name, cc.name;

SELECT
    schema_name(t.schema_id) AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    i.is_unique AS IsUnique,
    i.has_filter AS HasFilter,
    i.filter_definition AS FilterDefinition,
    STRING_AGG(c.name, N', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS KeyColumns
FROM sys.indexes AS i
JOIN sys.tables AS t ON t.object_id = i.object_id
JOIN sys.index_columns AS ic
    ON ic.object_id = i.object_id
    AND ic.index_id = i.index_id
    AND ic.is_included_column = 0
JOIN sys.columns AS c
    ON c.object_id = ic.object_id
    AND c.column_id = ic.column_id
WHERE schema_name(t.schema_id) = N'catalog'
    AND i.is_hypothetical = 0
    AND i.name IS NOT NULL
GROUP BY t.schema_id, t.name, i.name, i.is_unique, i.has_filter, i.filter_definition
ORDER BY t.name, i.name;

SELECT
    schema_name(t.schema_id) AS SchemaName,
    t.name AS TableName,
    c.name AS RowVersionColumn
FROM sys.tables AS t
JOIN sys.columns AS c ON c.object_id = t.object_id
WHERE schema_name(t.schema_id) = N'catalog'
    AND c.system_type_id = 189
ORDER BY t.name, c.name;
