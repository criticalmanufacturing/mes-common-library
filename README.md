# CM MES Common Library

This repository houses open-source features, libraries, and tools for the Critical Manufacturing (CM) MES platform.  It's intended to foster collaboration and code sharing within the CM MES developer and system integrators community.

## Purpose

The primary goal of this project is to provide reusable components that streamline and enhance CM MES customization projects.  These components are currently designed specifically for MES customization and may not be suitable for other applications.

## Scope

This library focuses on providing:

* **Features:**  Complete, self-contained functionalities that extend or modify the standard CM MES behavior.
* **Utilities:**  Reusable code snippets, helper functions, and libraries that can be incorporated into various customization projects.
* **Samples:**  Reusable master data that can be imported into an empty system.
* **Tools:**  Scripts or applications that aid in the development, testing, or deployment of CM MES customizations.

## File Structure

The repository is organized as follows:

```txt
📦mes-common-library
 ┣ 📂features                     # To store MES features
 ┃ ┗ 📂feature_x_root_folder
 ┣ 📂scaffolding                  # To store project/feature scaffolding helpers
 ┣ 📂samples                      # To store sample master data
 ┣ 📂utils                        # To store utilities or libraries
 ┃ ┗ 📂lib_or_tool_root_folder
 ┣ 📜LICENSE.txt
 ┗ 📜README.md
```

* **`features/`:** This directory contains self-contained features. Each feature resides in its own subdirectory (e.g., `feature_x/`).  Inside a feature directory, you'll find all the necessary files related to that feature, including source code, configuration files, documentation, and any other required assets.  Use descriptive names for feature directories.

* **`scaffolding/`:** This directory contains helper scripts and supporting configuration files used to bootstrap or automate repository-specific setup tasks in a consistent way. Currently the following is supported:

    * `scaffolding/new-feature.sh` - is specifically focused on creating a new CM MES customization project, its first feature, and the standard customization layers.

* **`samples/`:** This directory will contain a set of master data, that will show case how to setup MES use cases. It must include clear documentation of what each masterdata contains and the use case it covers. Each samples resides in its own subdirectory (e.g., `samples_x/`), the subdirectory must contain one of two, either an importable json/xlsx, an importable .zip. If the masterdata is to be deliverable as a CM CLI data package it should be in the features/ section.

* **`utils/`:** This directory houses reusable utilities, libraries, and tools.  Similar to features, each utility or tool should have its own subdirectory.  This promotes organization and prevents naming conflicts.  Include clear documentation within each utility/tool's directory explaining its purpose, usage, and any dependencies.

* **`LICENSE.txt`:** This file contains the license under which the code in this repository is distributed.  It's crucial to include a license to clarify the terms of use and distribution.  A common choice is the Apache 2.0 license.

