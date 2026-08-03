# IoT Custom Automation Configuration

A Critical Manufacturing MES Community feature that packages Connect IoT tasks and supporting libraries for automation configuration retrieval and management. The package enables Connect IoT agents to load and persist dynamic automation configuration data from MES systems during device automation workflows.

## Package overview

This directory is the Connect IoT deployment unit for the `Cmf.Community.IoTCustomAutomationConfiguration` feature. It is deployed as an IoT-type package within the feature hierarchy:

| Component | Type | Purpose |
| --- | --- | --- |
| `Cmf.Community.IoTCustomAutomationConfiguration` | Root package | Feature-level deployment wrapper that installs the business logic, data, and IoT packages. |
| `Cmf.Community.IoTCustomAutomationConfiguration.IoT` | IoT package | This package—contains Connect IoT tasks and deployment wizard UI. |
| `@criticalmanufacturing/connect-iot-custom-automationconfiguration-tasks` | npm workspace | The TypeScript/JavaScript implementation of automation configuration tasks. |

## What is shipped

### Automation configuration tasks

The implementation is under [`src/connect-iot-custom-automationconfiguration-tasks/src/`](src/connect-iot-custom-automationconfiguration-tasks/src/). It includes:

- **LoadAutomationConfigurationTask**: A Connect IoT task that retrieves dynamic automation configuration data from MES and persists it locally for use in device automation workflows. It:
  - Loads entity instances by ID from MES with configurable depth (`levelsToLoad`).
  - Invokes a server-side DEE (Dynamic Execution Engine) action named `CustomAutomationRetrieveConfigurationData` to execute business logic.
  - Retrieves configuration data structures including `AutomationConfigurationValues` with name-value pairs.
  - Stores configuration data using a local data handler for persistence.
  - Emits success or error outputs, plus dynamic outputs for each configured automation configuration value.
  - Supports retry logic with configurable retries and sleep-between-retries for resilience.

- **Persistence layer** (`src/persistence/`): Data models and handlers for storing automation configuration data locally:
  - `AutomationConfigurationData`: Top-level container for configuration.
  - `AutomationConfigurationDataProcess`: Process-scoped configuration.
  - `AutomationConfigurationValue`: Individual named configuration values.
  - `MutexQueue` and `PriorityQueue`: Thread-safe queueing utilities.
  - `AutomationConfigurationDataHandler`: Stores and retrieves configuration in memory/storage.

For full implementation details, task inputs, outputs, settings, and usage patterns, see [`src/connect-iot-custom-automationconfiguration-tasks/README.md`](src/connect-iot-custom-automationconfiguration-tasks/README.md).

### Deployment UI

[`ui.xml`](ui.xml) injects an IoT deployment wizard step that allows configuration of npm repository settings during installation:

- **Directory Repository**: Optional local file-based npm package repository with location setting.
- **NPM Server Repository**: Optional npm registry server with address, publish tag, credentials (username, password, email).

This step is presented as part of the MES deployment process, allowing operators to configure package sources for the Connect IoT environment.

### Workspace structure

The root [`package.json`](package.json) defines an npm workspace that:

- Declares `src/*` as workspace packages, making `src/connect-iot-custom-automationconfiguration-tasks` a member workspace.
- Delegates `build`, `lint`, `test`, `packagePacker`, and related commands to all workspace packages via npm workspace aggregation.
- Provides shared devDependencies for TypeScript, ESLint, Mocha, and the CM package bundler.

## `cmfpackage.json` manifest

[`cmfpackage.json`](cmfpackage.json) defines the CM deployment framework manifest for this IoT package:

- `packageId` identifies the package as `Cmf.Community.IoTCustomAutomationConfiguration.IoT`.
- `packageType: "IoT"` marks this as a Connect IoT package for deployment into the IoT runtime environment.
- `version` identifies the package release, aligned with the feature version `11331.0.0`.
- `isInstallable: true` makes this package installable by the CM deployment framework.
- `isToForceInstall: true` forces installation of this package when the deployment processes it, ensuring tasks are always available.
- `forceRerunAfterDatabaseRestore: true` allows package installation to run again after a database restore, re-initializing IoT assets.
- `contentToPack` specifies that the entire `src/*` directory (all workspace packages) be copied into the deployed `node_modules` location, with files matched by `.npmignore` in each package excluded (e.g., test sources, TypeScript source files).
- `xmlInjection` specifies that `ui.xml` be injected into the deployment package so the repository configuration wizard step is available during installation.

## Version and compatibility

The current package version is `11331.0.0`, aligned with MES release `11.3.3`:

- IoT package: `Cmf.Community.IoTCustomAutomationConfiguration.IoT@11331.0.0`
- npm tasks: `@criticalmanufacturing/connect-iot-custom-automationconfiguration-tasks@11331.0.0`
- Connect IoT dependencies use the `release-1133` npm dist tag.
- CM dependency versions: `11.3.3`

When creating a release, keep the root feature version, this IoT package version, and the npm package version aligned. Update dependency versions and npm dist tags when targeting a different MES release.

## Build, package, and test

Run commands from this directory:

```bash
npm install
npm run build
npm test
npm run lint
npm run packagePacker
```

The root npm workspace delegates build and test tasks to workspace packages. The tasks package scripts work as follows:

- `build` compiles the task library TypeScript sources and test sources.
- `test` builds first, then runs unit tests under `test/` with Mocha.
- `test:cover` runs tests with NYC coverage reporting (cobertura, lcov, text-summary formats).
- `lint` checks TypeScript sources with ESLint, following the root ESLint configuration.
- `lint:fix` automatically fixes linting issues.
- `packagePacker` creates the npm deployment artifact through the CM package bundler.

Test sources are excluded from the npm package by `.npmignore`, ensuring only compiled JavaScript, type declarations, and runtime dependencies are packaged.

## Project structure

```
IoTCustomAutomationConfiguration.IoT/
├── package.json                          # Root workspace configuration
├── tsconfig.json                         # TypeScript compiler configuration
├── ui.xml                                # Deployment wizard UI injection
├── cmfpackage.json                       # CM deployment manifest
└── src/
    └── connect-iot-custom-automationconfiguration-tasks/
        ├── package.json                  # npm package manifest
        ├── README.md                     # Detailed task documentation
        ├── src/
        │   ├── index.ts                  # Task exports
        │   ├── tasks/
        │   │   └── loadAutomationConfiguration/
        │   │       ├── loadAutomationConfiguration.task.ts
        │   │       └── loadAutomationConfiguration.task-module.ts
        │   └── persistence/
        │       ├── implementation/       # Data handlers and queues
        │       └── model/                # Data model classes
        ├── test/
        │   └── unit/                     # Unit tests
        └── templates/                    # Example payloads and metadata
```
