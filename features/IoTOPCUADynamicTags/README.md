# IoT OPC UA Dynamic Tags

`Cmf.Community.IoTOPCUADynamicTags` is a Critical Manufacturing MES Community feature that provides a ready-to-deploy OPC UA dynamic-tags configuration. It seeds the MES and Connect IoT definitions needed to create dynamic-tag controllers, register custom events, resolve execution and data-collection contexts, and manage the associated controller configuration.

The feature is delivered as a root package that bundles the following installable packages:

| Package | Purpose |
| --- | --- |
| [`IoTOPCUADynamicTags.Data`](IoTOPCUADynamicTags.Data/) | Compiled DEE logic and master data for the dynamic-tags controller, workflows, manager, and context resolution. |
| [`IoTOPCUADynamicTags.Database.Pre`](IoTOPCUADynamicTags.Database.Pre/Pre/) | Pre-deployment Online and ODS database upgrade scripts. |

The root package also depends on `Cmf.Community.IoTMESInteroperability`, which supplies the MES interoperability capabilities used by this feature. `Cmf.Environment` and `CriticalManufacturing.DeploymentMetadata` are optional deployment dependencies.

## Included configuration

The feature's master data defines:

- `OPCUA_Protocol` automation protocol and the `DynamicTags_DriverDefinition`.
- `DynamicTags_Controller` and its `Handler` driver definition.
- `Setup`, `RegisterCustomEvents`, `CounterBasedEvent`, and related dynamic-tags workflows.
- `DynamicTags_Manager`, deployed in manual mode.
- Custom OPC UA recipe, execution-context, and data-collection context resolution definitions.

The package version is `1.0.0`; the feature content targets MES version `11.3.3` and uses package content version `11331.0.0`.

## Deployment

Install the root package through the Critical Manufacturing package deployment process. The root package installs its Data and Database.Pre dependencies in the appropriate order. The Connect IoT controller configuration references the standard Connect IoT controller-engine task packages and must be deployed to a compatible Connect IoT environment.

See the package-specific READMEs for the content and manifest details of each child package.