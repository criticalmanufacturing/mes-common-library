# CM MES Common Library

This repository houses open-source features, libraries, and tools for the Critical Manufacturing (CM) MES platform.  It's intended to foster collaboration and code sharing within the CM MES developer and system integrators community.

## Purpose

The primary goal of this project is to provide reusable components that streamline and enhance CM MES customization projects.  These components are currently designed specifically for MES customization and may not be suitable for other applications.

## Scope

This library focuses on providing:

* **Features:**  Complete, self-contained functionalities that extend or modify the standard CM MES behavior.
* **Utilities:**  Reusable code snippets, helper functions, and libraries that can be incorporated into various customization projects.
* **Tools:**  Scripts or applications that aid in the development, testing, or deployment of CM MES customizations.

## File Structure

The repository is organized as follows:

```txt
📦mes-common-library
 ┣ 📂features                     # To store MES features
 ┃ ┗ 📂feature_x_root_folder
 ┣ 📂utils                        # To store utilities or libraries
 ┃ ┗ 📂lib_or_tool_root_folder
 ┣ 📜LICENSE.txt
 ┗ 📜README.md
```

* **`features/`:** This directory contains self-contained features. Each feature resides in its own subdirectory (e.g., `feature_x/`).  Inside a feature directory, you'll find all the necessary files related to that feature, including source code, configuration files, documentation, and any other required assets.  Use descriptive names for feature directories.

* **`utils/`:** This directory houses reusable utilities, libraries, and tools.  Similar to features, each utility or tool should have its own subdirectory.  This promotes organization and prevents naming conflicts.  Include clear documentation within each utility/tool's directory explaining its purpose, usage, and any dependencies.

* **`LICENSE.txt`:** This file contains the license under which the code in this repository is distributed.  It's crucial to include a license to clarify the terms of use and distribution.  A common choice is the Apache 2.0 license.

* **`README.md`:** This file (the one you're reading) provides an overview of the repository, its purpose, structure, and how to contribute.

## Branching Naming Conventions

Since MES customizations are typically tied to specific MES versions, our branching naming convention is prefixed by `{MES_MAJOR}/{MES_MINOR}[/{PATCH}]` (patch is only used when specifically targeting a particular MES patch version).

We support the following branch types:

| Name         | Convention                                            | Description                                                                                        | Example                                  |
| ------------ | ----------------------------------------------------- | -------------------------------------------------------------------------------------------------- | ---------------------------------------- |
| Dev          | `{MES_MAJOR}/{MES_MINOR}/dev`                         | Development branch for features targeting a specific MES major/minor version.                      | `11/2/dev`                               |
| Dev (Patch)  | `{MES_MAJOR}/{MES_MINOR}/{PATCH}/dev`                 | Development branch for features targeting a specific MES patch version.                            | `11/2/3/dev`                             |
| Feature      | `{MES_MAJOR}/{MES_MINOR}/feature/{ID}-{Summary}`      | Branch used during development of a specific feature. Should be deleted after merging to dev.      | `11/2/feature/123-add-custom-validation` |
| Feature (Patch) | `{MES_MAJOR}/{MES_MINOR}/{PATCH}/feature/{ID}-{Summary}` | Feature branch targeting a specific patch version. Should be deleted after merging to dev.   | `11/2/3/feature/123-patch-fix`         |
| Bug          | `{MES_MAJOR}/{MES_MINOR}/bug/{ID}-{Summary}`          | Branch used during development of a bug fix. Should be deleted after merging to dev.            | `11/2/bug/456-fix-material-handling`     |
| Bug (Patch)  | `{MES_MAJOR}/{MES_MINOR}/{PATCH}/bug/{ID}-{Summary}`  | Bug fix branch targeting a specific patch version. Should be deleted after merging to dev.      | `11/2/3/bug/456-hotfix`                  |
| Main         | `main`                                                | The main branch containing repository documentation and usage guidelines.                        | `main`                                   |

## Release Tags and Versioning Convention

Our versioning convention follows the MES version alignment to ensure compatibility and traceability. Features and libraries can evolve across different MES versions, maintaining their own version lineage.

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

1. **Create the release from the dev branch**: Ensure all features/bugs are merged into the appropriate dev branch (e.g., `11/2/dev`)
2. **Tag the release**: Use the versioning format above, incrementing the version for the specific MES version
3. **Generate release notes**: Document changes, new features, bug fixes, and breaking changes
4. **Publish the release**: Create a GitHub release with the tag and release notes

### Version Compatibility

- A library version tagged for a specific MES patch (e.g., `11_2_3-v1_0_0`) is compatible only with that patch version
- A library version tagged for a MES major/minor without a specific patch (e.g., `11_2-v1_0_0`) is compatible with all patch versions of that MES release (11.2.x)
- Each MES major/minor version maintains its own version sequence - versions are not comparable across different MES versions
- Always use the latest library version available for your specific MES version

## Contributing

We encourage contributions from the CM MES developer community!  Please see the [CONTRIBUTING.md](CONTRIBUTING.md) file (or create one) for guidelines on how to contribute code, report bugs, or suggest new features.  This file should detail the contribution process, coding style guidelines, and any other relevant information.

## License

This project is licensed under the [BSD 3-Clause License](LICENSE.txt) - see the [LICENSE.txt](LICENSE.txt) file for details.  *(Replace with the actual license)*

## (Optional) Installation

If applicable, add instructions on how to install and use the components in this library.  This section would be particularly important if you have tools or libraries that need to be set up.  For example:

## Installation (Example for a Tool)

1. Clone the repository: `git clone https://github.com/your-org/mes-common-library.git`
2. Navigate to the tool's directory: `cd mes-common-library/utils/my_tool`
3. Install dependencies (if any): `npm install`
4. Run the tool: `node my_tool.js`
