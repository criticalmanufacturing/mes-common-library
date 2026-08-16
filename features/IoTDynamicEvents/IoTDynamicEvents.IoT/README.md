# IoT Dynamic Events IoT Package

`Cmf.Community.IoTDynamicEvents.IoT` is the Connect IoT package for the IoT Dynamic Events feature. It deploys the `@criticalmanufacturing/connect-iot-controller-engine-custom-dynamic-events-tasks` task library so dynamic automation events can be configured directly in Connect IoT workflows.

## Contents

| Content | Location | Purpose |
| --- | --- | --- |
| Task library | [`src/controller-engine-custom-dynamic-events-tasks/`](src/controller-engine-custom-dynamic-events-tasks/) | TypeScript implementations, templates, and unit tests for custom event and timer tasks. |
| Designer metadata | [`ui.xml`](ui.xml) | Connect IoT deployment wizard configuration and UI injection metadata. |
| Workspace configuration | [`package.json`](package.json) | npm workspace and build, test, lint, and package scripts. |

The task library provides these task families:

- `CustomDynamicEventsTask`, which registers and subscribes to event templates in a combined workflow step.
- `CustomDynamicEventTaskRegisterTask`, which registers dynamic events for a driver or resource.
- `CustomDynamicEventTaskSubscribeTask`, which subscribes to dynamic events and emits their occurrence data.
- `CustomDriverSubscribeTask`, which subscribes to driver event notifications.
- `CustomTimerTask`, which supports sleep, timeout, repeated, and cron-based timing behavior.

The task library uses the Connect IoT Controller Engine APIs, CMF LBOs, `node-cron`, and related runtime dependencies. Its unit tests are under the task library's [`test/unit/`](src/controller-engine-custom-dynamic-events-tasks/test/unit/) directory.

## `cmfpackage.json`

This is an installable, non-unique `IoT` package with package version `11331.0.0`. Its manifest:

| Source | Target | Purpose |
| --- | --- | --- |
| `src/*` | `node_modules` | Packs the task-library workspace packages and their package metadata. |
| `ui.xml` | XML injection | Adds the Connect IoT configuration wizard metadata during deployment. |

The package is force-installed and configured to force a rerun after a database restore. The root `Cmf.Community.IoTDynamicEvents` package installs it as its mandatory dependency.

## Build and deployment

Run the npm workspace commands from this directory. The root [`package.json`](package.json) delegates build, test, lint, and packaging commands to the workspace under [`src/`](src/). Deploy this package through the root feature package so the task library is available to the Connect IoT workflow designer and runtime.