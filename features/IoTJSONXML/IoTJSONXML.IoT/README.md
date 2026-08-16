# IoT JSON/XML IoT Package

`Cmf.Community.IoTJSONXML.IoT` is the Connect IoT package for the IoT JSON/XML feature. It deploys the `@criticalmanufacturing/connect-iot-controller-engine-custom-jsonxml-tasks` task library and registers its task definitions in the Connect IoT designer.

## Contents

| Content | Location | Purpose |
| --- | --- | --- |
| Task library | [`src/controller-engine-custom-jsonxml-tasks/`](src/controller-engine-custom-jsonxml-tasks/) | TypeScript implementation and unit tests for JSON-to-XML and XML-to-JSON tasks. |
| Designer metadata | [`ui.xml`](ui.xml) | Connect IoT deployment wizard configuration and UI injection metadata. |
| Workspace configuration | [`package.json`](package.json) | npm workspace and build, test, lint, and package scripts. |

The task library provides:

- `Json2xmlTask`, which accepts a JSON value and optional root key and emits an XML string.
- `Xml2jsonTask`, which accepts an XML string and optional parser options and emits a JSON object or an error.

For task inputs, outputs, development commands, and implementation details, see the [task-library README](src/controller-engine-custom-jsonxml-tasks/README.md).

## `cmfpackage.json`

This is an installable, non-unique `IoT` package with package version `11331.0.0`. Its manifest:

| Source | Target | Purpose |
| --- | --- | --- |
| `src/*` | `node_modules` | Packs the task-library workspace packages and their package metadata. |
| `ui.xml` | XML injection | Adds the Connect IoT configuration wizard metadata during deployment. |

The package is force-installed and configured to force a rerun after a database restore. The root `Cmf.Community.IoTJSONXML` package installs it as its mandatory dependency.

## Build and deployment

Run the npm workspace commands from this directory. The root `package.json` delegates build, test, lint, and packaging commands to the workspace under [`src/`](src/). Deploy this package through the root feature package so the task library and designer metadata are installed together.