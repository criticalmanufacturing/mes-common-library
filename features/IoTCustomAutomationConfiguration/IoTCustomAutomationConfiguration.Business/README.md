# IoT Custom Automation Configuration Business Package

A Critical Manufacturing MES Community feature that provides server-side business logic for managing custom automation configuration metadata and entities in the MES system. This package enables Connect IoT automation controllers and drivers to store and retrieve their configuration parameters through standardized MES entity relationships.

The Business package is deployed as an installable package containing three .NET assemblies:

| Assembly | Purpose |
| --- | --- |
| `Cmf.Community.IoTCustomAutomationConfiguration.Common.dll` | Common utilities, constants, and data structures for automation configuration. |
| `Cmf.Community.IoTCustomAutomationConfiguration.Orchestration.dll` | Business orchestration layer that manages configuration creation, retrieval, and updates. |
| `Cmf.Community.IoTCustomAutomationConfiguration.Services.dll` | ASP.NET Core controller and API entry point for HTTP service requests. |

## What is shipped

### Common layer

The [`Common`](Common/) project contains shared utilities and abstractions:

- `CustomAutomationConfigurationConstants.cs` defines constant names for MES entities (CustomAutomationConfiguration, CustomAutomationConfigurationValue, CustomAutomationConfigurationStructure, CustomAutomationConfigurationEntity), SmartTables (CustomMaterialMovementContext, CustomSorterJobDefinitionContext, CustomAutomationJobIdLogic, CustomErrorHandling, CustomAlarmManagement), lookup tables, attributes, and configuration keys.
- `CustomAutomationConfigurationUtilities.cs` provides helper methods for type conversion (decimal, boolean, enum), data collection context resolution, SmartTable queries, entity attribute definitions, and localized exception messages.
- `DEEActionUtilities.cs` contains utilities for validating action groups and extracting action parameters from input dictionaries.
- `DataStructures/` contains serializable data transfer objects:
  - `AutomationConfigurationData.cs` represents a configuration with name, related entity, driver friendly name, parameter values, nested configurations, and extended properties.
  - `AutomationConfigurationValue.cs` represents a single configuration parameter with name, value, and extended properties.
  - `ExtendedCustomProperty.cs` provides extensibility for custom behavior.
- `Extensions/` folder contains LINQ, dictionary, entity, material, and table extension methods for common operations on MES business objects.
- `PlaceholderEntityObject/` folder contains interfaces that define the contract for custom automation configuration entity types (ICustomAutomationConfiguration, ICustomAutomationConfigurationValue, ICustomAutomationConfigurationStructure, ICustomAutomationConfigurationEntity) and their collections. These are placeholder definitions; the actual implementations are resolved at runtime from tenant-specific assemblies.
- `CommonStartupModule.cs` registers dependency injection services for the Common layer.

### Orchestration layer

The [`Orchestration`](Orchestration/) project implements business logic for configuration management:

- `CustomAutomationConfigurationOrchestration.cs` is the main orchestration class that:
  - Dynamically registers custom entity types at initialization by loading tenant-specific assemblies.
  - Discovers and registers all Connect IoT-enabled entity types (except those in an exclusion configuration).
  - Implements five core operations defined by the `ICustomAutomationConfigurationOrchestration` interface.
  - Orchestrates with Material, Resource, Entity Type, Configuration, Dispatch, and Generic Service orchestration layers.
  - Manages AutomationProtocol, AutomationController, and AutomationDriver entities.
  
- `ICustomAutomationConfigurationOrchestration.cs` defines the public API:
  - `CustomAutomationCreateConfigurationMetadata()` creates configuration metadata structures.
  - `CustomAutomationCreateConfigurationEntities()` creates configuration entity relationships and values.
  - `CustomAutomationRetrieveConfiguration()` retrieves existing automation configurations.
  - `CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabled()` establishes relationships between configurations and ConnectIoT-enabled entities.
  - `CustomAutomationUpdateConfigurationEntities()` updates configuration entity values.

