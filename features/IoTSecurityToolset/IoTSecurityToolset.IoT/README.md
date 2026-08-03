# IoT Security Toolset

A Critical Manufacturing MES Community feature that packages cryptographic security tasks for Connect IoT Controller Engine. The toolset provides RSA encryption/decryption and key setup tasks for securing communication and data in IoT workflows.

The feature is deployed as an IoT package containing security task implementations:

| Package | Package type | Purpose |
| --- | --- | --- |
| `Cmf.Community.IoTSecurityToolset.IoT` | `IoT` | Connect IoT package that installs the security tasks library and its deployment configuration. |
| `@criticalmanufacturing/connect-iot-controller-engine-custom-security-tasks` | npm package | The TypeScript/JavaScript implementation of RSA encryption, decryption, and key management tasks. |

## What is shipped

### Security tasks

The implementation is under [`src/custom-security-tasks/`](src/custom-security-tasks/). It includes three security-focused Controller Engine tasks:

- **`rsaSetup`**: Generates or initializes an RSA key pair. Outputs the public key for use in encryption operations.
- **`rsaEncrypter`**: Encrypts a string value using an RSA public key. Takes the public key and plain text value as inputs, returns Base64-encoded encrypted data.
- **`rsaDecrypter`**: Decrypts a Base64-encoded RSA-encrypted string using the corresponding private key. Returns the decrypted plain text value.

Each task is marked as `isController: true`, indicating it runs in the Connect IoT Controller Engine context and can be used in IoT workflows and device integration scenarios. Tasks follow the standard lifecycle as "Productive" and include success/error output ports for workflow control flow.

Task templates and metadata are located under [`src/custom-security-tasks/templates/`](src/custom-security-tasks/templates/), defining the task inputs, outputs, and integration points.

### Deployment configuration

[`ui.xml`](ui.xml) injects an IoT deployment wizard step for configuring local and npm server repositories. It includes:

- Directory repository configuration (optional local path for package storage)
- NPM server repository configuration (optional npm registry address, authentication, and publish tag)

This allows MES administrators to customize where IoT packages are sourced and published during deployment.

## `cmfpackage.json` manifest

[`cmfpackage.json`](cmfpackage.json) has `packageType: "IoT"`:

- `packageId` identifies the package as `Cmf.Community.IoTSecurityToolset.IoT`.
- `version` identifies the package release following the CM release versioning convention, aligned with MES `11.3.3`.
- `contentToPack` copies `src/*` into the deployed `node_modules` location, excluding files matched by `.npmignore`.
- `xmlInjection` injects `ui.xml` into the deployment package so repository configuration is available during installation.
- `isInstallable: true` makes the package installable via the CM deployment framework.
- `isUniqueInstall: false` allows multiple instances of the package to be installed.

An IoT package is the deployment unit for Connect IoT task/converter libraries. Here it carries the npm security tasks package, compiled JavaScript, type declarations, and the deployment UI configuration.

## Version and compatibility

The current package version is `11331.0.0`:

- IoT package: `Cmf.Community.IoTSecurityToolset.IoT@11331.0.0`
- npm tasks package: `@criticalmanufacturing/connect-iot-controller-engine-custom-security-tasks@0.0.0`
- Connect IoT dependencies use the `release-1133` npm dist tag.
- CM deployment compatibility: MES `11.3.3`

## Workspace and npm layout

The root [`package.json`](package.json) defines an npm workspace with the security tasks package under `src/*`:

```
.
├── package.json                              # Root workspace configuration
├── cmfpackage.json                           # Deployment package manifest
├── ui.xml                                    # Deployment wizard configuration
├── src/
│   └── custom-security-tasks/                # Security tasks library
│       ├── package.json                      # Tasks package manifest
│       ├── src/
│       │   ├── index.ts                      # Exports RSA tasks
│       │   ├── tasks/
│       │   │   ├── rsaSetup/
│       │   │   ├── rsaEncrypter/
│       │   │   └── rsaDecrypter/
│       │   └── ...
│       ├── templates/                        # Task metadata templates
│       │   ├── task_rsaSetup.json
│       │   ├── task_rsaEncrypter.json
│       │   └── task_rsaDecrypter.json
│       ├── test/                             # Unit tests for tasks
│       └── tsconfig.json, package.json, ...
└── Package/                                  # Pre-built deployment artifacts
    └── @cm-community-Cmf.Community.IoTSecurityToolset.IoT.11331.0.0.zip
```

The root `package.json` delegates build, test, lint, and packaging commands to the workspace packages via npm scripts. Run commands from the root directory.

## Build, package, and test

Run commands from this directory:

```bash
npm install
npm run build
npm test
npm run lint
npm run lint:fix
npm run packagePacker
```

The root npm workspace commands work as follows:

- `build` compiles the security tasks TypeScript sources and generates JavaScript and type declarations.
- `test` builds first, then runs unit tests for the security tasks.
- `test:cover` runs tests with code coverage reporting.
- `lint` checks TypeScript sources with ESLint.
- `lint:fix` automatically fixes linting issues.
- `packagePacker` creates the npm deployment artifact through the CM package bundler, generating the deployment zip file under `Package/`.

Unit tests are located under `src/custom-security-tasks/test/` and use Mocha test framework with Chai assertions. Test files are excluded from the npm package by `.npmignore`. Coverage reports are generated in standard formats (cobertura, lcov, text-summary).

## Pre-built deployment artifact

A pre-built deployment zip file is included under [`Package/`](Package/):

- `@cm-community-Cmf.Community.IoTSecurityToolset.IoT.11331.0.0.zip`

This artifact contains the packaged security tasks library ready for deployment to a MES environment. It is generated by the `packagePacker` command and can be deployed directly using the CM deployment framework.

## Further reading

See [`src/custom-security-tasks/README.md`](src/custom-security-tasks/README.md) for detailed implementation documentation and task-specific configuration details.
