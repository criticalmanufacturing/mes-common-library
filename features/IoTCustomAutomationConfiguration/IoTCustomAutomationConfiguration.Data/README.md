# IoT Custom Automation Configuration — Data Package

A Critical Manufacturing MES Community Data package that ships master data definitions, dynamic entity extensions (DEEs), and business rule configurations for custom automation of IoT-connected equipment. This package defines the data models, automation rules, and process hooks that extend the MES framework with custom automation capabilities.

## Package overview

The `IoTCustomAutomationConfiguration.Data` package is a `Data` package type, which is designed for deployment of master data, DEE rules, and entity type extensions. It is part of the larger `IoTCustomAutomationConfiguration` feature and depends on the sibling `IoTCustomAutomationConfiguration.Business` package, whose assemblies are referenced by the DEE code.

| Component | Location | Purpose |
| --- | --- | --- |
| **DEE Code** | [`DEEs/`](DEEs/) | C# business logic for automation actions, process rules, and entity extensions. |
| **Master Data** | [`MasterData/`](MasterData/) | Versioned and baseline JSON configuration data for entities, lookups, and initial metadata. |
| **Exported Objects** | [`ExportedObjects/`](ExportedObjects/) | XML exported entity type extensions and custom business logic configurations. |

## What is shipped

### DEEs (Dynamic Entity Extensions)

The [`DEEs/`](DEEs/) folder contains C# implementations of custom automation logic and entity-type extensions. These are compiled into the Data package and executed in the MES environment to extend standard behavior:

- **Automation Actions** ([`DEEs/Automation/`](DEEs/Automation/)): Four custom automation action classes that manage IoT custom automation configuration lifecycle—create, update, and retrieve configuration entities and metadata.
- **Entity Types** ([`DEEs/ProcessRules/EntityTypes/`](DEEs/ProcessRules/EntityTypes/)): Entity type extension that creates related entity relationships and initializes automation configurations when IoT custom automation is enabled.
- **Name Generators** ([`DEEs/NameGenerator/`](DEEs/NameGenerator/)): Name generation logic for auto-naming automation-related entities.
- **Process Rules**: Baseline and version-specific pre-process and post-process hooks under [`DEEs/ProcessRules/`](DEEs/ProcessRules/) that enforce business logic during entity lifecycle events.

All DEE code is compiled as part of the package build. These classes inherit from `DeeDevBase` and reference the `Cmf.Community.IoTCustomAutomationConfiguration.Actions` namespace, which is provided by the sibling `.Business` package.

### Master Data

The [`MasterData/`](MasterData/) folder contains versioned and baseline JSON data that seeds the MES database. Refer to [`MasterData/readme.md`](MasterData/readme.md) for folder naming conventions.

- **Baseline** ([`MasterData/Baseline/`](MasterData/Baseline/)): Default data that is deployed with every installation:
  - `DEEs/`: Automation actions and automation rules as JSON configuration.
  - `Model/`: Entity type definitions, custom metadata, name generators, and localized messages.
- **Versioned Data** ([`MasterData/1.0.0/`](MasterData/1.0.0/)): Version-specific data (currently a placeholder).

### Exported Objects

The [`ExportedObjects/`](ExportedObjects/) folder contains XML exported definitions of custom entity types and extended configurations:

- **Version 1.0.0** ([`ExportedObjects/1.0.0/`](ExportedObjects/1.0.0/)): Exported entity-type resource loading logic (`CustomGetResourceLoadPortInformation.xml`).

## `cmfpackage.json` manifest

The [`cmfpackage.json`](cmfpackage.json) manifest declares how content is packaged and deployed:

| Source | Target | Content Type | Purpose |
| --- | --- | --- | --- |
| `DEEs/*` | `DeeRules` | `DEE` | All compiled DEE assemblies and source. |
| `DEEs/ProcessRules/EntityTypes/*` | `DeeRules/ProcessRules/EntityTypes/` | `EntityTypes` | Entity type extension rules. |
| `DEEs/ProcessRules/Baseline/Before/*` | `DeeRules/ProcessRules/Baseline/Before/` | `ProcessRulesPre` | Baseline pre-process hooks. |
| `DEEs/ProcessRules/$(version)/Before/*` | `DeeRules/ProcessRules/$(version)/Before` | `ProcessRulesPre` | Version-specific pre-process hooks. |
| `DEEs/ProcessRules/$(version)/After/*` | `DeeRules/ProcessRules/$(version)/After` | `ProcessRulesPost` | Version-specific post-process hooks. |
| `DEEs/ProcessRules/Baseline/After/*` | `DeeRules/ProcessRules/Baseline/After` | `ProcessRulesPost` | Baseline post-process hooks. |
| `ExportedObjects/*` | `ExportedObjects` | `ExportedObjects` | Exported entity type definitions. |
| `MasterData/App/$(version)/*` | `MasterData/$(version)/` | `MasterData` | App-framework-targeted master data. |
| `MasterData/Framework/$(version)/*` | `MasterData/$(version)/` | `MasterData` | Self-framework-targeted master data. |
| `MasterData/$(version)/*` | `MasterData/$(version)/` | `MasterData` | Generic versioned master data. |
| `MasterData/Baseline/DEEs/*` | `MasterData/Baseline/DEEs/` | `MasterData` | Baseline automation rules. |
| `MasterData/Baseline/Model/*` | `MasterData/Baseline/Model/` | `MasterData` | Baseline entity definitions and metadata. |
| `MasterData/Baseline/SecondaryModel/*` | `MasterData/Baseline/SecondaryModel/` | `MasterData` | Baseline secondary model data. |

### `$(version)` substitution

The manifest uses `$(version)` as a placeholder for the package version (`11331.0.0`). During deployment, the framework replaces this token with the actual package version, allowing the same manifest structure to support multiple data versions. For example, `MasterData/$(version)/` becomes `MasterData/11331.0.0/` in the deployed package.

### Related packages

The manifest declares a `preBuild` dependency on [`../IoTCustomAutomationConfiguration.Business/`](../IoTCustomAutomationConfiguration.Business/):

```json
"relatedPackages": [
  {
    "path": "../IoTCustomAutomationConfiguration.Business/",
    "preBuild": true,
    "postBuild": false,
    "prePack": false,
    "postPack": false
  }
]
```

This ensures the Business package DLLs are built before the Data package, because the DEE code references `Cmf.Community.IoTCustomAutomationConfiguration.Actions` assemblies via `UseReference()` directives. The Business package provides these compiled assembly dependencies.

## Build and deployment

The package is built and deployed as part of the larger feature deployment pipeline. The Data package:

1. Compiles DEE code into the MES deployment bundle.
2. Packs master data, exported objects, and process rules into versioned and baseline deployment targets.
3. Is installed as a dependency of the root feature package.

Refer to the feature-level documentation and build scripts for full build, test, and deployment guidance.

## Folder conventions

Refer to the existing convention documentation in [`MasterData/readme.md`](MasterData/readme.md) and [`DEEs/ProcessRules/readme.md`](DEEs/ProcessRules/readme.md) for guidance on naming versioned and versioned-with-label subdirectories.
