# IoT JSON/XML

`Cmf.Community.IoTJSONXML` is a Critical Manufacturing MES Community feature that adds JSON/XML conversion tasks to Connect IoT Controller Engine workflows. It lets IoT workflows convert JSON values to XML strings and parse XML strings into JSON objects.

The feature is delivered as a root package with one installable child package:

| Package | Purpose |
| --- | --- |
| [`IoTJSONXML.IoT`](IoTJSONXML.IoT/) | Connect IoT package containing the custom JSON/XML task library and designer metadata. |

The root package uses version `11331.0.0` and targets MES `11.3.3`. It has optional dependencies on `Cmf.Environment` and `CriticalManufacturing.DeploymentMetadata`, both at version `11.3.3`.

## Capabilities

- Convert a JSON value to an XML string with configurable XML builder options.
- Convert an XML string to a JSON object with configurable parser options.
- Use both operations as custom tasks in the Connect IoT workflow designer.

Install the root package through the Critical Manufacturing package deployment process. See the child package README and the [task-library README](IoTJSONXML.IoT/src/controller-engine-custom-jsonxml-tasks/README.md) for package and task-level development details.