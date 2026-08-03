# IoT SQLite Tasks

A Critical Manufacturing MES Community feature that packages custom SQLite query and data storage tasks for Connect IoT Controller Engine. These tasks enable data persistence and retrieval from SQLite databases within IoT workflows.

The feature is deployed as an IoT package that contains the Connect IoT SQLite tasks library:

| Package | Package type | Purpose |
| --- | --- | --- |
| `Cmf.Community.IoTSQLLite.IoT` | `IoT` | Connect IoT package that installs the SQLite tasks library and its deployment configuration. |
| `@criticalmanufacturing/connect-iot-controller-engine-custom-sqlite-tasks` | npm package | The TypeScript/JavaScript implementation of the SQLite task library. |

## What is shipped

### SQLite tasks library

The implementation is under [`src/controller-engine-custom-sqlite-tasks/`](src/controller-engine-custom-sqlite-tasks/). It includes:

- `StoreSQLiteModule` — stores data into a SQLite database.
- `QuerySQLiteModule` — executes SQL queries against a SQLite database.
- `FlexibleQuerySQLiteModule` — provides flexible parameterized SQL query execution.
- `QuerySQLiteNestedArrayModule` — queries SQLite with support for nested array result structures.
- `QuerySQLiteByPathModule` — queries SQLite databases by file path.
- `JSONSchemaValidatorTask` — validates input data against a JSON schema before persistence or processing.
- `SQLiteManager` — utility class that manages SQLite database connections and operations using the `better-sqlite3` library.

The tasks support parameterized queries, data validation through JSON schema, and flexible result mapping for IoT integration scenarios. See [`src/controller-engine-custom-sqlite-tasks/README.md`](src/controller-engine-custom-sqlite-tasks/README.md) for implementation details and task-specific configuration.

### Deployment UI

[`ui.xml`](ui.xml) injects an IoT deployment wizard step for configuring repository sources. It includes optional local directory repository and npm server repository settings for package distribution during deployment.

## `cmfpackage.json` manifest

The IoT manifest [`cmfpackage.json`](cmfpackage.json) has `packageType: "IoT"` and:

- `packageId` identifies the installable package as `Cmf.Community.IoTSQLLite.IoT`.
- `version` identifies the package release (`11331.0.0`).
- `contentToPack` copies `src/*` into the deployed `node_modules` location, excluding files matched by `.npmignore`.
- `xmlInjection` injects `ui.xml` into the deployment package so the repository configuration step is available during installation.
- `isInstallable: true` makes the package installable.
- `isToForceInstall: true` forces the package installation when the deployment framework processes it.
- `forceRerunAfterDatabaseRestore: true` allows the package installation step to run again after a database restore.

An IoT package is the deployment unit for Connect IoT task/converter libraries. Here it carries the npm package under `src/controller-engine-custom-sqlite-tasks`, its runtime dependencies including `better-sqlite3` for SQLite operations, compiled JavaScript, type declarations, and the deployment UI injection.

## Version and compatibility

The current package version is `11331.0.0`. The package version follows the repository's CM release versioning convention and is aligned with MES `11.3.3`:

- IoT package: `Cmf.Community.IoTSQLLite.IoT@11331.0.0`
- npm tasks: `@criticalmanufacturing/connect-iot-controller-engine-custom-sqlite-tasks@0.0.0`
- CM deployment dependencies: `11.3.3`
- Connect IoT dependencies use the `release-1133` npm dist tag.

## Build, package, and test

Run commands from this directory:

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

Test files are excluded from the npm package by `.npmignore`.

## Dependencies

Key runtime dependencies for SQLite operations:

| Dependency | Purpose |
| --- | --- |
| `better-sqlite3@^11.10.0` | Synchronous SQLite database access. |
| `@criticalmanufacturing/connect-iot-controller-engine@release-1133` | Connect IoT task execution framework. |
| `ajv@^8.17.1` | JSON schema validation for the schema validator task. |
| `inversify@6.0.2` | Dependency injection container. |
| `moment@2.29.4` | Date/time utilities. |
