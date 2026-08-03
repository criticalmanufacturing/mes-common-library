# IoT SQLite Tasks

A Critical Manufacturing MES Community feature that packages custom SQLite query and data storage tasks for Connect IoT Controller Engine. The tasks let Connect IoT workflows persist data to, and retrieve data from, SQLite databases, with parameterized queries, JSON schema validation, and flexible result mapping.

The feature is deployed as a root package that contains one installable IoT package:

| Package | Package type | Purpose |
| --- | --- | --- |
| `Cmf.Community.IoTSQLLite` | `Root` | Deployment wrapper that installs the feature and its IoT dependency. |
| `Cmf.Community.IoTSQLLite.IoT` | `IoT` | Connect IoT package that installs the SQLite tasks library and its deployment configuration. |
| `@criticalmanufacturing/connect-iot-controller-engine-custom-sqlite-tasks` | npm package | The TypeScript/JavaScript implementation of the SQLite task library. |

## What is shipped

### SQLite tasks library

The implementation is under [`IoTSQLLite.IoT/src/controller-engine-custom-sqlite-tasks/`](IoTSQLLite.IoT/src/controller-engine-custom-sqlite-tasks/). It includes:

- `StoreSQLiteModule` — stores data into a SQLite database.
- `QuerySQLiteModule` — executes SQL queries against a SQLite database.
- `FlexibleQuerySQLiteModule` — provides flexible parameterized SQL query execution.
- `QuerySQLiteNestedArrayModule` — queries SQLite with support for nested array result structures.
- `QuerySQLiteByPathModule` — queries SQLite databases by file path.
- `JSONSchemaValidatorTask` — validates input data against a JSON schema before persistence or processing.
- `SQLiteManager` — utility class that manages SQLite database connections and operations using the `better-sqlite3` library.

See [`IoTSQLLite.IoT/README.md`](IoTSQLLite.IoT/README.md) for full implementation detail, the dependency list, and task-specific configuration.

### Deployment UI

[`IoTSQLLite.IoT/ui.xml`](IoTSQLLite.IoT/ui.xml) injects an IoT deployment wizard step for configuring repository sources, with optional local directory repository and npm server repository settings for package distribution during deployment.

### Other feature contents

- [`Libs/`](Libs/) contains the standard CM package library folders (`Business`, `LBOs`, `EntityTypes`, `Custom`, `External`, `PrivateFix/Business`, `PrivateFix/HTML`, `Tests`). They currently contain only `.gitkeep` placeholders; this feature does not ship additional Business, LBO, Entity Type, Custom, External, PrivateFix, or server-side test implementations.
- [`EnvironmentConfigs/env.json`](EnvironmentConfigs/env.json) is the feature environment configuration consumed by the deployment tooling (database, security portal, messaging, Kubernetes replica/resource settings, and volume paths for a local reference environment).
- [`deploymentdir/`](deploymentdir/) is the deployment framework output directory; it is empty until a deployment/packaging run populates it (e.g. with `CIPackages` and `Delivered` folders, as referenced by `repositories.json`).
- `global.json` pins the .NET SDK (`8.0.301`, `rollForward: latestFeature`) used by the deployment tooling.
- `NuGet.Config` points package restore at the CMF NuGet feed (`https://criticalmanufacturing.io/repository/nuget/index.json`) and `nuget.org`, with source-control integration disabled.
- `repositories.json` configures the CI package repository and delivered-package repository paths under `deploymentdir/` for the deployment framework.
- `.project-config.json` records the CM project scaffolding metadata for this feature: project name `IoTSQLLite`, repository type `Customization`, base layer `MES`, tenant `Community`, and MES/NuGet version `11.3.3`.
- `.devcontainer/devcontainer.json` defines the dev container image (`criticalmanufacturing.io/criticalmanufacturing/devcontainer:11`) and features (CM CLI, Portal SDK, Docker-in-Docker, Chrome testing) used to develop and build this feature.
- `.config/dotnet-tools.json` pins the local .NET tool `dotnet-coverage` (`17.6.0`) used for coverage collection.

