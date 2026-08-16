# IoT OPC UA Dynamic Tags Database Pre

`Cmf.Community.IoTOPCUADynamicTags.Database.Pre` is the database pre-deployment package for the IoT OPC UA Dynamic Tags feature. It applies the database changes required before the feature's MES data and Connect IoT configuration are installed.

## Contents

The package contains the same versioned upgrade script for both database targets:

| Database target | Script |
| --- | --- |
| Online | [`ONLINE/UpgradeScripts/1331.0.0/001-UpdatePropertyCustomIoTOPCUAExecutionContextST.sql`](ONLINE/UpgradeScripts/1331.0.0/001-UpdatePropertyCustomIoTOPCUAExecutionContextST.sql) |
| ODS | [`ODS/UpgradeScripts/1331.0.0/001-UpdatePropertyCustomIoTOPCUAExecutionContextST.sql`](ODS/UpgradeScripts/1331.0.0/001-UpdatePropertyCustomIoTOPCUAExecutionContextST.sql) |

The script updates the `CustomIoTOPCUAExecutionContextST` property for the feature's OPC UA execution-context support. The Online and ODS copies keep both supported database targets aligned.

## `cmfpackage.json`

The manifest is a `Database` package and packs:

| Source | Target |
| --- | --- |
| `ONLINE/UpgradeScripts/$(version)/*` | `Online/` |
| `ODS/UpgradeScripts/$(version)/*` | `ODS/` |

For this release, `$(version)` resolves to `1331.0.0` from package version `11331.0.0`. The package is installable and uniquely installable, with no additional package dependencies or custom deployment steps.

## Deployment

Install this package as the database pre-deployment dependency of the root `Cmf.Community.IoTOPCUADynamicTags` package. Run it before installing the Data package so the database property required by the deployed DEE and context-resolution configuration is available.