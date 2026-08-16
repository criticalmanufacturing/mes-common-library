# IoT Code Utilities IoT Package

`Cmf.Community.IoTCodeUtilities.IoT` is the Connect IoT package for the IoT Code Utilities feature. It deploys the `@criticalmanufacturing/connect-iot-controller-engine-custom-code-utilities-tasks` task library, which makes shared utility implementations available to Connect IoT workflows and custom code tasks.

## Contents

| Content | Location | Purpose |
| --- | --- | --- |
| Task library | [`src/controller-engine-custom-code-utilities-tasks/`](src/controller-engine-custom-code-utilities-tasks/) | TypeScript implementations, templates, and unit tests for the utility-registration tasks and shared libraries. |
| Designer metadata | [`ui.xml`](ui.xml) | Connect IoT deployment wizard configuration and UI injection metadata. |
| Workspace configuration | [`package.json`](package.json) | npm workspace and build, test, lint, and package scripts. |

The task library provides these task families:

- `CustomCodeUtilitiesFrameworkTask`, which registers the framework utility for SmartTable resolution, mapping caching, hash generation, and data-store persistence.
- `CustomCodeUtilitiesObjectTranslatorTask`, which registers conversion helpers such as object-to-string and ASCII, binary, decimal, and hexadecimal transformations.
- `CustomCodeUtilitiesAPITask`, which registers MES API helpers for object lookup, attribute loading, query execution, and automation state changes.

The registered utilities are intended to be consumed by other tasks through the Connect IoT task library. Unit tests are under the task library's [`test/unit/`](src/controller-engine-custom-code-utilities-tasks/test/unit/) directory.

## `cmfpackage.json`

This is an installable, non-unique `IoT` package with package version `11331.0.0`. Its manifest:

| Source | Target | Purpose |
| --- | --- | --- |
| `src/*` | `node_modules` | Packs the task-library workspace packages and their package metadata. |
| `ui.xml` | XML injection | Adds the Connect IoT configuration wizard metadata during deployment. |

The package is force-installed and configured to force a rerun after a database restore. The root `Cmf.Community.IoTCodeUtilities` package installs it as its mandatory dependency.

## Build and deployment

Run the npm workspace commands from this directory. The root [`package.json`](package.json) delegates build, test, lint, and packaging commands to the workspace under [`src/`](src/). Deploy this package through the root feature package so the utility tasks are available to Connect IoT workflow designers and runtimes.