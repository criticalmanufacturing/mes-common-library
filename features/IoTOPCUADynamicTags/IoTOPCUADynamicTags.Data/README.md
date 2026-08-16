# IoT OPC UA Dynamic Tags Data

`Cmf.Community.IoTOPCUADynamicTags.Data` is the Data package for the IoT OPC UA Dynamic Tags feature. It deploys the compiled DEE assembly and the MES/Connect IoT master data that define the dynamic-tags controller and its supporting configuration.

## Contents

| Content | Location | Purpose |
| --- | --- | --- |
| DEE code | [`DEEs/`](DEEs/) | Compiled custom logic, including `CustomIoTOPCUAExecutionContextST`. |
| Master data | [`MasterData/`](MasterData/) | Versioned automation protocol, controller, workflows, manager, and context-resolution definitions. |
| Exported objects | [`ExportedObjects/`](ExportedObjects/) | Reserved package location for exported MES objects. |

The current master-data version is `1331.0.0`. It includes:

- `900_IoT_AutomationProtocol.json`, which defines the OPC UA protocol and dynamic-tags driver.
- `901_IoT_DynamicTags.json`, which defines the `DynamicTags_Controller`, handler, and workflows.
- `902_IoT_DynamicTags_Manager.json`, which defines the manually deployed `DynamicTags_Manager`.
- Custom OPC UA recipe, execution-context, and data-collection context-resolution definitions.

## `cmfpackage.json`

The manifest packages the following content:

| Source | Target | Content type |
| --- | --- | --- |
| `DEEs/*` | `DeeRules` | `DEE` |
| `DEEs/ProcessRules/EntityTypes/*` | `DeeRules/ProcessRules/EntityTypes/` | `EntityTypes` |
| Versioned process-rule folders | Corresponding `DeeRules/ProcessRules/$(version)/` folders | `ProcessRulesPre` / `ProcessRulesPost` |
| `MasterData/$(version)/*` | `MasterData/$(version)/` | `MasterData` |
| `ExportedObjects/*` | `ExportedObjects` | `ExportedObjects` |

`$(version)` is replaced with the package version, `11331.0.0`, during packaging. This package is installable and uniquely installable.

## Build and deployment

Build the DEE project under [`DEEs/`](DEEs/) using the feature's configured MES/.NET toolchain, then pack the package using the manifest. Deploy it as the Data dependency of the root `Cmf.Community.IoTOPCUADynamicTags` package. The root feature also depends on `Cmf.Community.IoTMESInteroperability`.