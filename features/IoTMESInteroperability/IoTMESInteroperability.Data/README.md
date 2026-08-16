# IoT MES Interoperability Data

`Cmf.Community.IoTMESInteroperability.Data` is the Data package for the IoT MES Interoperability feature. It deploys Connect IoT DEE actions and the master data definitions that connect IoT workflows to MES material, recipe, resource, metadata, and document operations.

## Contents

| Content | Location | Purpose |
| --- | --- | --- |
| Connect IoT DEEs | [`DEEs/ConnectIoT/`](DEEs/ConnectIoT/) | Custom actions for material operations, material lifecycle, recipes, resource state, documents, consumables, and metadata notifications. |
| Master data | [`MasterData/`](MasterData/) | Versioned DEE actions, rules, metadata resolutions, material tracking, recipe configuration, and operation definitions. |
| Exported objects | [`ExportedObjects/`](ExportedObjects/) | Reserved package location for exported MES objects. |

The current master-data version is `11331.0.0`. It configures operations including:

- Material operation execution and checking materials from a previous sub-resource.
- Creating new recipes, terminating materials, detaching consumables, and adjusting resource state.
- Creating external documents and notifying IoT of metadata changes.
- Metadata and resource-state resolution, material tracking, and default recipe type configuration.

The corresponding Connect IoT DEE implementations include `IoTMaterialOperation`, `IoTCheckGetMaterialsFromPreviousSubResource`, `IoTCreateNewRecipe`, `IoTTerminateMaterial`, `IoTDetachConsumables`, `IoTAdjustResourceState`, `IoTCreateExternalDocuments`, `NotifyMetadataChange`, and `NotifyIoTMetadataDefinitionChange`.

## `cmfpackage.json`

The manifest packages the following content:

| Source | Target | Content type |
| --- | --- | --- |
| `DEEs/*` | `DeeRules` | `DEE` |
| `DEEs/ProcessRules/EntityTypes/*` | `DeeRules/ProcessRules/EntityTypes/` | `EntityTypes` |
| `DEEs/ProcessRules/$(version)/Before/*` | `DeeRules/ProcessRules/$(version)/Before` | `ProcessRulesPre` |
| `DEEs/ProcessRules/$(version)/After/*` | `DeeRules/ProcessRules/$(version)/After` | `ProcessRulesPost` |
| `MasterData/$(version)/*` | `MasterData/$(version)/` | `MasterData` |
| `ExportedObjects/*` | `ExportedObjects` | `ExportedObjects` |

`$(version)` resolves to `11331.0.0` during packaging. This package is installable and uniquely installable. Its related-package configuration builds the sibling [`Business`](../IoTMESInteroperability.Business/) package before the Data package because the DEEs reference its compiled assemblies.

## Build and deployment

Build the related Business package first, then build and pack the Data package with the manifest. Deploy it as the Data dependency of the root `Cmf.Community.IoTMESInteroperability` package.