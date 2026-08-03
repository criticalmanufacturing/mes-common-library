# IoTAMQPDriver.IoT Package

This directory is the **npm workspace root** and **Connect IoT deployment package** (`Cmf.Community.IoTAMQPDriver.IoT`) for the AMQP driver. It orchestrates the build, test, and deployment of the AMQP device driver implementation and its deployment wizard UI.

## Structure

| Item | Purpose |
| --- | --- |
| [`package.json`](package.json) | npm workspace configuration; delegates build/test/lint scripts to `src/driver-amqp/`. |
| [`cmfpackage.json`](cmfpackage.json) | CM deployment manifest; declares this as an `IoT` package with content packing and UI injection rules. |
| [`ui.xml`](ui.xml) | Deployment wizard configuration; injects IoT repository setup step for local directory and npm server repositories. |
| [`tsconfig.json`](tsconfig.json) | TypeScript configuration (ES6 target, CommonJS modules, declaration files enabled). |
| [`src/driver-amqp/`](src/driver-amqp/) | The actual driver implementation; see its [README](src/driver-amqp/README.md) for details. |

## npm Workspace

This is an npm workspace root. The root [`package.json`](package.json) defines:

- `scripts`: delegation commands that run across all workspace packages with the `-ws` flag:
  - `build`: compiles TypeScript sources in all workspace packages.
  - `test`: runs integration tests in all workspace packages.
  - `lint`: checks code style in all workspace packages.
  - `test:cover`: generates test coverage reports across all packages.
  - `packagePacker`: bundles each workspace package for deployment.

- `workspaces`: includes `src/*`, which currently contains the single `driver-amqp` workspace.

- `devDependencies`: shared build and test tooling (TypeScript, ESLint, Mocha, Testcontainers, @criticalmanufacturing/node-package-bundler).

Running `npm` commands from this directory orchestrates the entire driver package build pipeline.

## Deployment manifest

[`cmfpackage.json`](cmfpackage.json) declares this as a CM Connect IoT package:

| Field | Value | Meaning |
| --- | --- | --- |
| `packageId` | `Cmf.Community.IoTAMQPDriver.IoT` | Unique identifier for this Connect IoT package. |
| `version` | `11331.0.0` | Package version; must be kept in sync with the feature-level and npm package versions. |
| `packageType` | `IoT` | Indicates this is a Connect IoT driver/task library package (not Root or other types). |
| `isInstallable` | `true` | This package can be installed via the CM deployment framework. |
| `isToForceInstall` | `true` | Force installation even if dependencies are already present. |
| `forceRerunAfterDatabaseRestore` | `true` | Re-run installation after database restore operations. |
| `contentToPack` | `src/*` → `node_modules` | Copy all workspace sources (the driver implementation) into the deployed `node_modules` directory during packaging, excluding files matched by `.npmignore`. |
| `xmlInjection` | `ui.xml` | Inject this UI definition into the deployment package so the repository configuration wizard step is available. |

## Deployment UI

[`ui.xml`](ui.xml) defines a wizard step injected during installation, providing fields for:

- **Directory Repository**: optional local file-system package repository path.
- **NPM Server Repository**: optional npm registry address, publish tag, and authentication credentials (username, password, email).

These settings allow deployment-time configuration of where npm packages are sourced or published.

## Build and deployment workflow

1. **Install dependencies** (from this directory):
   ```bash
   npm install
   ```

2. **Build** the driver implementation:
   ```bash
   npm run build
   ```

3. **Run tests** to verify the driver:
   ```bash
   npm test
   ```

4. **Run linting** to check code quality:
   ```bash
   npm run lint
   ```

5. **Create the deployment package**:
   ```bash
   npm run packagePacker
   ```
   This generates the deployment artifact in the CM package format, copying `src/driver-amqp/` contents into `node_modules`, excluding test files and sources (see `src/driver-amqp/.npmignore`).

See [`../README.md`](../README.md) for feature-level build and version information.

## Registry configuration

The npm registry is configured in [`.npmrc`](.npmrc):
```
registry=https://criticalmanufacturing.io/repository/npm/
```

This ensures npm packages are fetched from the Critical Manufacturing package feed.

## Driver implementation

The actual AMQP device driver implementation lives in [`src/driver-amqp/`](src/driver-amqp/). See its [README](src/driver-amqp/README.md) for:

- TypeScript source code for the driver (currently a placeholder).
- npm package configuration and runtime dependencies.
- Test suite and test results.
- Command and event template examples.
- Extended data configuration for device parameters, commands, and properties.

This package root handles the deployment wrapper, workspace orchestration, and UI injection; the implementation details are contained within `src/driver-amqp/`.
