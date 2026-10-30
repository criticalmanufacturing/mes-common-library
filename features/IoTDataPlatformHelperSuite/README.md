# IoTDataPlatformHelperSuite

A Critical Manufacturing MES Community feature that ships a suite of **Connect IoT Controller Engine** helpers for talking to CM's **Data Platform** and **ML Platform**, plus a general-purpose Python scripting task. It is deployed as a single root package (`cmfpackage.json`, `packageId: Cmf.Community.IoTDataPlatformHelperSuite.IoT`, `packageType: Root`) bundling two installable Connect IoT sub-packages:

| Package | What it provides |
|---|---|
| [`IoTDataPlatformHelperSuite.IoT`](IoTDataPlatformHelperSuite.IoT/) | Purpose-built Connect IoT tasks/converters for the Data Platform: telemetry posting, OData queries, ML predictions, ISA-95 tagging. |
| [`IoTDataPlatformHelperSuite.IoT.Python`](IoTDataPlatformHelperSuite.IoT.Python/) | A generic "Python Code" task that runs arbitrary Python (via Pyodide/WebAssembly) inside a Controller Engine flow. |

Together they give Connect IoT flow designers both higher-level, Data-Platform-aware building blocks and a general-purpose scripting escape hatch.

Both packages are Angular/TypeScript "Task Library" npm workspaces targeted at CM MES/Connect IoT **11.3.1**, and both are `packageType: "IoT"` with a `ui.xml` injection for the deployment wizard.

## Packages

### `IoTDataPlatformHelperSuite.IoT` — Data Platform tasks

npm workspace root, with a single Task Library package at `src/custom-dataplatform-tasks/` (`@criticalmanufacturing/connect-iot-controller-engine-custom-dataplatform-tasks`, display name "Custom Data Platform Tasks").

**Tasks** (`src/custom-dataplatform-tasks/src/tasks/`):

- **`EquipmentEventWithDataplatformTask`** — subscribes to equipment/driver events, resolves the ISA-95 hierarchy (Material/Resource/Area/Facility/Site/Enterprise) of the current instance, and posts the occurrence as telemetry to the Data Platform — either immediately or queued for batched posting through the `BatchPostEventHandler` singleton (`src/context/batchPostEventHandler.ts`), which buffers events per application in the persisted DataStore and flushes them via `PostMultipleIoTEvents` on a timer/size threshold.
- **`PostNumericTelemetryTask`** — posts a single numeric/parameter telemetry value (class, name, unit, value, timestamp, tags), tagged with the resolved ISA-95 hierarchy.
- **`PostMultipleNumericTelemetryTask`** — same, for arrays of parameters/values/timestamps in one call.
- **`QueryODataTask`** — builds and executes an OData query (via `odata-query`) against the MES Data Manager OData endpoint (`{HOSTURL}/datamanager/odata/{folder}/{dataset}`), with dynamic inputs substituted into select/filter/expand/orderBy templates, emitting `value`, `countResponse`, `nextLink`, `rawResponse`.
- **`MlPredictionTask`** — invokes CM's **ML Platform Agent** for real-time predictions. The difference to the default CM MES task is that this one supports edge deployment with connect iot. At runtime it builds a parameter map (`ML_ModelName`, `ML_ModelRevision`, `ML_SysProperties_EventId`, plus each declared input) and POSTs it via `superagent` to `${mlModelPredictionEndpoint}/ml/predict` (default `http://mlplatformagent:8080`), emitting the JSON `result` plus each dynamically-declared output. There's no embedded ML/AI SDK — it's a thin REST client to the `mlplatformagent`/`mlplatformtraining` Kubernetes services described in `EnvironmentConfigs/env.json`.
  - The task's designer editor is driven by scripts under `templates/scripts/ml-prediction/`: `onInit.ts`/`onMlModelChange.ts` reload the picked `MLModel` LBO record and its feature/transformation list to rebuild the task's dynamic inputs (one per model feature) and outputs (shaped by `MLModelType`: binary/multi-class classification, regression, or unsupervised), then call `reloadTree()` to refresh the designer UI; `onBeforeSave.ts` flattens the picked model object down to `mlModelName`/`mlModelRevision` before persisting settings.

**Converters** (`src/custom-dataplatform-tasks/src/converters/`) — small value-transform plugins usable in designer data bindings:

- **`NowConverter`** (`"now"`) — current date/time in a selectable format (epoch ms/s, ISO, UTC, local, date, time, with timezone).
- **`ToTimeConverter`** (`"toTime"`) — converts a duration string (`"2s"`, `"1.5h"`, via the `ms` library) into an absolute future epoch-ms timestamp.
- **`UnixToISOStringConverter`** (`"unixToISOString"`) — Unix ms timestamp → ISO 8601 string.

**Shared utilities** (`src/custom-dataplatform-tasks/src/utilities/`): `SystemCalls.extractISA95`/`postTelemetry`/`postTelemetryBatch` and ISA-95 lookup query builders, used across the telemetry tasks.

### `IoTDataPlatformHelperSuite.IoT.Python` — Python Code task

A single Angular library (`controller-engine-python-tasks`) exposing one task, **"Python Code"**: task authors write Python in a Monaco editor (with custom autocomplete for the sandbox API) following a `class Code` / `async def main(self, inputs, outputs)` convention, which is compiled and executed on the Controller inside a **Pyodide** (Python-in-WebAssembly) sandbox. The sandbox blocks `import js`/`pyodide_js` and instead exposes bridged access to `logger`, `data_store`, `message_bus`, `system`, `utils`, `lbos`, and (optionally) the attached equipment `driver`.

See [IoTDataPlatformHelperSuite.IoT.Python/README.md](IoTDataPlatformHelperSuite.IoT.Python/README.md) for full details.

## Other top-level contents

- **`Libs/`** — a scaffolded CM .NET/DEE package (`Business`, `Custom`, `EntityTypes`, `External`, `LBOs`, `PrivateFix/Business`, `PrivateFix/HTML`, `Tests`). All folders currently contain only a `.gitkeep` — no server-side business-object code has been implemented yet.
- **`EnvironmentConfigs/env.json`** — a CM Environment Manager configuration for deploying this feature (DB, Kafka, Redis, Kubernetes replica/resource/volume settings for services including `iot-ml-task-executor`, `mlplatformagent`, `mlplatformtraining`, `clickhouse`, `connectiot-manager`). Infra config, not application code — but it confirms the real backing services the `MlPredictionTask` and Data Platform calls depend on.
- **`cmfpackage.json`** — the root deployment manifest tying the two IoT sub-packages together as one installable feature, with a (non-mandatory) dependency on `Cmf.Environment` and `CriticalManufacturing.DeploymentMetadata` 11.3.1.
- **`.devcontainer/`, `.config/dotnet-tools.json`, `global.json`, `NuGet.Config`** — standard CM MES repo tooling shared by the whole feature (devcontainer definition, dotnet SDK pin 8.0.301, dotnet tool manifest, NuGet feed config).
- **`repositories.json`** / **`deploymentdir/`** — CI package-repository paths and the (currently empty) build output directory used by the CM deployment framework.
