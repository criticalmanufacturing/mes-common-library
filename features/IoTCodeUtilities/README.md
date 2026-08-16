# IoT Code Utilities

`Cmf.Community.IoTCodeUtilities` is a Critical Manufacturing MES Community feature that provides reusable utility tasks and shared code for Connect IoT workflows. The utilities help custom code interact with MES objects, resolve configuration mappings, translate values, and perform common encoding and conversion operations.

The feature is delivered as a root package with one installable child package:

| Package | Purpose |
| --- | --- |
| [`IoTCodeUtilities.IoT`](IoTCodeUtilities.IoT/) | Connect IoT package containing the custom code utility task library and designer metadata. |

The root package uses version `11331.0.0` and targets MES `11.3.3`. It has optional dependencies on `Cmf.Environment` and `CriticalManufacturing.DeploymentMetadata`, both at version `11.3.3`.

## Capabilities

- Register a reusable framework utility for SmartTable resolution, mapping caches, hash creation, and persisted data-store mappings.
- Register object translation utilities for string conversion, ASCII/binary/decimal/hexadecimal conversion, and related value operations.
- Register MES API utilities for object lookup, attribute loading, query execution, and automation instance state updates.

Install the root package through the Critical Manufacturing package deployment process. See the child package README for task-library, manifest, and build details.