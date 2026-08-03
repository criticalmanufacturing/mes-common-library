# IoT MTConnect Driver

A Critical Manufacturing MES Community feature that packages an MTConnect protocol driver for Connect IoT. The driver connects equipment integrations to MTConnect agents and reads device data through the MTConnect protocol.

The feature is deployed as a root package that contains one installable IoT package:

| Package | Package type | Purpose |
| --- | --- | --- |
| `Cmf.Community.IoTMTConnect` | `Root` | Deployment wrapper that installs the feature and its IoT dependency. |
| `Cmf.Community.IoTMTConnect.IoT` | `IoT` | Connect IoT package that installs the MTConnect driver npm package and its deployment wizard UI. |
| `@criticalmanufacturing/connect-iot-driver-mtconnect` | npm package | The TypeScript/JavaScript implementation of the MTConnect device driver. |

## What is shipped

The root package itself does not carry driver source; it declares the dependency that installs it. The MTConnect driver implementation, its Connect IoT deployment UI, its configuration fields, and its build/test tooling are all documented in the sub-package README:

- [`IoTMTConnect.IoT/README.md`](IoTMTConnect.IoT/README.md)

In summary, the sub-package installs `MTConnectDeviceDriver`, a Connect IoT `DeviceDriverBase` implementation that talks HTTP/HTTPS to MTConnect agents (through .NET Core interop via `edge-js`), supports the Probe, Current, Sample, and Assets MTConnect event/command types, and extracts property values from MTConnect XML responses using JSONata expressions. It also injects an IoT deployment wizard step (`ui.xml`) for configuring the package's npm repository.

### Other feature contents

- [`Libs/`](Libs/) contains the standard CM package library folders (`Business`, `Custom`, `EntityTypes`, `External`, `LBOs`, `PrivateFix`, `Tests`). They currently contain only `.gitkeep` placeholders (and empty `PrivateFix/Business` and `PrivateFix/HTML` subfolders); this feature does not ship additional Business, LBO, Entity Type, Custom, External, PrivateFix, or server-side test implementations.
- [`EnvironmentConfigs/env.json`](EnvironmentConfigs/env.json) is the feature environment configuration used by the deployment tooling. It defines placeholder database, security, and application settings for a local `MES` environment (host `cmf-common.local`, tenant `Community`/`Common.TimeTracking`) and is not intended for production use as-is.
- `deploymentdir/` is the deployment framework output directory (`CIPackages` and `Delivered` repositories, per `repositories.json` and `.project-config.json`). It is not present in this checkout; the deployment tooling creates it when packaging/deployment commands run.
- `global.json` pins the .NET SDK version (`8.0.309`, `rollForward: latestFeature`) used to build the feature.
- `NuGet.Config` configures the CMF and nuget.org NuGet package sources and disables source-control integration for the solution.
- `repositories.json` points the deployment framework at the local `CIPackages` and `Delivered` repositories under `deploymentdir/`.
- `.project-config.json` records the CM scaffolding project settings for this feature: project name `IoTMTConnect`, `RepositoryType: Customization`, `BaseLayer: MES`, `MESVersion`/`NugetVersion`/`TestScenariosNugetVersion` all `11.3.3`, the CMF NPM/NuGet registries, and the deployment directory paths.
- `.config/dotnet-tools.json` declares the local .NET tool `dotnet-coverage` (version `17.6.0`) used for code coverage.
- `.devcontainer/` configures the VS Code dev container, based on the `criticalmanufacturing.io/criticalmanufacturing/devcontainer:11` image, forwarding port `80` and running `cmf login sync` on folder open.

## `cmfpackage.json` manifests

CM deployment framework manifests describe installable deployment packages. The two manifests in this feature have different responsibilities:

### Root manifest

[`cmfpackage.json`](cmfpackage.json) has `packageType: "Root"`:

- `packageId` identifies the feature as `Cmf.Community.IoTMTConnect`.
- `version` identifies the feature release (`11331.0.0`).
- `description` is the generic CM Community customization deployment description.
- `isInstallable: true` makes the root package installable.
- `isUniqueInstall: false` means the package is not restricted to a single installation per environment.
- `dependencies` pulls in the IoT child package and optional CM deployment dependencies:
  - `Cmf.Environment` (`11.3.3`, `mandatory: false`) and `CriticalManufacturing.DeploymentMetadata` (`11.3.3`, `mandatory: false`) are optional dependencies for the MES environment/deployment framework; they are not required for the feature to install, but bring in environment and deployment metadata packages when present.
  - `Cmf.Community.IoTMTConnect.IoT` (`11331.0.0`) is the required package that contains the actual Connect IoT driver.

A root package is therefore the feature-level installation and dependency entry point. It does not itself pack the driver source; it composes the packages that do.

### IoT manifest

[`IoTMTConnect.IoT/cmfpackage.json`](IoTMTConnect.IoT/cmfpackage.json) has `packageType: "IoT"` and describes the installable child package, `Cmf.Community.IoTMTConnect.IoT`. Its fields (`contentToPack`, `xmlInjection`, `isToForceInstall`, `forceRerunAfterDatabaseRestore`) are explained in the sub-package README linked above.

## Version and compatibility

The current feature version is `11331.0.0`. The version follows the repository's CM release versioning convention and is aligned with MES `11.3.3`:

- Root package: `Cmf.Community.IoTMTConnect@11331.0.0`
- IoT package: `Cmf.Community.IoTMTConnect.IoT@11331.0.0`
- npm driver: `@criticalmanufacturing/connect-iot-driver-mtconnect@11331.0.0`
- CM deployment dependencies (`Cmf.Environment`, `CriticalManufacturing.DeploymentMetadata`): `11.3.3`

When creating a release, keep the root package, IoT package, and npm driver versions aligned. Update the CM dependency versions when targeting a different MES release.

## Build, package, and test

The root feature directory has no npm/build tooling of its own; build, test, lint, and packaging commands run from the IoT sub-package. Run commands from [`IoTMTConnect.IoT/`](IoTMTConnect.IoT/):

```bash
npm install
npm run build
npm test
npm run lint
npm run packagePacker
```

See [`IoTMTConnect.IoT/README.md`](IoTMTConnect.IoT/README.md) for what each script does (`build` compiles the driver and integration-test TypeScript sources and the .NET Core components; `test` runs Mocha integration tests; `test:cover` adds NYC coverage; `lint`/`lint:fix` run ESLint; `packagePacker` creates the npm deployment artifact) and for full driver implementation and configuration details.
