# IoTDataPlatformHelperSuite.IoT

A Critical Manufacturing MES Community Connect IoT package (`packageType: "IoT"`) that ships a suite of Controller Engine tasks and converters for interacting with the Data Platform, ML Platform, and related services. This directory is the npm workspace root and deployment unit for the IoT sub-package.

## Package composition

| Component | Location | Purpose |
| --- | --- | --- |
| npm workspace root | `.` | Coordinates builds, tests, and packaging across workspace members using npm scripts. |
| Task Library package | `src/custom-dataplatform-tasks/` | TypeScript/Angular library (`@criticalmanufacturing/connect-iot-controller-engine-custom-dataplatform-tasks`) implementing the tasks and converters. |
| Deployment UI | `ui.xml` | Deployment wizard step for configuring local directory and npm server repositories. |

## What is shipped

This package deploys two runtime categories:

### Tasks

Located under `src/custom-dataplatform-tasks/src/tasks/`. Each task integrates with Data Platform, ML Platform, or OData endpoints to perform specialized work in Connect IoT flows:

- **`EquipmentEventWithDataplatformTask`** — Subscribes to equipment/driver events, resolves the ISA-95 hierarchy, and posts telemetry to the Data Platform (immediate or batched).
- **`PostNumericTelemetryTask`** — Posts a single numeric telemetry value with ISA-95 hierarchy tags.
- **`PostMultipleNumericTelemetryTask`** — Posts multiple numeric telemetry values in one call.
- **`QueryODataTask`** — Builds and executes OData queries against the Data Manager OData endpoint with dynamic input substitution.
- **`MlPredictionTask`** — Invokes the ML Platform Agent for real-time predictions via REST, with dynamic inputs/outputs shaped by the selected MLModel.

For full design details, usage patterns, and configuration, see [`../README.md`](../README.md).

### Converters

Located under `src/custom-dataplatform-tasks/src/converters/`. Converters are value-transform plugins usable in designer data bindings:

- **`NowConverter`** (`"now"`) — Current date/time in selectable formats (epoch ms/s, ISO, UTC, local, date, time, timezone).
- **`ToTimeConverter`** (`"toTime"`) — Converts duration strings (`"2s"`, `"1.5h"`, via the `ms` library) to absolute future epoch-ms timestamps.
- **`UnixToISOStringConverter`** (`"unixToISOString"`) — Unix ms timestamp to ISO 8601 string.

## `cmfpackage.json` manifest

The deployment manifest [`cmfpackage.json`](cmfpackage.json) has `packageType: "IoT"`:

- `packageId: "Cmf.Community.IoTDataPlatformHelperSuite.IoT"` — identifies this installable package.
- `version: "11331.0.0"` — aligned with feature release and MES 11.3.3.
- `contentToPack` copies `src/*` (the built Task Library package and all generated artifacts) into the deployed `node_modules` location, excluding files matched by `.npmignore` (TypeScript source, tests, build configuration).
- `xmlInjection: ["ui.xml"]` — injects the deployment UI step into the installation wizard.
- `isInstallable: true` / `isUniqueInstall: false` — marks this as an installable IoT package that may coexist with other packages of the same type.

## Deployment UI

[`ui.xml`](ui.xml) defines an IoT deployment wizard step (`Deployment.ConnectIoT`) with two groups:

1. **Directory Repository** — Optional fields for enabling and specifying a local directory repository location.
2. **NPM Server Repository** — Optional fields for configuring an npm registry (address, publish tag, authentication, email).

Both groups are optional; configuration values are captured during deployment and used by the installation framework.

## Build, package, and test

Run commands from this directory (the workspace root):

```bash
npm install
npm run build
npm test
npm run lint
npm run lint:fix
npm run test:cover
npm run packagePacker
npm run watch
```

### Scripts overview

Workspace scripts delegate to member packages via `npm run <script> -ws --if-present`:

- **`build`** — Compiles all TypeScript sources (tasks, converters, utilities, tests) via `tsc`.
- **`test`** — Runs unit tests under `src/custom-dataplatform-tasks/test/unit/` via Mocha (timeout 5s).
- **`test:cover`** — Runs tests with NYC coverage reporting (lcov, cobertura, text summary; timeout 10s).
- **`lint`** — Checks all TypeScript files with ESLint.
- **`lint:fix`** — Runs ESLint with `--fix` to auto-correct style violations.
- **`packagePacker`** — Creates the npm deployment artifact via the CM package bundler (`@criticalmanufacturing/node-package-bundler`). Output is in `src/custom-dataplatform-tasks/`.
- **`watch`** — Watches for TypeScript changes and reruns tests on each change.
- **`generateTaskLibrary`** — Scaffolds a new task library package under `src/` using `cmf new iot taskLibrary`.

### TypeScript configuration

[`tsconfig.json`](tsconfig.json) targets ES6, CommonJS, includes DOM and ES2017 libraries, and enables experimental decorators (used by the Task Library framework). The config excludes `node_modules` and `templates/` directories from compilation.

### Test structure

Unit tests are located under `src/custom-dataplatform-tasks/test/unit/`. The build step (`npm run build`) also compiles tests via `tsc -p test/unit`. Coverage reports are written to the package directory.

## Dependencies

### Runtime dependencies

Located in `src/custom-dataplatform-tasks/package.json`:

- `@criticalmanufacturing/connect-iot-common` — Connect IoT platform common utilities (tag: `release-1133`).
- `@criticalmanufacturing/connect-iot-controller-engine` — Controller Engine task/converter framework (tag: `release-1133`).
- `cmf-lbos` — CM LBO client library (tag: `release-1133`).
- `odata-query` — OData query builder library (`^8.0.7`).
- `inversify` — Dependency injection container (`6.0.2`).
- `moment`, `reflect-metadata`, `zone.js` — Utility and decorator-support libraries.

### Development dependencies

- `cmf-core-connect-iot` — Connect IoT core testing and build tools (tag: `release-1133`).
- `typescript` (`5.2.2`), ESLint (`^8.53.0`), Mocha (`10.2.0`), NYC (`15.1.0`) — Compilation, linting, and testing tools.
- `@types/chai`, `@types/mocha`, `@types/node` — TypeScript type stubs.

## Version and compatibility

The package version follows the repository's CM release versioning:

- Package: `Cmf.Community.IoTDataPlatformHelperSuite.IoT@11331.0.0`
- npm Task Library: `@criticalmanufacturing/connect-iot-controller-engine-custom-dataplatform-tasks@11331.0.0`
- MES alignment: `11.3.3`
- Connect IoT dependencies: `release-1133` dist tag

When updating this package for a new MES release, align the version numbers across `package.json` (root and `src/custom-dataplatform-tasks/`), `cmfpackage.json`, and Connect IoT npm dist tags.

## Related files and directories

- **`../cmfpackage.json`** — Root feature manifest that bundles this IoT package with the Python task package.
- **`../README.md`** — Feature-level README with full design details, usage patterns, and task/converter specifications.
- **`../.devcontainer/`, `../global.json`, `../NuGet.Config`** — Feature-level SDK and tooling configuration shared across the feature.
- **`src/custom-dataplatform-tasks/src/utilities/`** — Shared utility functions for ISA-95 hierarchy lookup and telemetry posting.
- **`src/custom-dataplatform-tasks/templates/`** — Task/converter JSON definitions and designer editor scripts (e.g., `onInit.ts`, `onMlModelChange.ts` for ML prediction task UI).
