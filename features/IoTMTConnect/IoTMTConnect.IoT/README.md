# IoT MTConnect Driver

A Critical Manufacturing MES Community feature that packages an MTConnect protocol driver for Connect IoT. The driver connects equipment integrations to MTConnect agents and reads device data through the MTConnect protocol.

The feature is deployed as a root package that contains one installable IoT package:

| Package | Package type | Purpose |
| --- | --- | --- |
| `Cmf.Community.IoTMTConnect` | `Root` | Deployment wrapper that installs the feature and its IoT dependency. |
| `Cmf.Community.IoTMTConnect.IoT` | `IoT` | Connect IoT package that installs the MTConnect driver npm package and its deployment wizard UI. |
| `@criticalmanufacturing/connect-iot-driver-mtconnect` | npm package | The TypeScript/JavaScript implementation of the MTConnect device driver. |

## What is shipped

### MTConnect driver

The implementation is under [`src/driver-mtconnect/`](src/driver-mtconnect/). It includes:

- `MTConnectDeviceDriver`, a Connect IoT `DeviceDriverBase` implementation.
- HTTP/HTTPS communication with MTConnect agents through .NET Core interop (`edge-js` library).
- Agent connection settings for address, port, and optional device filtering.
- Optional .NET Core SDK version selection when multiple versions are installed.
- Communication state management for connecting, setup, communicating, and disconnection.
- Support for MTConnect event types: Probe, Current, Sample, and Assets.
- Support for MTConnect commands: Probe, Current, Sample, Assets, and Asset.
- JSONata expressions for extracting property values from MTConnect XML responses.
- Example event and command payloads under `src/driver-mtconnect/templates/`.

The driver is property- and event-oriented. Properties are extracted from MTConnect data using JSONata expressions, and events capture MTConnect data stream snapshots (Probe, Current, Sample, Assets). Commands allow sending requests to the MTConnect agent.

### Deployment UI

[`ui.xml`](ui.xml) injects an IoT deployment wizard step for configuring the local directory repository and npm server repository. It includes optional repository enablement, address, publish tag, registry credentials, and registry email fields.

### Other feature contents

- [`Libs/`](../Libs/) contains the standard CM package library folders. They currently contain only `.gitkeep` placeholders; this feature does not ship additional Business, LBO, Entity Type, Custom, External, PrivateFix, or server-side test implementations.
- [`EnvironmentConfigs/`](../EnvironmentConfigs/) contains the feature environment configuration used by the deployment tooling.
- [`deploymentdir/`](../deploymentdir/) is the deployment framework output directory.
- `global.json`, `NuGet.Config`, and `repositories.json` provide feature-level SDK, package-feed, and repository configuration.

## `cmfpackage.json` manifests

CM deployment framework manifests describe installable deployment packages. The `cmfpackage.json` in this directory has `packageType: "IoT"`:

- `packageId` identifies the installable package as `Cmf.Community.IoTMTConnect.IoT`.
- `version` identifies the package release.
- `contentToPack` copies `src/*` into the deployed `node_modules` location, excluding files matched by `.npmignore`.
- `xmlInjection` injects `ui.xml` into the deployment package so the IoT repository configuration step is available during installation.
- `isToForceInstall: true` forces the IoT package installation when the deployment framework processes it.
- `forceRerunAfterDatabaseRestore: true` allows the package installation step to run again after a database restore.

The IoT package is the deployment unit for Connect IoT task/driver libraries. Here it carries the npm package under `src/driver-mtconnect`, its runtime dependencies, compiled JavaScript, type declarations, and the deployment UI injection.

## Version and compatibility

The current package version is `11331.0.0`. The package version follows the repository's CM release versioning convention and is aligned with MES `11.3.3`:

- Root package: `Cmf.Community.IoTMTConnect@11331.0.0`
- IoT package: `Cmf.Community.IoTMTConnect.IoT@11331.0.0`
- npm driver: `@criticalmanufacturing/connect-iot-driver-mtconnect@11331.0.0`
- CM deployment dependencies: `11.3.3`
- Connect IoT dependencies use the `release-1133` npm dist tag.

When creating a release, keep the root package, IoT package, and npm driver versions aligned. Update the CM dependency versions and the Connect IoT npm dist tag when targeting a different MES release.

## Build, package, and test

Run commands from this directory (`IoTMTConnect.IoT/`):

```bash
npm install
npm run build
npm test
npm run lint
npm run packagePacker
```

The root npm package delegates `build`, `test`, `lint`, coverage, and packaging commands to its workspace packages. The driver package's scripts work as follows:

- `build` compiles the driver and integration-test TypeScript sources and builds the .NET Core components.
- `test` runs integration tests with Mocha under `test/integration/`.
- `test:cover` runs tests with NYC coverage and writes reports under the driver package.
- `lint` checks TypeScript sources with ESLint.
- `lint:fix` automatically fixes ESLint violations.
- `packagePacker` creates the npm deployment artifact through the CM package bundler.

The integration tests cover MTConnect agent communication, including connection setup and data retrieval. Test files are excluded from the npm package by `src/driver-mtconnect/.npmignore`.

For full driver implementation details, including architecture, MTConnect protocol handling, and advanced configurations, see [`src/driver-mtconnect/README.md`](src/driver-mtconnect/README.md).

## Configuration reference

At minimum, configure the driver with an MTConnect agent address and port. The main communication fields are:

| Field | Default | Description |
| --- | --- | --- |
| `address` | `localhost` | MTConnect agent host or IP address. |
| `port` | `5000` | MTConnect agent port. |
| `device` | empty | Optional device filter; when specified, all MTConnect interactions will filter by this device. |
| `netCoreSdkVersion` | empty | Optional .NET Core SDK version to use when multiple versions are installed; leave empty to ignore. |
| `heartbeatInterval` | `30000` | Heartbeat interval in milliseconds. |
| `setupTimeout` | `10000` | Setup timeout in milliseconds. |
| `intervalBeforeReconnect` | `5000` | Delay before reconnect attempts in milliseconds. |
| `connectingTimeout` | `30000` | Maximum connection wait in milliseconds. |

Property, event, and command-specific extended data is defined in the npm package manifest under `criticalManufacturing.automationProtocol.extendedData` and includes support for JSONata expressions, MTConnect event types, and MTConnect command types.
