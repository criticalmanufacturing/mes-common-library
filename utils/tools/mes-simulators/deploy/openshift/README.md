# Deploying MESSimulator to OpenShift

The image is built **in the cluster** from this folder's source (a binary build), and a single-replica Deployment runs
it. [deployment.yaml](deployment.yaml) is a **template**: everything that depends on the environment (the MES address,
client id, speed, order limit, previous-run cleanup, pod resources, any other setting) is a parameter of
[deploy.ps1](deploy.ps1), which fills it in and refuses to apply it with a value left out. The personal access token is a
Secret; the line is a ConfigMap. In the pod, all of it reaches the simulator as `MESSIM_` variables (see "Configuration"
in the [main README](../../README.md)).

## Deploy

```powershell
oc login --web --server=https://<cluster api url>:6443        # the session expires: log in again when oc says Unauthorized
cd utils/tools/mes-simulators
./deploy/openshift/deploy.ps1 -EnvironmentAddress https://<mes-host> -Project messimulator -TokenFromAppSettings
oc logs -f deployment/messimulator
```

[deploy.ps1](deploy.ps1) stages a clean copy of the source (**without `appsettings.json`**, so the token is never in the
image or the build context, and without the tests), creates the project when it doesn't exist, creates the Secret
`messimulator` and the ConfigMap `messimulator-line`, applies [build.yaml](build.yaml) and
[deployment.yaml](deployment.yaml), and starts the build. The Deployment has an image trigger, so it rolls out when
the build finishes.

- **Token:** `-TokenFromAppSettings` reads it from your local `appsettings.json`; otherwise `-Token`, the
  `MESSIM_TOKEN` variable, or a prompt. It never appears on a command line or in the uploaded source.
