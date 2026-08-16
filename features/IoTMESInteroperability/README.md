# IoT MES Interoperability

`Cmf.Community.IoTMESInteroperability` is a Critical Manufacturing MES Community feature that provides the business and data extensions used to integrate Connect IoT workflows with MES material, recipe, resource, metadata, and document operations.

The feature is delivered as a root package that bundles two installable packages:

| Package | Purpose |
| --- | --- |
| [`IoTMESInteroperability.Business`](IoTMESInteroperability.Business/) | Compiled Business assemblies containing the public orchestration and service implementations used by MES and Connect IoT integrations. |
| [`IoTMESInteroperability.Data`](IoTMESInteroperability.Data/) | DEE actions and versioned master data that expose interoperability operations to Connect IoT and configure their MES definitions. |

The root package also has optional dependencies on `Cmf.Environment` and `CriticalManufacturing.DeploymentMetadata`, both at version `11.3.3`. The feature packages use version `11331.0.0` and target MES `11.3.3`.

## Capabilities

- Execute material operations from Connect IoT and load the related MES entities.
- Retrieve material recipes for IoT workflows.
- Create and terminate materials, detach consumables, and adjust resource state.
- Create external documents and notify Connect IoT when MES metadata changes.
- Resolve metadata, resource state, and material-tracking information for IoT processes.

Install the root package through the Critical Manufacturing package deployment process. The root package installs the Business and Data packages together; see the child READMEs for their build, content, and manifest details.