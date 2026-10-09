<#
.SYNOPSIS
  Builds the MESSimulator image in an OpenShift cluster and deploys it.

.DESCRIPTION
  Run it after "oc login". It
    1. stages a clean copy of the source (no appsettings.json with the token, no tests),
    2. creates (or selects) the project,
    3. creates the Secret with the MES access token and the ConfigMap with the line file,
    4. applies the build and the deployment (deploy/openshift/*.yaml, renamed to -Name),
    5. builds the image in the cluster from the staged source and rolls the Deployment out.

  Every object is named after -Name (default "messimulator"): the Deployment, BuildConfig, ImageStream and Secret
  <name>, the ConfigMap <name>-line and the registry Secret <name>-registry. Several simulators (e.g. one per MES) can
  so live in the same project.

  Where the image goes:
    - the cluster's integrated registry (the default), when the cluster has one;
    - Docker Hub, with -DockerHubUser: for a cluster without an integrated registry. The build still runs in the
      cluster and pushes to docker.io/<user>/<name> with the credentials you give, kept in a Secret.

.PARAMETER Name
  Name of the deployment and of every object it creates. Lowercase letters, digits and '-' (a DNS label), at most 40
  characters. Default: messimulator.

.PARAMETER LineFile
  The line file the simulator runs, relative to the MESSimulator folder (e.g. simulationConfiguration.industrial.json).
  Default: simulationConfiguration.semi.json.

.PARAMETER EnvironmentAddress
  The MES address, e.g. https://mes.example.com. Required (except with -StageOnly): deployment.yaml is a template and
  holds no environment.

.PARAMETER ClientId
  The MES client id of the access token. Default: MES.

.PARAMETER Speed
  Simulation speed (1: real-fab cadence; 20: 20x faster). Default: 1.

.PARAMETER MaxOrders
  Orders to launch before the simulator stops launching (0: no limit). Default: 0.

.PARAMETER TerminatePreviousRuns
  Terminate what earlier runs left behind each time the pod starts: every simulator order of the MES (named like the
  line's Line:Order:ProductionOrderNameFormat). Only when this simulator owns the MES. The pod has no console to confirm
  it, so this also turns the confirmation off.

.PARAMETER Settings
  Any other simulator setting, as MESSIM_ environment variables without the prefix, e.g.
  @{ "Line__Order__MaxLotsInFlight" = "10"; "Mes__MaxConcurrentCalls" = "8" }.

.PARAMETER CpuRequest
  Pod CPU request. Default: 100m. -CpuLimit: 500m, -MemoryRequest: 256Mi, -MemoryLimit: 512Mi.

.PARAMETER Project
  OpenShift project (namespace). An existing one is used as it is; one that does not exist is created. Without it: the
  project you are in now ("oc project"), else -Name.

.PARAMETER Token
  The MES personal access token. Without it: the MESSIM_TOKEN environment variable, then -TokenFromAppSettings, then a prompt.
  It is never printed, and never part of the uploaded source.

.PARAMETER TokenFromAppSettings
  Read the token from the local appsettings.json (the one you run the simulator with locally).

.PARAMETER DockerHubUser
  Push the image to docker.io/<DockerHubUser>/<name> (see above).

.PARAMETER DockerHubToken
  A Docker Hub access token with read and write permission (Account settings > Personal access tokens). Without it: the
  DOCKERHUB_TOKEN environment variable, then a prompt. Not your password.

.PARAMETER NuGetUser
  User for the Critical Manufacturing NuGet feed (nuget.config source "CMF"), needed to restore Cmf.LightBusinessObjects
  in the cluster build. Without it: the NUGET_CMF_USERNAME environment variable, then the "CMF" credentials of your
  user NuGet.Config, then a prompt. The password/token comes from NUGET_CMF_PASSWORD, the same NuGet.Config entry, or
  a prompt. It is only written to the staged source for the build, and deleted afterwards.

.PARAMETER Tag
  Image tag with -DockerHubUser. Default: latest.

.PARAMETER RenderOnly
  Only render the manifests that would be applied (no cluster needed).

.PARAMETER StageOnly
  Only stage the source and print where; no cluster needed. Handy to check what the image is built from.

.PARAMETER SkipBuild
  Apply the configuration, but don't build (e.g. only the line file or the settings changed).
#>
param(
    [string]$Name = "messimulator",
    [string]$LineFile = "simulationConfiguration.semi.json",
    [string]$EnvironmentAddress,
    [string]$ClientId = "MES",
    [decimal]$Speed = 1,
    [int]$MaxOrders = 0,
    [switch]$TerminatePreviousRuns,
    [hashtable]$Settings = @{},
    [string]$CpuRequest = "100m",
    [string]$CpuLimit = "500m",
    [string]$MemoryRequest = "256Mi",
    [string]$MemoryLimit = "512Mi",
    [string]$Project,
    [string]$Token,
    [switch]$TokenFromAppSettings,
    [string]$DockerHubUser,
    [string]$DockerHubToken,
    [string]$NuGetUser,
    [string]$Tag = "latest",
    [switch]$RenderOnly,
    [switch]$StageOnly,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# The name becomes object names and a label value: a DNS label, short enough for the "-registry" suffix
if ($Name -notmatch '^[a-z0-9]([-a-z0-9]*[a-z0-9])?$' -or $Name.Length -gt 40) {
    throw "-Name '$Name' must be lowercase letters, digits and '-', start and end with a letter or digit, and be at most 40 characters"
}

# The environment-dependent values (see deployment.yaml, a template)
if (-not $StageOnly) {
    if (-not $EnvironmentAddress) { throw "-EnvironmentAddress is required: the MES to run against, e.g. https://mes.example.com" }
    if ($EnvironmentAddress -notmatch '^https?://[^\s"]+$') { throw "-EnvironmentAddress '$EnvironmentAddress' must be an http(s) address" }
    if ($Speed -le 0) { throw "-Speed must be > 0" }
    if ($MaxOrders -lt 0) { throw "-MaxOrders must be >= 0 (0: no limit)" }
    foreach ($key in $Settings.Keys) {
        if ($key -notmatch '^[A-Za-z][A-Za-z0-9_]*$') { throw "-Settings key '$key' must be a setting path with '__' between the levels, e.g. Line__Order__MaxLotsInFlight" }
    }
    foreach ($quantity in $CpuRequest, $CpuLimit, $MemoryRequest, $MemoryLimit) {
        if ($quantity -notmatch '^[0-9.]+[A-Za-z]*$') { throw "'$quantity' is not a Kubernetes quantity (e.g. 500m, 512Mi)" }
    }
}

# A value as a double-quoted YAML string
function ConvertTo-YamlString([string]$value) {
    '"' + ($value -replace '\\', '\\' -replace '"', '\"') + '"'
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$manifests = $PSScriptRoot
$external = [bool]$DockerHubUser
$image = if ($external) { "docker.io/$DockerHubUser/$Name" } else { $null }
$lineName = "$Name-line"
$registrySecret = "$Name-registry"
$linePath = Join-Path $root $LineFile
if (-not (Test-Path $linePath)) { throw "Line file '$LineFile' not found in $root" }

function Invoke-Oc {
    & oc @args
    if ($LASTEXITCODE -ne 0) { throw "oc $($args -join ' ') failed (exit code $LASTEXITCODE)" }
}

# The manifests to apply: build.yaml and deployment.yaml with every "messimulator" (object names, ConfigMap and Secret
# references, the app label, the ImageStream tag) replaced by -Name, and deployment.yaml's @...@ values filled in from
# the parameters (-EnvironmentAddress, -ClientId, -Speed, -MaxOrders, -TerminatePreviousRuns, -Settings, resources).
# For an external registry also the image repository, the push and pull secret, and no ImageStream or image trigger
# (which belong to the integrated registry)
function New-Manifests {
    $overlay = Join-Path ([IO.Path]::GetTempPath()) "$Name-manifests"
    if (Test-Path $overlay) { Remove-Item $overlay -Recurse -Force }
    New-Item -ItemType Directory -Path $overlay | Out-Null
    foreach ($file in "build.yaml", "deployment.yaml") {
        $yaml = ((Get-Content (Join-Path $manifests $file) -Raw) -replace "`r`n", "`n") -replace '\bmessimulator\b', $Name
        if ($file -eq "deployment.yaml") {
            $values = [ordered]@{
                ENVIRONMENT_ADDRESS     = $EnvironmentAddress
                CLIENT_ID               = $ClientId
                SPEED                   = $Speed.ToString([Globalization.CultureInfo]::InvariantCulture)
                MAX_ORDERS              = "$MaxOrders"
                TERMINATE_PREVIOUS_RUNS = if ($TerminatePreviousRuns) { "true" } else { "false" }
                CONFIRM_TERMINATE       = if ($TerminatePreviousRuns) { "false" } else { "true" }
                CPU_REQUEST             = $CpuRequest
                CPU_LIMIT               = $CpuLimit
                MEMORY_REQUEST          = $MemoryRequest
                MEMORY_LIMIT            = $MemoryLimit
            }
            foreach ($entry in $values.GetEnumerator()) {
                # The template quotes every value: replace the quoted placeholder with a safely quoted value
                $yaml = $yaml.Replace("""@$($entry.Key)@""", (ConvertTo-YamlString $entry.Value))
            }

            # -Settings: one more MESSIM_ variable each, where the template has the @SETTINGS@ marker
            $settingsLines = foreach ($key in ($Settings.Keys | Sort-Object)) {
                "            - name: MESSIM_$key`n              value: $(ConvertTo-YamlString $Settings[$key])"
            }
            # ('$' is special in a -replace replacement: doubled so a value can hold it)
            $settingsBlock = (($settingsLines | ForEach-Object { "$_`n" }) -join '').Replace('$', '$$')
            $yaml = $yaml -replace '(?m)^[ \t]*# @SETTINGS@.*\n', $settingsBlock

            if ($yaml -match '@[A-Z_]+@') {
                throw "deployment.yaml still has the placeholder $($Matches[0]) after filling in the parameters"
            }
        }
        [IO.File]::WriteAllText((Join-Path $overlay $file), $yaml)
    }

    $kustomization = @'
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
resources:
  - build.yaml
  - deployment.yaml
'@
    if ($external) {
        $kustomization += @'

images:
  - name: @NAME@
    newName: @IMAGE@
    newTag: "@TAG@"
patches:
  - target:
      kind: ImageStream
      name: @NAME@
    patch: |-
      $patch: delete
      apiVersion: image.openshift.io/v1
      kind: ImageStream
      metadata:
        name: @NAME@
  - target:
      kind: BuildConfig
      name: @NAME@
    patch: |-
      apiVersion: build.openshift.io/v1
      kind: BuildConfig
      metadata:
        name: @NAME@
      spec:
        output:
          to:
            kind: DockerImage
            name: @IMAGE@:@TAG@
          pushSecret:
            name: @SECRET@
  - target:
      kind: Deployment
      name: @NAME@
    patch: |-
      - op: remove
        path: /metadata/annotations/image.openshift.io~1triggers
  - target:
      kind: Deployment
      name: @NAME@
    patch: |-
      apiVersion: apps/v1
      kind: Deployment
      metadata:
        name: @NAME@
      spec:
        template:
          spec:
            imagePullSecrets:
              - name: @SECRET@
'@
    }
    $kustomization = $kustomization.Replace("@NAME@", $Name).Replace("@IMAGE@", "$image").Replace("@TAG@", $Tag).Replace("@SECRET@", $registrySecret)
    [IO.File]::WriteAllText((Join-Path $overlay "kustomization.yaml"), ($kustomization -replace "`r`n", "`n"))
    return $overlay
}

if ($RenderOnly) {
    & oc kustomize (New-Manifests)
    return
}

# 1. A clean copy of the source: the build context is what the image is built from, so keep the token and the tests out
$stage = Join-Path ([IO.Path]::GetTempPath()) "$Name-src"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null
# Folders (by name, at any depth) and files left out; works with Windows PowerShell and pwsh on Linux/macOS
$excludedFolders = @("bin", "obj", ".vs", ".vscode", ".claude", ".github", ".git", "Tests", "deploy", "master data", "scratch")
$excludedFiles = @("appsettings*.json", "*.user", "*.md", ".nuget-cmf-credentials")
$rootPrefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
foreach ($file in Get-ChildItem -Path $root -Recurse -File -Force) {
    $relative = $file.FullName.Substring($rootPrefix.Length)
    $folders = @($relative -split '[\\/]' | Select-Object -SkipLast 1)
    if ($folders | Where-Object { $excludedFolders -contains $_ }) { continue }
    $name = $file.Name
    if ($excludedFiles | Where-Object { $name -like $_ }) { continue }
    $target = Join-Path $stage $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
}
if (Get-ChildItem $stage -Recurse -Filter "appsettings*.json") { throw "appsettings.json is in the staged source: it holds the token" }
# The repository is CRLF; the Docker build reads these two with LF
foreach ($file in "Dockerfile", ".dockerignore") {
    $path = Join-Path $stage $file
    [IO.File]::WriteAllText($path, ((Get-Content $path -Raw) -replace "`r`n", "`n"))
}
Write-Host "Staged the source in $stage ($((Get-ChildItem $stage -Recurse -File | Measure-Object).Count) files)"
if ($StageOnly) { return }

# 2. Logged in? Project
$user = (& oc whoami 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Not logged in to OpenShift ($user). Run: oc login --web --server=<api url>" }
Write-Host "Logged in as $user on $(& oc whoami --show-server)"
if (-not $Project) {
    $Project = (& oc project -q 2>$null)
    if ($LASTEXITCODE -ne 0 -or -not $Project) { $Project = $Name }
}
& oc get project $Project 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Project '$Project' does not exist: creating it"
    Invoke-Oc new-project $Project
} else {
    Write-Host "Using the existing project '$Project'"
    Invoke-Oc project $Project

    # This deploys objects named $Name / $Name-line: applying them updates any that already exist
    $existing = @("imagestream/$Name", "buildconfig/$Name", "deployment/$Name", "secret/$Name", "secret/$registrySecret", "configmap/$lineName") |
        Where-Object { & oc get $_ -o name 2>$null; $LASTEXITCODE -eq 0 }
    $global:LASTEXITCODE = 0
    if ($existing) {
        Write-Host "Already in '$Project', and updated by this deployment: $($existing -join ', ')"
    }
}
Write-Host "Deploying '$Name' with the line $LineFile against $EnvironmentAddress (speed $Speed$(if ($TerminatePreviousRuns) { ', terminating previous runs at start' }))"

# 3. Secret (token) and ConfigMap (line file, mounted as /config/simulationConfiguration.json)
if (-not $Token) { $Token = $env:MESSIM_TOKEN }
if (-not $Token -and $TokenFromAppSettings) {
    $settings = Get-Content (Join-Path $root "appsettings.json") -Raw | ConvertFrom-Json
    $Token = $settings.ClientConfiguration.Authentication.SecurityAccessToken
}
if (-not $Token) {
    $secure = Read-Host "MES personal access token" -AsSecureString
    $Token = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}
if (-not $Token) { throw "No access token" }

$tokenFile = Join-Path ([IO.Path]::GetTempPath()) "$Name-token"
try {
    # From a file, not --from-literal: the token must not show in the process list
    [IO.File]::WriteAllText($tokenFile, $Token.Trim())
    & oc create secret generic $Name --from-file=access-token=$tokenFile --dry-run=client -o yaml | & oc apply -f -
    if ($LASTEXITCODE -ne 0) { throw "Creating the secret failed" }
} finally {
    Remove-Item $tokenFile -Force -ErrorAction SilentlyContinue
}

# The repository's line files have a BOM and CRLF, which oc would store as an unreadable escaped string: a clean copy.
# Whatever the file is called, the key is simulationConfiguration.json (the Deployment reads /config/simulationConfiguration.json)
$cleanLine = Join-Path ([IO.Path]::GetTempPath()) "$Name-simulationConfiguration.json"
$lineJson = (Get-Content $linePath -Raw -Encoding UTF8).TrimStart([char]0xFEFF) -replace "`r`n", "`n"
[IO.File]::WriteAllText($cleanLine, $lineJson, (New-Object Text.UTF8Encoding($false)))
try {
    # One quoted argument: --from-file=simulationConfiguration.json=(Join-Path ...) would be split in two by PowerShell
    & oc create configmap $lineName "--from-file=simulationConfiguration.json=$cleanLine" --dry-run=client -o yaml | & oc apply -f -
} finally {
    Remove-Item $cleanLine -Force -ErrorAction SilentlyContinue
}
if ($LASTEXITCODE -ne 0) { throw "Creating the line ConfigMap failed" }

# The registry credential: the build pushes with it, the pods pull with it
if ($external) {
    if (-not $DockerHubToken) { $DockerHubToken = $env:DOCKERHUB_TOKEN }
    if (-not $DockerHubToken) {
        $secure = Read-Host "Docker Hub access token for $DockerHubUser (read and write)" -AsSecureString
        $DockerHubToken = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
    }
    if (-not $DockerHubToken) { throw "No Docker Hub token" }

    $authFile = Join-Path ([IO.Path]::GetTempPath()) "$Name-dockerconfig.json"
    try {
        $auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${DockerHubUser}:$($DockerHubToken.Trim())"))
        $dockerConfig = @{ auths = @{ "https://index.docker.io/v1/" = @{ username = $DockerHubUser; password = $DockerHubToken.Trim(); auth = $auth } } } | ConvertTo-Json -Depth 5
        [IO.File]::WriteAllText($authFile, $dockerConfig, (New-Object Text.UTF8Encoding($false)))
        & oc create secret generic $registrySecret --type=kubernetes.io/dockerconfigjson "--from-file=.dockerconfigjson=$authFile" --dry-run=client -o yaml | & oc apply -f -
        if ($LASTEXITCODE -ne 0) { throw "Creating the registry secret failed" }
    } finally {
        Remove-Item $authFile -Force -ErrorAction SilentlyContinue
    }

    # A first attempt may have left the integrated-registry ImageStream behind: nothing uses it now
    & oc delete imagestream $Name --ignore-not-found 2>&1 | Out-Null
    $global:LASTEXITCODE = 0
}

# 4. Build and deployment
Invoke-Oc apply -k (New-Manifests)

# 5. Build the image from the staged source. With the integrated registry the image trigger on the Deployment rolls it
# out; with Docker Hub there is no trigger, so roll it out once the build has pushed the image
if (-not $SkipBuild) {
    # The CMF NuGet feed credentials for the restore in the build stage (see the Dockerfile): from the parameters, the
    # environment, the user NuGet.Config, or a prompt. Never printed; deleted from the staged source after the build
    $nugetPassword = $env:NUGET_CMF_PASSWORD
    if (-not $NuGetUser) { $NuGetUser = $env:NUGET_CMF_USERNAME }
    if (-not $NuGetUser -or -not $nugetPassword) {
        $userConfig = if ($IsWindows -or $env:OS -eq "Windows_NT") { Join-Path $env:APPDATA "NuGet/NuGet.Config" } else { Join-Path $HOME ".nuget/NuGet/NuGet.Config" }
        if (Test-Path $userConfig) {
            $credentials = ([xml](Get-Content $userConfig -Raw)).configuration.packageSourceCredentials.CMF.add
            if ($credentials) {
                if (-not $NuGetUser) { $NuGetUser = ($credentials | Where-Object key -eq "Username").value }
                if (-not $nugetPassword) { $nugetPassword = ($credentials | Where-Object key -eq "ClearTextPassword").value }
            }
        }
    }
    if (-not $NuGetUser) { $NuGetUser = Read-Host "User for the CMF NuGet feed" }
    if (-not $nugetPassword) {
        $secure = Read-Host "Password or token for the CMF NuGet feed ($NuGetUser)" -AsSecureString
        $nugetPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
    }
    if (-not $NuGetUser -or -not $nugetPassword) { throw "No credentials for the CMF NuGet feed" }

    $nugetCredentials = Join-Path $stage ".nuget-cmf-credentials"
    try {
        [IO.File]::WriteAllText($nugetCredentials, "Username=$NuGetUser;Password=$nugetPassword", (New-Object Text.UTF8Encoding($false)))
        & oc start-build $Name --from-dir=$stage --follow
        $buildExitCode = $LASTEXITCODE
    } finally {
        Remove-Item $nugetCredentials -Force -ErrorAction SilentlyContinue
    }
    if ($buildExitCode -ne 0) {
        if (-not $external) {
            Write-Host "If the build says the integrated container image registry is not configured, run again with -DockerHubUser <user>." -ForegroundColor Yellow
        }
        throw "The build failed (oc exit code $buildExitCode)"
    }
    if ($external) {
        Invoke-Oc rollout restart deployment/$Name
    }
}

Write-Host ""
Write-Host "Done. Follow the simulator with:  oc logs -f deployment/$Name"