- `InputObjects/` folder contains data contracts for orchestration method inputs, each inheriting from `BaseInput`:
  - `CustomAutomationCreateConfigurationMetadataInput.cs`
  - `CustomAutomationCreateConfigurationEntitiesInput.cs`
  - `CustomAutomationRetrieveConfigurationInput.cs`
  - `CustomAutomationCreateRelatedEntityRelationOnConnectIoTEnabledInput.cs`
  - `CustomAutomationUpdateConfigurationEntitiesInput.cs`

- `OutputObjects/` folder contains corresponding output data contracts, each inheriting from `BaseOutput`.

- `OrchestrationStartupModule.cs` registers orchestration services for dependency injection.

### Services layer

The [`Services`](Services/) project provides the HTTP API:

- `COMMONController.cs` is an ASP.NET Core `ControllerBase` that routes API requests to orchestration methods. It is configured with the `[Route("api/[controller]/[action]")]` attribute for RESTful API endpoints.

### Build output

The [`Release`](Release/) folder contains the compiled assemblies and their dependency manifests:

- Three .dll files (Common, Orchestration, Services) compiled from the corresponding projects.
- Three .deps.json files that describe runtime dependencies for each assembly.
- Three .pdb files for debugging symbols.
- A shared utility assembly `Cmf.Common.CustomActionUtilities.dll` copied from NuGet dependencies.

## Project architecture

The three projects follow a layered architecture:

- **Common → Orchestration → Services**: Services depends on Orchestration, which depends on Common. This ensures clear separation of concerns.
- **Dependency Injection**: Each layer defines a startup module (CommonStartupModule, OrchestrationStartupModule) that registers services in the DI container.
- **Entity Factory Pattern**: The orchestration layer dynamically discovers and instantiates custom entity types at runtime using reflection, enabling the package to work with any tenant-specific entity definitions.
- **Configuration-Driven Behavior**: MES configuration keys (e.g., `CustomEntityToExcludeFromConfigurationRelationConfiguration`) control which entity types are registered, allowing customization without code changes.

## `cmfpackage.json` manifest

[`cmfpackage.json`](cmfpackage.json) defines the deployment package:

- `packageId` identifies this as `Cmf.Community.IoTCustomAutomationConfiguration.Business`.
- `version` is `11331.0.0`, aligned with MES `11.3.3` and matching the root feature package version.
- `packageType: "Business"` indicates this is a server-tier business logic package installed into the MES server runtime.
- `isInstallable: true` allows the package to be deployed via the CM deployment framework.
- `isUniqueInstall: false` permits multiple installations if needed.
- `contentToPack` copies all `.dll` files from the `Release/` folder into the MES business tier deployment.

A Business package is therefore a runtime deployment unit for server-side .NET assemblies. The MES deployment framework extracts the DLLs and registers them with the ASP.NET Core service host, making the orchestration methods and HTTP endpoints available to the MES application.

## Version and compatibility

The current package version is `11331.0.0`. The version follows the repository's CM release versioning convention:

- Business package: `Cmf.Community.IoTCustomAutomationConfiguration.Business@11331.0.0`
- MES target: `11.3.3`
- Framework: `.NET 8.0`
- Common dependencies:
  - `Cmf.Foundation.BusinessObjects@11.3.3`
  - `Cmf.Navigo.BusinessObjects@11.3.3`
  - `cmf.common.customactionutilities@11.3.0.1263488`

Maintain version alignment across the feature when releasing updates.

## Build and package

Build from the [`Business.sln`](Business.sln) solution file:

```bash
dotnet build Business.sln --configuration Release
```

The build output is automatically placed in the `Release/` folder by the project's `csproj` configuration. The `cmfpackage.json` manifest then packages all `.dll` files for deployment:

```bash
npm run packagePacker
```

Alternatively, the CM deployment framework runs `packagePacker` automatically during feature installation.

## References

- **Parent feature**: [`..`](..) - IoTCustomAutomationConfiguration feature containing the Data, DEEs, and Business sub-packages.
- **Data layer**: [`../IoTCustomAutomationConfiguration.Data`](../IoTCustomAutomationConfiguration.Data) - Custom entity definitions and DEE actions.
- **Orchestration patterns**: Follows the MES Foundation and Navigo orchestration abstraction layers for consistency with standard MES packages.
