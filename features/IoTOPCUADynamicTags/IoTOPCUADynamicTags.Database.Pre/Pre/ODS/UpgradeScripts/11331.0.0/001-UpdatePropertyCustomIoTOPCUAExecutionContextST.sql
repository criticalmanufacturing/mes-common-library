DECLARE @EntityName NVARCHAR(512) = N'CustomIoTOPCUAExecutionContext'
DECLARE @IsEntityType BIT = 0
DECLARE @IsSmartTable BIT = 1
DECLARE @IsGenericTable BIT = 0
DECLARE @SqlStatement NVARCHAR(MAX) = N''

DECLARE @ObjectsWildcard NVARCHAR(512) = (
    CASE WHEN @IsEntityType = 1 THEN N'T_'
        WHEN @IsSmartTable = 1 THEN N'T_ST_'
        WHEN @IsGenericTable = 1 THEN N'T_GT_'
        ELSE N''
        END) + (CASE WHEN ISNULL(@EntityName, N'') = N'' THEN N'' ELSE (@EntityName + N'%') END)

IF(OBJECT_ID('tempdb..#ObjectsToDelete') IS NOT NULL)
    DROP TABLE #ObjectsToDelete

IF(OBJECT_ID('tempdb..#EligibleTablesForRemoval') IS NOT NULL)
    DROP TABLE #EligibleTablesForRemoval

;WITH BASE_DATA AS
(
    SELECT CASE SO.[Type]
                WHEN N'TF' THEN 'FUNCTION'
                WHEN N'P' THEN 'PROCEDURE'
                WHEN N'V' THEN 'VIEW'
                WHEN N'U' THEN 'TABLE'
                WHEN N'TT' THEN 'TYPE'
                WHEN N'IF' THEN 'FUNCTION'
            END [ObjectType]
        , CASE [Type]
                WHEN N'TF' THEN 1
                WHEN N'IF' THEN 1
                WHEN N'P' THEN 0
                WHEN N'V' THEN 2
                WHEN N'U' THEN 4
                WHEN N'TT' THEN 3
            END [Order]
        , (
            CASE [Type]
                WHEN 'U' THEN
                    (CASE WHEN SO.[Name] LIKE '%History' THEN 0 WHEN SO.[Name] LIKE '%Hst' THEN 1 ELSE 2 END)
                ELSE 0
                END
            ) [InnerOrder]
        , ISNULL(TT.[Name], SO.[Name]) [ObjectName]
        , ISNULL(SS2.[Name], SS.[Name]) [SchemaName]
    FROM [sys].[objects] SO
    INNER JOIN [sys].[schemas] SS ON SS.[Schema_id] = SO.[Schema_id]
    LEFT JOIN [sys].[table_types] TT ON TT.[type_table_object_id] = SO.[object_id]
    LEFT JOIN [sys].[schemas] SS2 ON SS2.[schema_id] = TT.[schema_id]
    WHERE SO.[Name] LIKE ('%' + @EntityName + '%')
        AND SO.[Type] NOT IN ('F', 'UQ', 'PK', 'TR')
)
SELECT N'DROP ' + BD.[ObjectType] + N' [' + BD.[SchemaName] + N'].[' + BD.[ObjectName] + N'];' [SqlStatement]
    , DENSE_RANK() OVER (PARTITION BY 1 ORDER BY BD.[Order], BD.[InnerOrder]) [OrderOfDeletion]
    , BD.*
INTO #ObjectsToDelete
FROM BASE_DATA BD

-- COLLECT TABLES TO BE REMOVED FROM REPLICATION TABLE
SELECT T.[name] [TableToDelete]
INTO #EligibleTablesForRemoval
FROM [sys].[tables] T
INNER JOIN [sys].[schemas] SS ON SS.[schema_id] = T.[schema_id]
WHERE T.[name] LIKE @ObjectsWildcard
    AND SS.[name] = 'UserDataModel'


WHILE(EXISTS(SELECT * FROM #ObjectsToDelete))
BEGIN
    SELECT @SqlStatement = MAX([SqlStatement])
    FROM #ObjectsToDelete
    WHERE [OrderOfDeletion] = (SELECT MIN([OrderOfDeletion]) FROM #ObjectsToDelete)

    EXEC sp_executesql @SqlStatement

    DELETE FROM #ObjectsToDelete WHERE [SqlStatement] = @SqlStatement
END

DELETE FROM [Control].[T_ReplicationTables] WHERE [TableName] IN (
    SELECT [TableToDelete]
    FROM #EligibleTablesForRemoval
) AND [SchemaName] = 'UserDataModel'

IF(@IsEntityType = 1)
BEGIN

    DELETE ETP
    FROM [dbo].[T_EntityType] ET
    INNER JOIN [dbo].[T_EntityTypeProperty] ETP ON ETP.[EntityTypeId] = ET.[EntityTypeId]
    WHERE ET.[Name] = @EntityName

    DELETE ETP
    FROM [dbo].[T_EntityType] ET
    INNER JOIN [dbo].[T_EntityTypePropertyHst] ETP ON ETP.[EntityTypeId] = ET.[EntityTypeId]
    WHERE ET.[Name] = @EntityName

    DELETE ET
    FROM [dbo].[T_EntityType] ET
    WHERE ET.[Name] = @EntityName

    DELETE ET
    FROM [dbo].[T_EntityTypeHst] ET
    WHERE ET.[Name] = @EntityName

END
ELSE IF(@IsSmartTable = 1)
BEGIN
    DELETE STPK
    FROM dbo.T_SmartTable ST
    INNER JOIN dbo.T_SmartTablePrecedenceKey STPK ON STPK.SmartTableId = ST.SmartTableId
    WHERE ST.Name = @EntityName

    DELETE STPKH
    FROM dbo.T_SmartTable ST
    INNER JOIN dbo.T_SmartTablePrecedenceKeyHst STPKH ON STPKH.SmartTableId = ST.SmartTableId
    WHERE ST.Name = @EntityName

    DELETE STP
    FROM dbo.T_SmartTable ST
    INNER JOIN dbo.T_SmartTableProperty STP ON STP.SmartTableId = ST.SmartTableId
    WHERE ST.Name = @EntityName

    DELETE STPH
    FROM dbo.T_SmartTable ST
    INNER JOIN dbo.T_SmartTablePropertyHst  STPH ON STPH.SmartTableId = ST.SmartTableId
    WHERE ST.Name = @EntityName

    DELETE ST
    FROM dbo.T_SmartTable ST
    WHERE ST.Name = @EntityName

    DELETE STH
    FROM dbo.T_SmartTable STH
    WHERE STH.Name = @EntityName

END
ELSE IF(@IsGenericTable = 1)
BEGIN

    DELETE GTP
    FROM dbo.T_GenericTable GT
    INNER JOIN dbo.T_GenericTableProperty GTP ON GTP.GenericTableId = GT.GenericTableId
    WHERE GT.Name = @EntityName

    DELETE GTPH
    FROM dbo.T_GenericTable GT
    INNER JOIN dbo.T_GenericTablePropertyHst GTPH ON GTPH.GenericTableId = GT.GenericTableId
    WHERE GT.Name = @EntityName

    DELETE GT
    FROM dbo.T_GenericTable GT
    WHERE GT.Name = @EntityName

    DELETE GTH
    FROM dbo.T_GenericTable GTH
    WHERE GTH.Name = @EntityName

END
GO