## `cmfpackage.json` manifests

CM deployment framework manifests describe installable deployment packages. The two manifests in this feature have different responsibilities:

### Root manifest

[`cmfpackage.json`](cmfpackage.json) has `packageType: "Root"`:

- `packageId` identifies the feature as `Cmf.Community.IoTSQLLite`.
- `version` identifies the feature release (`11331.0.0`).
- `description` is the generic CM Community customization description.
- `isInstallable: true` makes the root package installable.
- `isUniqueInstall: false` allows the package to be installed alongside other instances/versions rather than requiring a single unique install.
- `dependencies` pulls in the IoT child package and optional CM deployment dependencies:
  - `Cmf.Environment` (`11.3.3`, `mandatory: false`) — optional dependency on the MES environment package.
  - `CriticalManufacturing.DeploymentMetadata` (`11.3.3`, `mandatory: false`) — optional dependency on the deployment framework's metadata package.
  - `Cmf.Community.IoTSQLLite.IoT` (`11331.0.0`) — the required child package that contains the actual Connect IoT SQLite tasks and is implicitly mandatory (no `mandatory` field, so it defaults to required).

A root package is therefore the feature-level installation and dependency entry point. It does not itself pack the tasks source; it composes the packages that do.

### IoT manifest

[`IoTSQLLite.IoT/cmfpackage.json`](IoTSQLLite.IoT/cmfpackage.json) has `packageType: "IoT"`:

- `packageId` identifies the installable child as `Cmf.Community.IoTSQLLite.IoT`.
- `version` matches the root package (`11331.0.0`).
- `contentToPack` copies `src/*` into the deployed `node_modules` location, excluding files matched by `.npmignore`.
- `xmlInjection` injects `ui.xml` into the deployment package so the repository configuration step is available during installation.
- `isInstallable: true` makes the IoT package installable on its own.
- `isToForceInstall: true` forces the package installation when the deployment framework processes it.
- `forceRerunAfterDatabaseRestore: true` allows the package installation step to run again after a database restore.

An IoT package is the deployment unit for Connect IoT task/converter libraries. Here it carries the npm package under `src/controller-engine-custom-sqlite-tasks`, its runtime dependencies including `better-sqlite3` for SQLite operations, compiled JavaScript, type declarations, and the deployment UI injection.

## Version and compatibility

The current root and IoT package version is `11331.0.0`. The package version follows the repository's CM release versioning convention and is aligned with MES `11.3.3`:

- Root package: `Cmf.Community.IoTSQLLite@11331.0.0`
- IoT package: `Cmf.Community.IoTSQLLite.IoT@11331.0.0`
- npm tasks: `@criticalmanufacturing/connect-iot-controller-engine-custom-sqlite-tasks@0.0.0`
- CM deployment dependencies (`Cmf.Environment`, `CriticalManufacturing.DeploymentMetadata`): `11.3.3`
- Connect IoT dependencies (`@criticalmanufacturing/connect-iot-controller-engine`) use the `release-1133` npm dist tag.

When creating a release, keep the root package and IoT package versions aligned, and update the optional CM dependency versions and the Connect IoT npm dist tag when targeting a different MES release.

## Build, package, and test

Run commands from [`IoTSQLLite.IoT/`](IoTSQLLite.IoT/):

```bash
npm install
npm run build
npm test
npm run lint
npm run packagePacker
```

The root npm workspace delegates `build`, `test`, `lint`, and packaging commands to the tasks package under `src/controller-engine-custom-sqlite-tasks/`. The package scripts work as follows:

- `build` compiles the tasks library TypeScript sources.
- `test` runs the unit tests under `test/`.
- `test:cover` runs the tests with NYC coverage and writes reports.
- `lint` checks TypeScript sources with ESLint.
- `packagePacker` creates the npm deployment artifact through the CM package bundler.

Test files are excluded from the npm package by `.npmignore`. See [`IoTSQLLite.IoT/README.md`](IoTSQLLite.IoT/README.md) for the full dependency list and further detail.
