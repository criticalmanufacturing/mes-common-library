# System Monitoring - Installed Customization Packages

## Requirement Specification

System administrators need to see which customization packages are installed in an environment without database access. **System Monitoring > Application > Versioning** lists, besides the product components, the installed customization packages whose name matches configured patterns: one row per package with its latest installed version and the install date.

## Design Specification

### Relevant Artifacts

Name | Type | Is Mandatory | Data Type | Description
:--- | :--- | :----------: | :-------- | :----------
V_InstalledPackage | Database view (`UserDataModel`, package `Cmf.Community.SystemMonitoringView.Database.Post`, Online database) | Yes | View | Latest installed version and install date per package id, read from the installation journal `dbo.T_PackageInfo` (installed rows only, not removed).
CustomAppendInstalledCustomizationPackages | DEE action (Post) | Yes | Action group `ApplicationMonitoringManagement.ApplicationMonitoringManagementOrchestration.GetApplicationMonitoringInformation.Post` | Reads the view and adds the package rows to the response.
/Community/SystemMonitoringView/InstalledPackagePatterns/ | Config entry | No | String | `;`-separated SQL LIKE patterns. `%` matches any run of characters, `_` one character; both always act as wildcards (there is no escape, so a literal `_` or `%` cannot be matched), and character sets in `[]` are not supported. Matching ignores case. The installed value is `Cmf.Community.%`. If the entry is empty, missing or contains only `;` separators, no package is shown (and the view is not queried): clear the entry to hide the list.

### How it works

1. When the Versioning (customization) context is requested, the DEE reads the view `UserDataModel.V_InstalledPackage`.
2. The packages whose id matches one of the configured patterns are added to the page, one row per package, with the latest installed version and the install date.
3. The config entry is read on every request: changing it takes effect on the next page load, without redeploy.

Behaviour:

- Packages are added only when the Versioning context is requested; requests for other contexts do not query the view.
- When no package matches, nothing is added.
- If the view cannot be read, the page shows the product rows only and a warning is logged.

### Assumptions

- The installation journal `dbo.T_PackageInfo` is owned by the deployment tooling (Setup/EnvManager) and is not a public contract. The view is the single place to adjust if the table shape changes.
- Only installed rows are listed: journal rows with status other than installed, or marked as removed (a `RemovedBy` value), are ignored. If the deployment tooling records an uninstall in some other way, that package stays listed until its row is marked as removed.
- One row per package id: the most recent install wins, even when its version is lower than an earlier one (a downgrade).
- The install date is shown as the deployment tooling stored it, formatted `yyyy-MM-dd HH:mm:ss` without a time zone; on the environment this was verified on it is UTC.

### How to verify

1. Open **System Monitoring > Application > Versioning**: the custom packages (e.g. `Cmf.Community.*`) are listed with version and install date.
2. Change the config entry `/Community/SystemMonitoringView/InstalledPackagePatterns/` and reload the page: the list changes accordingly.

## Work items

User Story | Type | Title | Description
:--------- | :--- | :---- | :----------
AB#132 | User Story | System Monitoring: show installed customization packages in Application / Versioning | Lists the installed customization packages in Application / Versioning.
