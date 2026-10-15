# @criticalmanufacturing/audit - Plugin used to audit CM CLI based projects

## Installing

```
npm install --global @criticalmanufacturing/audit
```


After installation, a new command `audit` will be available in the [cmf-cli](https://github.com/criticalmanufacturing/cli) tool.

## Commands

### Audit Init

This command sets up the audit in your project. It always copies the `Cmf.Audit` package to the project root and adds it to the dependencies of the root `cmfpackage.json`. The package is picked for your project's MES version (major.minor, from `.project-config.json`). Its dependencies on MES packages (e.g. `Cmf.Environment`) are set to your exact MES version, including the patch. MES versions without a `Cmf.Audit` package are rejected. For Azure DevOps projects, it also generates the audit pipelines in `Builds` (`CD-AuditReview.yml` and its `.tasks`). If you agree, it also adds the Audit Review stage to your existing `Builds/CD-Containers.yml`.

The agent pool, service connection, build-runner container options and environments are read from your own pipelines (`CD-Containers.yml`, `.vars/global.yml`, `.vars/<Environment>.yml`, `.pipeline-config.json`). Generic tasks your project already has (e.g. `use-node-version.yml`) are kept. The Audit Review stage added to CD-Containers is off by default: set `RunAuditReview: true` in `Builds/.vars/global.yml` to turn it on.

When you don't pass `--azureDevOps` or `--includeInCDContainers`, the command asks you. If it can't ask (e.g. in CI), it uses the default answer: `true`.

Invoke:

```powershell
cmf audit init -h
```

Usage:
</br>
  - cmf audit init [options] 

Options:
  - --azureDevOps Whether the project uses Azure DevOps pipelines. When false, only the Cmf.Audit package is copied [default: prompt]
  - --includeInCDContainers Whether to merge the Audit Review stage into Builds/CD-Containers.yml [default: prompt, true]
  - --agentPool Azure DevOps agent pool [default: the one used by CD-Containers, global.yml or .pipeline-config.json]
  - --defaultServiceConnection Service connection used to pull the build-runner image [default: the one used by CD-Containers or global.yml]
  - --securityAccessTokenSecretName Name of the secret pipeline variable holding the MES security access token used by the audit [default: AuditSecurityAccessToken]
  - --cmfAuditVersion Version of cmf-audit installed by the pipelines [default: the running cmf-audit version]
  - --cmfAuditRegistry NPM registry the pipelines install cmf-audit from [default: https://registry.npmjs.com/]
  - --force Overwrite the audit files (Cmf.Audit package and audit pipelines) if they already exist [default: false]
  - -?, -h, --help        Show help and usage information

### Audit Review

This command will traverse all the customization of your project and run auditors for all the supported customization packages. If the generate report is set as true it will generate CSV files with results.

Invoke:

```powershell
cmf audit review -h
```

Usage:
</br>
  - cmf audit review [options] 

Options:
  - --azureDevOpsUrl The URI of the TFS collection or Azure DevOps organization. predefined variable: System.TeamFoundationCollectionUri. [default: null]
  - --teamProject The name of the project that contains this build. predefined variable: System.TeamProject [default: null]
  - --repository The name of the triggering repository. predefined variable: Build.Repository.Name [default: null]
  - --personalAccessToken Token used to access AzureDevOps REST API. predefined variable: System.AccessToken [default: null]
  - --infrastructureFileName Infrastructure file name [default: .pipeline-config.json]
  - --pipelineVersion cmf pipeline version to use [default: latest]
  - --npmRegistry npm registry version to use [default: https://criticalmanufacturing.io/repository/npm/]
  - --validatorsToRun Will generate a run the pipeline Auditor [default: []]
  - --generateReport Will generate a report and persist the flagged artifacts [default: true]
  - --reportOutputFolder Folder where a report will be generated, requires the generateReport flag to be true [default: temp folder]
  - -?, -h, --help        Show help and usage information

### Audit Review Pipeline

This command will compare the comitted pipelines against a generated set of pipelines. If the generate report is set as true it will generate CSV files with results.

Invoke:

```powershell
cmf audit review pipeline -h
```

Usage:
</br>
  - cmf audit review pipeline [options] 

Options:
  - --azureDevOpsUrl The URI of the TFS collection or Azure DevOps organization. predefined variable: System.TeamFoundationCollectionUri. [default: null]
  - --teamProject The name of the project that contains this build. predefined variable: System.TeamProject [default: null]
  - --repository The name of the triggering repository. predefined variable: Build.Repository.Name [default: null]
  - --personalAccessToken Token used to access AzureDevOps REST API. predefined variable: System.AccessToken [default: null]
  - --infrastructureFileName Infrastructure file name [default: .pipeline-config.json]
  - --pipelineVersion cmf pipeline version to use [default: latest]
  - --npmRegistry npm registry version to use [default: https://criticalmanufacturing.io/repository/npm/]
  - --validatorsToRun Will generate a run the pipeline Auditor [default: []]
  - --generateReport Will generate a report and persist the flagged artifacts [default: true]
  - --reportOutputFolder Folder where a report will be generated, requires the generateReport flag to be true [default: temp folder]
  - -?, -h, --help        Show help and usage information

### Audit Review Root

This command will audit the root custom package. If the generate report is set as true it will generate CSV files with results.

Invoke:

```powershell
cmf audit review root -h
```

Usage:
</br>
  - cmf audit review root [options] 

Arguments:
</br>
  - \<workingDir>    Working Directory [default: .]

Options:
  - --azureDevOpsUrl The URI of the TFS collection or Azure DevOps organization. predefined variable: System.TeamFoundationCollectionUri. [default: null]
  - --teamProject The name of the project that contains this build. predefined variable: System.TeamProject [default: null]
  - --repository The name of the triggering repository. predefined variable: Build.Repository.Name [default: null]
  - --personalAccessToken Token used to access AzureDevOps REST API. predefined variable: System.AccessToken [default: null]
  - --generateReport Will generate a report and persist the flagged artifacts [default: true]
  - --reportOutputFolder Folder where a report will be generated, requires the generateReport flag to be true [default: temp folder]
  - -?, -h, --help        Show help and usage information

### Audit Review Data

This command will audit the data custom package. If the generate report is set as true it will generate CSV files with results.

Invoke:

```powershell
cmf audit review data -h
```

Usage:
</br>
  - cmf audit review data [options] 

Arguments:
</br>
  - \<workingDir>    Working Directory [default: .]

Options:
  - --azureDevOpsUrl The URI of the TFS collection or Azure DevOps organization. predefined variable: System.TeamFoundationCollectionUri. [default: null]
  - --teamProject The name of the project that contains this build. predefined variable: System.TeamProject [default: null]
  - --repository The name of the triggering repository. predefined variable: Build.Repository.Name [default: null]
  - --personalAccessToken Token used to access AzureDevOps REST API. predefined variable: System.AccessToken [default: null]
  - --generateReport Will generate a report and persist the flagged artifacts [default: true]
  - --reportOutputFolder Folder where a report will be generated, requires the generateReport flag to be true [default: temp folder]
  - --packageId Id of Package to Audit, will only be used if command called directly [default: Cmf.Custom.Data ]
  - -?, -h, --help        Show help and usage information
