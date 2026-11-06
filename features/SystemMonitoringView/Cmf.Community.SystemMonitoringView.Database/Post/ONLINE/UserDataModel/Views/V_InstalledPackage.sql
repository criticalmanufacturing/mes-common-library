-- Latest installed version per package id, read from the EnvManager installation journal.
-- The journal (dbo.T_PackageInfo) is written by the Setup tool and is not a public contract:
-- this view is the only place that knows its shape.
-- Installed rows: Status = 1 and RemovedBy empty (an empty string on installed rows, not NULL).
-- Latest = most recent InstalledOn per PackageId (ties: highest PackageInfoId).
CREATE OR ALTER VIEW [UserDataModel].[V_InstalledPackage]
AS
SELECT [PackageId], [PackageVersion], [InstalledOn]
FROM (
    SELECT [PackageId], [PackageVersion], [InstalledOn],
           ROW_NUMBER() OVER (PARTITION BY [PackageId] ORDER BY [InstalledOn] DESC, [PackageInfoId] DESC) AS [Rn]
    FROM [dbo].[T_PackageInfo]
    WHERE [Status] = 1 AND ([RemovedBy] IS NULL OR [RemovedBy] = '')
) AS [P]
WHERE [P].[Rn] = 1
