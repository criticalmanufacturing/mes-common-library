# IoT AMQP Driver

A Critical Manufacturing MES Community feature that packages an AMQP protocol driver for Connect IoT. The driver connects equipment integrations to an AMQP broker and publishes Connect IoT commands as JSON messages.

The feature is deployed as a root package that contains one installable IoT package:

| Package | Package type | Purpose |
| --- | --- | --- |
| `Cmf.Community.IoTAMQPDriver` | `Root` | Deployment wrapper that installs the feature and its IoT dependency. |
| `Cmf.Community.IoTAMQPDriver.IoT` | `IoT` | Connect IoT package that installs the AMQP driver npm package and its deployment wizard UI. |
| `@criticalmanufacturing/connect-iot-driver-amqp` | npm package | The TypeScript/JavaScript implementation of the AMQP device driver. |

## What is shipped

### AMQP driver

The implementation is under [`IoTAMQPDriver.IoT/src/driver-amqp/`](IoTAMQPDriver.IoT/src/driver-amqp/). It includes:

- `AMQPDeviceDriver`, a Connect IoT `DeviceDriverBase` implementation.
- AMQP 1.0 broker communication through the `rhea` library.
- Broker connection settings for address, port, heartbeat, connection/setup timeouts, and reconnect delay.
- Optional TLS support using a PEM certificate and private key. Certificate files are managed under the configured certificates directory.
- Username/password authentication, with anonymous access used when no username is configured.
- Command publication to a queue, exchange, or topic-style address.
- JSON message bodies, message subjects, generated message IDs, and creation timestamps.
- Command options for durability, expiration policy, and routing capabilities such as queue, topic, fanout, and headers.
- Connect IoT communication-state transitions and logging for connecting, setup, communicating, disconnecting, and broker failures.
- Example command and event payloads under `IoTAMQPDriver.IoT/src/driver-amqp/templates/`.

The driver is command-oriented. `getValues` and `setValues` are intentionally unsupported and fail if called directly; commands are sent through the AMQP connection with `execute`.

### Deployment UI

[`IoTAMQPDriver.IoT/ui.xml`](IoTAMQPDriver.IoT/ui.xml) injects an IoT deployment wizard step for configuring the local directory repository and npm server repository. It includes optional repository enablement, address, publish tag, registry credentials, and registry email fields.

### Other feature contents

- [`Libs/`](Libs/) contains the standard CM package library folders. They currently contain only `.gitkeep` placeholders; this feature does not ship additional Business, LBO, Entity Type, Custom, External, PrivateFix, or server-side test implementations.
- [`EnvironmentConfigs/`](EnvironmentConfigs/) contains the feature environment configuration used by the deployment tooling.
- [`deploymentdir/`](deploymentdir/) is the deployment framework output directory.
- `global.json`, `NuGet.Config`, and `repositories.json` provide feature-level SDK, package-feed, and repository configuration.

## `cmfpackage.json` manifests

CM deployment framework manifests describe installable deployment packages. The two manifests in this feature have different responsibilities:

### Root manifest

[`cmfpackage.json`](cmfpackage.json) has `packageType: "Root"`:

- `packageId` identifies the feature as `Cmf.Community.IoTAMQPDriver`.
- `version` identifies the feature release.
- `isInstallable: true` makes the root package installable.
- `dependencies` pulls in the IoT child package and optional CM deployment dependencies.
- `Cmf.Environment` and `CriticalManufacturing.DeploymentMetadata` are optional dependencies for the MES environment/deployment framework.
- `Cmf.Community.IoTAMQPDriver.IoT` is the package that contains the actual Connect IoT driver and is required by the root package.

A root package is therefore the feature-level installation and dependency entry point. It does not itself pack the driver source; it composes the packages that do.

### IoT manifest

[`IoTAMQPDriver.IoT/cmfpackage.json`](IoTAMQPDriver.IoT/cmfpackage.json) has `packageType: "IoT"`:

- `packageId` identifies the installable child as `Cmf.Community.IoTAMQPDriver.IoT`.
- `contentToPack` copies `src/*` into the deployed `node_modules` location, excluding files matched by `.npmignore`.
- `xmlInjection` injects `ui.xml` into the deployment package so the IoT repository configuration step is available during installation.
- `isToForceInstall: true` forces the IoT package installation when the deployment framework processes it.
- `forceRerunAfterDatabaseRestore: true` allows the package installation step to run again after a database restore.

An IoT package is the deployment unit for Connect IoT task/driver libraries. Here it carries the npm package under `src/driver-amqp`, its runtime dependencies, compiled JavaScript, type declarations, and the deployment UI injection.

## Version and compatibility

The current feature and IoT package version is `11331.0.0`. The package version follows the repository's CM release versioning convention and is aligned with MES `11.3.3`:

- Root package: `Cmf.Community.IoTAMQPDriver@11331.0.0`
- IoT package: `Cmf.Community.IoTAMQPDriver.IoT@11331.0.0`
- npm driver: `@criticalmanufacturing/connect-iot-driver-amqp@11331.0.0`
- CM deployment dependencies: `11.3.3`
- Connect IoT dependencies use the `release-1133` npm dist tag.

When creating a release, keep the root package, IoT package, and npm driver versions aligned. Update the CM dependency versions and the Connect IoT npm dist tag when targeting a different MES release.

## Build, package, and test

Run commands from [`IoTAMQPDriver.IoT/`](IoTAMQPDriver.IoT/):

```bash
npm install
npm run build
npm test
npm run lint
npm run packagePacker
```

The root npm package delegates `build`, `test`, `lint`, coverage, and packaging commands to its workspace packages. The driver package's scripts work as follows:

- `build` compiles the driver and integration-test TypeScript sources.
- `test` builds first, then runs the integration tests under `test/integration/`.
- `test:cover` runs the tests with NYC coverage and writes reports under the driver package.
- `lint` checks TypeScript sources with ESLint.
- `packagePacker` creates the npm deployment artifact through the CM package bundler.

The integration tests use Testcontainers and start a RabbitMQ broker. Docker must be available for the tests to run. They cover connection setup and command publishing, including queue address handling, address overrides, and dynamic addresses. The repository includes the last generated JUnit report at [`IoTAMQPDriver.IoT/src/driver-amqp/test/amqp-test-results.xml`](IoTAMQPDriver.IoT/src/driver-amqp/test/amqp-test-results.xml); it records six tests with zero failures from the recorded run. Test files are excluded from the npm package by `src/driver-amqp/.npmignore`.

## Configuration reference

At minimum, configure the driver with an AMQP broker address and port. The main communication fields are:

| Field | Default | Description |
| --- | --- | --- |
| `address` | `127.0.0.1` | AMQP broker host. |
| `port` | `5671` | AMQP broker port. |
| `username` / `password` | empty | Optional broker credentials. |
| `certificate` / `privateKey` | empty | Optional PEM certificate and private key for TLS. |
| `certificatesRootFolder` | `${tmp}/ConnectIoT/AMQP/Certificates/${id}` | Directory used for generated or resolved certificate files. |
| `heartbeatInterval` | `30000` | Heartbeat interval in milliseconds. |
| `setupTimeout` | `10000` | Setup timeout in milliseconds. |
| `intervalBeforeReconnect` | `5000` | Delay before reconnect attempts in milliseconds. |
| `connectingTimeout` | `30000` | Maximum connection wait in milliseconds. |

Command-specific extended data is defined in the npm package manifest and includes `address`, `durable`, `expirationPolicy`, and `capabilities`, plus parameter types for message body and subject values.