* **`README.md`:** This file (the one you're reading) provides an overview of the repository, its purpose, structure, and how to contribute.

## Features Acceptance

All features must contain tests and documentation. The tests should be unit tests, or mocked tests, they should not depend on an MES instance. Every feature must be self describable. This means it should contain all the necessary code and documentation for it to be used. All features, will follow the CLI pipeline of `cmf restore`, `cmf build` and `cmf package`. 

If a feature does not contain tests and documentation it will be rejected. If a feature does not have enough test coverage or has missing documentation it will be rejected.

## Branching Naming Conventions

Since MES customizations are typically tied to specific MES versions, our branching naming convention is prefixed by `{MES_MAJOR}.{MES_MINOR}[/{PATCH}]` (patch is only used when specifically targeting a particular MES patch version).

We support the following branch types:

| Name         | Convention                                            | Description                                                                                        | Example                                  |
| ------------ | ----------------------------------------------------- | -------------------------------------------------------------------------------------------------- | ---------------------------------------- |
| Dev          | `{MES_MAJOR}.{MES_MINOR}/dev`                         | Development branch for features targeting a specific MES major/minor version.                      | `11.2/dev`                               |
| Dev (Patch)  | `{MES_MAJOR}.{MES_MINOR}.{PATCH}/dev`                 | Development branch for features targeting a specific MES patch version.                            | `11.2.3/dev`                             |
| Feature      | `{MES_MAJOR}.{MES_MINOR}/feature/{ID}-{Summary}`      | Branch used during development of a specific feature. Should be deleted after merging to dev.      | `11.2/feature/123-add-custom-validation` |
| Feature (Patch) | `{MES_MAJOR}.{MES_MINOR}.{PATCH}/feature/{ID}-{Summary}` | Feature branch targeting a specific patch version. Should be deleted after merging to dev.   | `11.2/3/feature/123-patch-fix`         |
| Bug          | `{MES_MAJOR}.{MES_MINOR}/bug/{ID}-{Summary}`          | Branch used during development of a bug fix. Should be deleted after merging to dev.               | `11.2/bug/456-fix-material-handling`     |
| Bug (Patch)  | `{MES_MAJOR}.{MES_MINOR}.{PATCH}/bug/{ID}-{Summary}`  | Bug fix branch targeting a specific patch version. Should be deleted after merging to dev.         | `11.2.3/bug/456-hotfix`                  |
| Main         | `main`                                                | The main branch containing repository documentation and usage guidelines.                        | `main`                                   |

## Release Tags and Versioning Convention

Our versioning convention follows the MES version alignment to ensure compatibility and traceability. Features and libraries can evolve across different MES versions, maintaining their own version lineage.

### Features Versioning

All CLI Packages must following format:

```
{MES_MAJOR}{MES_MINOR}{MES_PATCH}{MAJOR}.{MINOR}.{PATCH}
```

```json
{
  "packageId": "Cmf.Community.IoTDataPlatformHelperSuite",
  "version": "11301.0.0",
  (...)
}
```

The version will contain in the major the CM MES version plus the major, followed by minor and patch. For example, if a package applies to version 11.3.0 of CM MES and is in the version 1.0.0 of that package. It should have the version `11301.0.0`. If the package is updated to version 11.3.1 and no change is done beyond update it should move to version `11311.0.0`. If in version 11.3.0 the package suffers a breaking change, it will move to version `11302.0.0`.

If a package does not conform to this versioning it will not be accepted. This is enforced both in CI (on every PR and push, see `.github/workflows/_build-changed.yml`) and locally through a `pre-commit` git hook (see [Commit Message & Hook Enforcement](CONTRIBUTING.md#commit-message--hook-enforcement) in `CONTRIBUTING.md`).

### Tag Format

```
{MES_MAJOR}_{MES_MINOR}[_{MES_PATCH}]-v{MAJOR}_{MINOR}_{PATCH}
```

### Components

- **`{MES_MAJOR}_{MES_MINOR}[_{MES_PATCH}]`**: The MES version this release is compatible with (patch is optional and used only when targeting a specific patch)
- **`{MAJOR}`**: Package major version - incremented for breaking changes in the feature/library itself
- **`{MINOR}`**: Package minor version - incremented for backward-compatible functionality additions
- **`{PATCH}`**: Package patch version - incremented for backward-compatible bug fixes

### Versioning Strategy

A feature or library can evolve across MES versions with its own version lineage. For example:
- A feature starts at `11_1-v1_0_0` for MES 11.1
- When adapted for MES 11.2 with significant changes, it becomes `11_2-v2_0_0`
- A bug fix for the MES 11.1 version would be `11_1-v1_0_1`
- A bug fix for the MES 11.2 version would be `11_2-v2_0_1`

### Examples

| Tag              | Description                                                                 |
| ---------------- | --------------------------------------------------------------------------- |
| `11_1-v1_0_0`    | Version 1.0.0 of a feature for MES v11.1.x                                  |
| `11_2-v2_0_0`    | Version 2.0.0 of the same feature adapted for MES v11.2.x                   |
| `11_2-v2_1_0`    | Added new features to the MES 11.2 version                                  |
| `11_2-v2_1_1`    | Bug fix for the MES 11.2 version                                            |
| `11_3-v3_0_0`    | Version 3.0.0 of the feature for MES v11.3.x                                |

**Important Note**: Package versions can evolve independently across MES versions. The same feature may have different major versions for different MES releases (e.g., `11_1-v1_5_0` and `11_2-v2_0_0`), reflecting the changes needed to adapt to each MES version.

### Release Process

1. **Create the release from the dev branch**: Ensure all features/bugs are merged into the appropriate dev branch (e.g., `11.2/dev`)
2. **Release**: Run the Release Pipeline. This will tag the release and will publish the artifacts

### Version Compatibility

- A library version tagged for a specific MES patch (e.g., `11_2_3-v1_0_0`) is compatible only with that patch version
- A library version tagged for a MES major/minor without a specific patch (e.g., `11_2-v1_0_0`) is compatible with all patch versions of that MES release (11.2.x)
- Each MES major/minor version maintains its own version sequence - versions are not comparable across different MES versions
- Always use the latest library version available for your specific MES version

## Commit Hooks (Husky & Commitlint)

This repository uses [Husky](https://typicode.github.io/husky/) to manage Git hooks and [commitlint](https://commitlint.js.org/) to enforce commit message formatting. Hooks are installed automatically the first time you run `npm install` at the repository root (this also happens automatically when the devcontainer is created, via `postCreateCommand`).

Two hooks are configured under `.husky/`:

* **`commit-msg`** — runs `commitlint`, which checks the commit message against the [`@commitlint/config-conventional`](https://github.com/conventional-changelog/commitlint/tree/master/%40commitlint/config-conventional) ruleset (configured in `.commitlintrc.json`). This enforces the [Conventional Commits](https://www.conventionalcommits.org/) format `type(scope): subject`, e.g.:

    ```
    fix: correct offset calculation in the IoT persistency task
    feat(IoTMTConnect): add support for MTConnect adapters over TLS
    ```

    Messages that don't follow this pattern (missing type, missing subject, etc.) are rejected before the commit is created — this applies to `git commit --amend` as well.

* **`pre-commit`** — runs `.github/scripts/validate-package-versions.sh`, which rejects the commit if any `cmfpackage.json` version doesn't follow the [Features Versioning](#features-versioning) rule (`{MES_MAJOR}{MES_MINOR}{MES_PATCH}{MAJOR}.{MINOR}.{PATCH}`).

Both checks are also enforced in CI (see `.github/workflows/_build-changed.yml`) as a backstop, but the hooks give you the same feedback locally before you push. See [CONTRIBUTING.md](CONTRIBUTING.md#commit-message--hook-enforcement) for more details.

## Contributing

We encourage contributions from the CM MES developer community!  Please see the [CONTRIBUTING.md](CONTRIBUTING.md) file (or create one) for guidelines on how to contribute code, report bugs, or suggest new features.  This file should detail the contribution process, coding style guidelines, and any other relevant information.

## License

This project is licensed under the [BSD 3-Clause License](LICENSE.txt) - see the [LICENSE.txt](LICENSE.txt) file for details.  *(Replace with the actual license)*
