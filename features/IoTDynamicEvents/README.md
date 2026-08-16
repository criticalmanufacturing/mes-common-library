# IoT Dynamic Events

`Cmf.Community.IoTDynamicEvents` is a Critical Manufacturing MES Community feature that adds custom Connect IoT Controller Engine tasks for registering, subscribing to, and handling dynamic automation events, together with timer support for event-driven workflows.

The feature is delivered as a root package with one installable child package:

| Package | Purpose |
| --- | --- |
| [`IoTDynamicEvents.IoT`](IoTDynamicEvents.IoT/) | Connect IoT package containing the custom dynamic-event and timer task library. |

The root package uses version `11331.0.0` and targets MES `11.3.3`. It has optional dependencies on `Cmf.Environment` and `CriticalManufacturing.DeploymentMetadata`, both at version `11.3.3`.

## Capabilities

- Register dynamic event templates and subscribe to events produced by Connect IoT drivers.
- Subscribe to dynamic event tasks and expose event values to subsequent workflow tasks.
- Combine event registration and subscription in a single task.
- Use sleep, timeout, repeated, and cron-style timers in event workflows.

Install the root package through the Critical Manufacturing package deployment process. See the child package README for task-library, manifest, and build details.