- **NuGet feed:** the build restores `Cmf.LightBusinessObjects` from the CMF feed, which needs credentials:
  `-NuGetUser`, else `NUGET_CMF_USERNAME`/`NUGET_CMF_PASSWORD`, else the `CMF` entry of your user `NuGet.Config`
  (see the main README, "Building"), else a prompt. They go into the staged source for the build only (read by the
  Dockerfile's build stage, not in the image) and are deleted after the build.
- **Check what is built:** `./deploy/openshift/deploy.ps1 -StageOnly` stages the source and stops (no cluster needed).
- **Check what is applied:** `-RenderOnly` prints the manifests with the parameters filled in (no cluster needed).
- **Every run re-renders the manifests:** pass the same parameters each time (e.g. keep the command in a small
  per-environment script); a value left out goes back to its default.

## A cluster without an integrated registry (Docker Hub)

The default build pushes the image to the cluster's own registry. A cluster without one rejects the build with
`InvalidOutputReference: Output image could not be resolved` ("the integrated container image registry is not
configured"; `oc describe build messimulator-1` shows it), e.g. a cluster whose pods pull through an external registry
proxy. Push to Docker Hub instead:

```powershell
$env:DOCKERHUB_TOKEN = "<Docker Hub access token, read and write>"     # or leave it out: you are prompted
./deploy/openshift/deploy.ps1 -EnvironmentAddress https://<mes-host> -DockerHubUser <your-docker-hub-user> -TokenFromAppSettings
```

The build still runs in the cluster; it pushes `docker.io/<user>/messimulator:latest` with the access token (not your
password: Docker Hub > Account settings > Personal access tokens), which the script keeps in the Secret
`messimulator-registry` (the build pushes with it, the pods pull with it). The Deployment has no image trigger in this
mode, so the script restarts it once the image is pushed. Rendered without a cluster:
`./deploy/openshift/deploy.ps1 -EnvironmentAddress https://<mes-host> -DockerHubUser <user> -RenderOnly`.

- **Visibility:** a repository created by a push is public on a free account, so anyone could pull the image. It holds the
  application and `simulationConfiguration.json`, never the MES token, but make the repository private in Docker Hub if you prefer (the pull
  secret is already set).
- **Another tag:** `-Tag v2`. Another registry: the same idea with its own server and credentials (not scripted).
- **A first attempt without `-DockerHubUser`** leaves a Deployment whose image can't be pulled, plus an unused ImageStream and
  a failed build. The next run updates the Deployment and removes the ImageStream; the failed build can be deleted with
  `oc delete build messimulator-1`.

## Another simulator: name, line file, MES

`-Name` names the deployment and every object it creates:
- the Deployment, BuildConfig, ImageStream and Secret `<name>`;
- the ConfigMap `<name>-line`;
- the registry Secret `<name>-registry`;
- the pods' `app` label;
- with Docker Hub, the repository `docker.io/<user>/<name>`.

The default is `messimulator`, which is what earlier deployments used. Several simulators, e.g. one per MES, can share a
project. `-LineFile` picks the line (relative to the simulator folder; default `simulationConfiguration.semi.json`), and
`-EnvironmentAddress` the MES:

```powershell
./deploy/openshift/deploy.ps1 -Name indutech-sim -LineFile simulationConfiguration.industrial.json `
    -EnvironmentAddress https://<industrial-mes-host> -TokenFromAppSettings
oc logs -f deployment/indutech-sim
```

- **Name format:** lowercase letters, digits and `-`, at most 40 characters.
- **Line file in the pod:** whatever it is called, it is mounted as `/config/simulationConfiguration.json`.
- **Commands below:** with another name, use `deployment/<name>` instead of `deployment/messimulator`.
- **Same MES:** two simulators on the same MES compete for the same resources (see "Operating it").

## An existing project

Pass its name, or `oc project <name>` first and leave `-Project` out (it defaults to the project you are in):

```powershell
./deploy/openshift/deploy.ps1 -EnvironmentAddress https://<mes-host> -Project my-existing-project -TokenFromAppSettings
```

An existing project is used as it is. You need the `edit` role in it (to create the Secret, ConfigMap, BuildConfig,
ImageStream and Deployment). The script creates objects named `messimulator` and `messimulator-line` and says
beforehand which of them already exist, since applying updates them. Nothing else in the project is touched.

## Settings (deploy.ps1 parameters)

| Parameter | Default | Becomes | Meaning |
|---|---|---|---|
| `-EnvironmentAddress` | **required** | `MESSIM_ClientConfiguration__Connection__EnvironmentAddress` | The MES (an http(s) address) |
| `-ClientId` | `MES` | `MESSIM_ClientConfiguration__Authentication__ClientId` | The MES client id of the token |
| `-Token` / `-TokenFromAppSettings` | prompt | Secret `<name>`, key `access-token` | The MES personal access token |
| `-Speed` | `1` | `MESSIM_Simulation__Speed` | Real-fab cadence; a faster demo: a larger number |
| `-MaxOrders` | `0` | `MESSIM_Line__Order__MaxOrders` | 0: launches orders until stopped |
| `-TerminatePreviousRuns` | off | `MESSIM_Line__Startup__TerminatePreviousRuns=true` and `ConfirmTerminate=false` | Terminate what earlier runs left, each time the pod starts. It terminates **every** simulator order of the MES (those matching `Line:Order:ProductionOrderNameFormat`), so only when this simulator owns it. The pod can't ask, so the switch is the confirmation |
| `-LineFile` | `simulationConfiguration.semi.json` | ConfigMap `<name>-line`, `MESSIM_LINE_FILE` | The line |
| `-CpuRequest` / `-CpuLimit` | `100m` / `500m` | pod resources | |
| `-MemoryRequest` / `-MemoryLimit` | `256Mi` / `512Mi` | pod resources | |
| `-Settings` | none | one `MESSIM_<key>` each | Any other setting, `__` between the levels, e.g. `-Settings @{ "Line__Order__MaxLotsInFlight" = "10"; "Line__Startup__MaxTerminatedOrders" = "200" }` |

Steps can't be set this way (their names contain spaces): change those in the line file.

## Operating it

- **Change the line or a setting:** edit the line file (`-LineFile`, default `simulationConfiguration.semi.json`), run
  `deploy.ps1 -SkipBuild`, then
  `oc rollout restart deployment/messimulator` (the simulator reads its configuration at startup).
  A quick one-off change: `oc set env deployment/messimulator MESSIM_Simulation__Speed=20` (the next `deploy.ps1` run
  puts back what its parameters say).
- **New code:** run `deploy.ps1` again.
- **Expired token** (the pod crash-loops with an authentication error): run `deploy.ps1 -SkipBuild` with the same
  parameters and the new token (from `MESSIM_TOKEN` or the prompt, so it stays off the command line), then restart.
- **Stopping it:** scale to 0 (`oc scale deployment/messimulator --replicas=0`). The pod gets 11 minutes to let the lots in
  progress finish their step. Killing it sooner leaves lots in process in the MES that block their resources; the next start
  with `TerminatePreviousRuns` cleans them up.
- **One simulator per MES:** the Deployment is `Recreate` with 1 replica on purpose. Two simulators on the same MES compete
  for the same resources and clean up each other's lots.

## Needs from the cluster

- The build reaches `mcr.microsoft.com` (base images) and the NuGet feeds in `nuget.config` (nuget.org and the CMF feed).
  Behind a proxy or a mirror, set the proxy on the BuildConfig or point `nuget.config` at the mirror.
- The pods reach the MES address (the same cluster here, so it does).
- It runs as an arbitrary user under the default `restricted` SCC; the image needs no privileges and writes no files.

## Checked

Not run against a cluster yet (the session was not logged in). Checked locally: `oc kustomize` renders the manifests; the
staged source contains no `appsettings.json` and no tests; `dotnet restore` and `dotnet publish MESSimulator.csproj`
(the Dockerfile's commands) on the staged copy produce the app with `simulationConfiguration.json`; and that output starts with environment
variables only, no `appsettings.json`.
