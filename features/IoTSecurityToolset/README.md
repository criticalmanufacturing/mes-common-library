# IoT Security Toolset

A Critical Manufacturing MES Community feature that packages cryptographic security tasks for Connect IoT Controller Engine. The toolset provides RSA key setup, encryption, and decryption tasks that IoT workflows can use to protect data and communication.

The feature is deployed as a root package that contains one installable IoT package:

| Package | Package type | Purpose |
| --- | --- | --- |
| `Cmf.Community.IoTSecurityToolset` | `Root` | Deployment wrapper that installs the feature and its IoT dependency. |
| `Cmf.Community.IoTSecurityToolset.IoT` | `IoT` | Connect IoT package that installs the security tasks library and its deployment configuration. |
| `@criticalmanufacturing/connect-iot-controller-engine-custom-security-tasks` | npm package | The TypeScript/JavaScript implementation of RSA encryption, decryption, and key management tasks. |

## What is shipped

The security tasks implementation, the deployment wizard UI, the workspace layout, and the build/test commands are documented in the IoT sub-package README: [`IoTSecurityToolset.IoT/README.md`](IoTSecurityToolset.IoT/README.md). In summary, the sub-package ships three Controller Engine tasks (`rsaSetup`, `rsaEncrypter`, `rsaDecrypter`) under `IoTSecurityToolset.IoT/src/custom-security-tasks/`, plus a `ui.xml` deployment wizard step for configuring local and npm repositories used to source/publish the package during installation. This root README covers only the feature-level (root) contents; see the linked README for task-level and build details.

## `cmfpackage.json` manifests

CM deployment framework manifests describe installable deployment packages. The two manifests in this feature have different responsibilities:

### Root manifest

[`cmfpackage.json`](cmfpackage.json) has `packageType: "Root"`:

- `packageId` identifies the feature as `Cmf.Community.IoTSecurityToolset`.
- `version` identifies the feature release, currently `11331.0.0`.
- `description` is the generic Community customization deployment description.
- `isInstallable: true` makes the root package installable.
- `isUniqueInstall: false` allows multiple instances of the package to be installed.
- `dependencies` pulls in the IoT child package and optional CM deployment dependencies:
  - `Cmf.Environment` (`11.3.3`, optional) — the MES environment framework dependency.
  - `CriticalManufacturing.DeploymentMetadata` (`11.3.3`, optional) — deployment framework metadata dependency.
  - `Cmf.Community.IoTSecurityToolset.IoT` (`11331.0.0`, mandatory by default since no `mandatory` flag is set) — the package that contains the actual Connect IoT security tasks and is required by the root package.

A root package is therefore the feature-level installation and dependency entry point. It does not itself pack any task source; it composes the packages that do.

### IoT manifest

[`IoTSecurityToolset.IoT/cmfpackage.json`](IoTSecurityToolset.IoT/cmfpackage.json) has `packageType: "IoT"`:

- `packageId` identifies the installable child as `Cmf.Community.IoTSecurityToolset.IoT`.
- `version` matches the root package's `11331.0.0`.
- `contentToPack` copies `src/*` into the deployed `node_modules` location, excluding files matched by `.npmignore`.
- `xmlInjection` injects `ui.xml` into the deployment package so the repository configuration step is available during installation.
- `isInstallable: true` / `isUniqueInstall: false` mirror the root package's installation settings.

An IoT package is the deployment unit for Connect IoT task/converter libraries. Here it carries the npm security tasks package, compiled JavaScript, type declarations, and the deployment UI configuration. Full detail on the tasks and manifest fields is in the [IoT sub-package README](IoTSecurityToolset.IoT/README.md).

## Version and compatibility

The current feature and IoT package version is `11331.0.0`. The package version follows the repository's CM release versioning convention and is aligned with MES `11.3.3`:

- Root package: `Cmf.Community.IoTSecurityToolset@11331.0.0`
- IoT package: `Cmf.Community.IoTSecurityToolset.IoT@11331.0.0`
- npm tasks package: `@criticalmanufacturing/connect-iot-controller-engine-custom-security-tasks@0.0.0`
- CM deployment dependencies (`Cmf.Environment`, `CriticalManufacturing.DeploymentMetadata`): `11.3.3`
- Connect IoT dependencies use the `release-1133` npm dist tag.

When creating a release, keep the root package, IoT package, and npm tasks package versions aligned. Update the CM dependency versions and the Connect IoT npm dist tag when targeting a different MES release.

## Build, package, and test

Run commands from [`IoTSecurityToolset.IoT/`](IoTSecurityToolset.IoT/), since that is where the npm workspace and its `package.json` live:

```bash
npm install
npm run build
npm test
npm run lint
npm run lint:fix
npm run packagePacker
```

- `build` compiles the security tasks TypeScript sources and generates JavaScript and type declarations.
- `test` builds first, then runs unit tests for the security tasks (Mocha/Chai, under `src/custom-security-tasks/test/`).
- `test:cover` runs tests with code coverage reporting (cobertura, lcov, text-summary).
- `lint` checks TypeScript sources with ESLint; `lint:fix` automatically fixes issues.
- `packagePacker` creates the npm deployment artifact through the CM package bundler, generating the deployment zip under `IoTSecurityToolset.IoT/Package/`.

The root feature directory has no npm workspace of its own — it only holds the `Root` `cmfpackage.json`, feature-level deployment/environment configuration, and pre-built package artifacts.

## Other top-level contents

- [`Libs/`](Libs/) contains the standard CM package library folders (`Business`, `Custom`, `EntityTypes`, `External`, `LBOs`, `Tests`, `PrivateFix/Business`, `PrivateFix/HTML`). All of them currently contain only `.gitkeep` placeholders; this feature ships no additional server-side Business, LBO, Entity Type, Custom, External, PrivateFix, or test implementations beyond the IoT security tasks.
- [`EnvironmentConfigs/env.json`](EnvironmentConfigs/env.json) is the feature's sample environment configuration (database, security portal, message bus, Kubernetes replica/resource sizing, volume paths, etc.) used by the deployment tooling.
- [`Package/`](Package/) contains the pre-built root deployment artifacts: `Cmf.Community.IoTSecurityToolset.11331.0.0.zip` and `@cm-community-Cmf.Community.IoTSecurityToolset.11331.0.0.zip`. These wrap the root package for deployment via the CM deployment framework; the corresponding IoT package artifact is built separately under `IoTSecurityToolset.IoT/Package/`.
- `global.json` pins the .NET SDK version (`8.0.301`, `rollForward: latestFeature`) used for any .NET-based tooling in this feature.
- `NuGet.Config` configures the CM and nuget.org package feeds and disables solution-level source control integration.
- `repositories.json` and `.project-config.json` point the CM deployment/build tooling at a `deploymentdir/` folder (`CIPackages` and `Delivered` repositories) for this feature. That folder is not present in the repository — it is created locally by the deployment/build tooling when needed, not checked in.
- [`.devcontainer/devcontainer.json`](.devcontainer/devcontainer.json) defines the VS Code dev container image (`criticalmanufacturing.io/criticalmanufacturing/devcontainer:11`) and features used for local development, including the CM CLI, Portal SDK, Docker-in-Docker, and a Chrome testing feature.
- [`.config/dotnet-tools.json`](.config/dotnet-tools.json) is a .NET local tool manifest pinning `dotnet-coverage` version `17.6.0`.
- `.project-config.json` also records project-level metadata such as the base layer (`MES`), tenant (`Community`), MES version (`11.3.0`), and NPM/NuGet registry addresses used by CM scaffolding tooling.
