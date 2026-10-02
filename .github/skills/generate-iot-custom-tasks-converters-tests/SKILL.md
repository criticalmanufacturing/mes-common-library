---
name: generate-iot-custom-tasks-converters-tests
description: Generate unit tests for ConnectIoT controller-engine tasks and converters. Use when asked to write tests, add unit tests, or test a task/converter in any controller-engine-* package.
---

Unit tests for `controller-engine-*` tasks follow a strict, consistent pattern across all packages. This skill documents exactly how to write them.

**Test framework**: Mocha + Chai. Tests live in `test/unit/tasks/<task-name>/<task-name>.task.test.ts` or `test/unit/converters/<name>/<name>.converter.test.ts`, mirroring the source layout under `src/lib/`.

---

## Step 1 — Read the task source first

Before writing any test, read the task's `.ts` file (not the module wrapper). You need:

- All `@Task.InputProperty` / input fields and their types, the tasks can also be inferred and only live in the `this` object
- All `@Task.OutputProperty` / `Task.Output<T>` fields and their names
- The `Settings` interface and its fields
- Any injected dependencies (`@DI.Inject`) — each one needs a mock
- The working modes / enums that affect behaviour
- Whether the task has `branches` (control-flow tasks need `EnvironmentSetup` — see below)

For converters, read the `transform()` method signature, its parameters object, and all error conditions.

---

## Step 2 — Choose the right template

### A. Converter test

Use when the source exports a class decorated with `@Converter.Converter()`.

```typescript
import { Converter } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/test";
import { expect } from "chai";

import { MyConverter } from "../../../../src/lib/converters/my-converter/my-converter.converter";

describe("My Converter", () => {
    let converter: Converter.ConverterContainer;

    beforeEach(async () => {
        converter = await EngineTestSuite.createConverter({ class: MyConverter });
    });

    it("should transform X into Y", async () => {
        const result = await converter.execute(inputValue, { paramKey: paramValue });
        expect(result).to.deep.equal(expectedOutput);
    });

    it("should throw when input is invalid", async () => {
        try {
            await converter.execute(badInput, { paramKey: null });
        } catch (error) {
            expect(error).to.be.an.instanceof(Error);
            expect(error.message).to.equal("exact error message from source");
        }
    });
});
```

No `reflect-metadata` import needed for converters. `converter.execute(value, params)` — `params` is the `parameters` object passed to `transform()`.

---

### B. Task test — the two-task factory pattern

The standard pattern for all dataflow tasks. Creates the task-under-test and a `MockTask` wired together; `MockTask.onInit` triggers and `MockTask.onChanges` validates.

```typescript
import "reflect-metadata";
import { Task, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/test";
import { expect } from "chai";

import { MyTaskModule } from "../../../../src/lib/tasks/my-task/my-task.task-module";
import { MyTaskSettings } from "../../../../src/lib/tasks/my-task/my-task.task";

describe("My Task tests", () => {

    const myTaskTestFactory = (
        settings: MyTaskSettings,
        trigger: (outputs: Map<string, Task.Output<any>>) => void,
        validate: (changes: Task.Changes) => void
    ): void => {
        EngineTestSuite.createTasks([
            {
                class: MyTaskModule,  // task under test — id can be "0" or "1", doesn't matter
                id: "0",
                settings
            },
            {
                id: "1",
                class: Task.Task({ name: "mockTask" })(
                    class MockTask implements Task.TaskInstance {
                        [key: string]: any;
                        _outputs: Map<string, Task.Output<any>> = new Map();

                        async onBeforeInit(): Promise<void> {
                            this["activate"] = new Task.Output<boolean>();
                            this._outputs.set("activate", this["activate"]);
                            // Add one Output per additional input the task expects
                        }

                        async onChanges(changes: Task.Changes): Promise<void> {
                            validate(changes);
                        }

                        async onInit(): Promise<void> {
                            trigger(this._outputs);
                        }
                    }
                )
            }
        ], [
            { sourceId: "1", outputName: "activate", targetId: "0", inputName: "activate" },
            { sourceId: "0", outputName: "success",  targetId: "1", inputName: "success"  },
            { sourceId: "0", outputName: "error",    targetId: "1", inputName: "error"    },
        ], undefined /* pass DriverProxyMock instance here if task uses driver */);
    };

    it("should emit success when activated", (done) => {
        myTaskTestFactory(
            { /* settings */ },
            (outputs) => { outputs.get("activate").emit(true); },
            (changes) => {
                expect(changes["success"].currentValue).to.equal(true);
                done();
            }
        );
    });

    it("should emit error when something goes wrong", (done) => {
        myTaskTestFactory(
            { /* bad settings */ },
            (outputs) => { outputs.get("activate").emit(true); },
            (changes) => {
                expect(changes["error"]).to.exist;
                expect(changes["error"].currentValue).to.be.instanceOf(Error);
                done();
            }
        );
    });
});
```

**Key rules:**
- The `done` callback signals async test completion — always required for task tests.
- `validate` runs inside `onChanges` — guard with a flag if the task emits more than once (see Step 5).
- `trigger` runs inside `onInit` — emit input values here.
- Task ID (`"0"` / `"1"`) is arbitrary. What matters is **array order**: `createTasks` initializes tasks in the order they appear in the array. If the task-under-test needs to subscribe to the driver *before* MockTask fires, put it **first** in the array.
- Some tasks use the raw task class instead of the `*Module` wrapper (e.g., `RecipeUploadRequestTask`). Check the source — if there's no `*task-module.ts` file, import the task class directly.

**Settings merging pattern** (when you want overrides on top of defaults):
```typescript
const DEFAULTS: MyTaskSettings = { path: "", flag: "w", attempts: 1 };

const factory = (overrides: Partial<MyTaskSettings>, trigger, validate) => {
    const taskDefinition = {
        class: MyTaskModule,
        id: "0",
        settings: Object.assign({}, DEFAULTS, overrides)
    };
    // ...
};
```

**Extracting MockTask as a named factory** (useful when you need DI inside MockTask):
```typescript
const MockTaskDefinition = (trigger: Function, validate: Function): any => {
    class MockTask implements Task.TaskInstance {
        _outputs: Map<string, Task.Output<any>> = new Map();

        @DI.Inject(TYPES.Dependencies.ExecutionContext)
        private _executionContext: Dependencies.ExecutionContext;

        async onChanges(changes: Task.Changes): Promise<void> {
            validate(changes, this._executionContext);
        }
        async onInit(): Promise<void> { trigger(this._outputs); }
        async onDestroy(): Promise<void> {}
    }
    return MockTask;
};

// Usage:
EngineTestSuite.createTasks([
    taskDefinition,
    { id: "0", class: Task.Task({ name: "mockTask" })(MockTaskDefinition(trigger, validate)) }
], links, undefined, bindingHelper);
```

**`async/await` with Promise wrapper** (alternative to `done`, used when trigger is async):
```typescript
it("should handle the request", async () => {
    await new Promise<void>((resolve, reject) => {
        myTaskTestFactory(
            settings,
            async (outputs) => {
                // async trigger logic
                await waitFor(1000, "should have received", () => messages.length === 1);
                expect(messages[0].content.id).to.equal("expected");
                resolve();
            },
            (changes) => {
                if (!changes["success"]) { return; }
                expect(changes["success"].currentValue).to.equal(true);
            }
        );
    });
});
```

---

### C. Control-flow task test — `EnvironmentSetup` pattern

For tasks that use **branches** (`condition`, `for`, `foreach`): the two-task factory doesn't apply. Use `EnvironmentSetup` from the engine test utilities instead.

```typescript
import * as inversify from "inversify";
import { TYPES, EngineContainerModule } from "@criticalmanufacturing/connect-iot-controller-engine";
import * as chai from "chai";
import { EnvironmentSetup, MockTask } from "@criticalmanufacturing/connect-iot-controller-engine/test";
import { DIBasicContainerModule, DIPackageManagerContainerModule } from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/di.basic";
import { ConditionTask } from "../../../../src/lib/tasks/controlFlow/condition.task";

describe("Condition Task tests", () => {
    let container: inversify.Container;

    before(() => {
        container = new inversify.Container();
        container.bind(TYPES.Values.ControllerId).toConstantValue("AutomationControllerInstance/1234567890123456789");
        container.load(DIBasicContainerModule, EngineContainerModule, DIPackageManagerContainerModule);
    });

    beforeEach(() => { container.snapshot(); });
    afterEach(() => { container.restore(); });

    it("should execute the if branch when condition is true", (done) => {
        new EnvironmentSetup(container)
            .registerTaskInstance("condition", ConditionTask)
            .addTriggerTestTask({
                id: "A", type: { name: "AutoTrigger", package: "", version: "" },
                outputs: [{ name: "$scope.value", displayName: "$scope.value", dataType: "string", value: "Hello" }],
                branches: [{
                    name: "eventHandler1",
                    tasks: [{
                        id: "If", type: { name: "condition", package: "", version: "" },
                        branches: [
                            {
                                name: "if", order: 0,
                                settings: { type: "If", condition: "{{ $scope.value == 'Hello' }}" },
                                tasks: [{ id: "B", type: { name: "Success", package: "", version: "" } }]
                            },
                            {
                                name: "else", order: 1,
                                settings: { type: "Else" },
                                tasks: [{ id: "C", type: { name: "Fail", package: "", version: "" } }]
                            }
                        ]
                    }]
                }]
            })
            .registerMockEventAutoTriggerTask()
            .registerMockTaskInstance("Success", {
                onChanges(changes, task) { done(); }
            })
            .registerMockTaskInstance("Fail", {
                onChanges(changes, task) { chai.should().fail("Should not reach Fail"); }
            })
            .run();
    });

    // To test init errors (invalid task configuration):
    it("should detect invalid configuration", (done) => {
        new EnvironmentSetup(container)
            .registerTaskInstance("condition", ConditionTask)
            .addTriggerTestTask({
                id: "A", type: { name: "AutoTrigger", package: "", version: "" },
                outputs: [],
                branches: [{
                    name: "eventHandler1",
                    tasks: [{ id: "If", type: { name: "condition", package: "", version: "" }, branches: [] }]
                }]
            })
            .registerMockEventAutoTriggerTask("AutoTrigger", (taskId, error) => {
                if (taskId === "If") {
                    chai.expect(error.message).to.equal("Must have at least one branch");
                    done();
                }
            })
            .run();
    });
});
```

`EnvironmentSetup` API summary:
- `.registerTaskInstance(name, TaskClass)` — registers the task-under-test by name
- `.addTriggerTestTask(def)` — declares the workflow topology with branches
- `.registerMockEventAutoTriggerTask(name?, errorCallback?)` — registers the auto-trigger; `errorCallback(taskId, error)` fires when a task's init throws
- `.registerMockTaskInstance(name, { onInit?, onChanges? })` — registers named downstream mock tasks
- `.run()` — starts execution

---

## Step 3 — Mocks

### Built-in mocks

```typescript
import { DriverProxyMock }   from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/driver-proxy.mock";
import { DataStoreMock }     from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/data-store.mock";
import { MessageBusMock }    from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/message-bus.mock";
import { SystemProxyMock }   from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/system-proxy.mock";
import { LoggerMock }        from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/logger.mock";
import { ExecutionContextZoneJS } from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/execution-context.zonejs";
```

### Injecting mocks via the DI container

Pass a fourth callback argument to `createTasks`:

```typescript
EngineTestSuite.createTasks([...], [...], driverMock, (container) => {
    // Bindings already in the engine module → use rebind
    container.rebind<Dependencies.Logger>(TYPES.Dependencies.Logger).toConstantValue(loggerMock);
    container.rebind(TYPES.System.Proxy).toConstantValue(systemProxyMock);
    container.rebind<Dependencies.ExecutionContext>(TYPES.Dependencies.ExecutionContext)
        .to(ExecutionContextZoneJS).inSingletonScope();

    // Bindings from the package's own DI module → unbind first, then bind
    container.unbind(TYPES.System.PersistedDataStore);
    container.bind(TYPES.System.PersistedDataStore).toConstantValue(dataStoreMock);

    // Driver proxy (when also needed as DI binding, not just third arg)
    container.bind(TYPES.System.Driver).toConstantValue(driverMock);

    // Capture container for use in test bodies
    testContainer = container;
});
```

**`rebind` vs `unbind+bind`**: use `rebind` for bindings registered in `EngineContainerModule` (logger, system proxy, execution context); use `unbind+bind` for bindings registered in the package's own container module.

### DriverProxyMock API

```typescript
// Pass as third argument to createTasks for tasks that call driverProxy
driverMock = new DriverProxyMock();

// Subscribe to raw messages (task sends, test validates)
driverMock.subscribeRaw("SubjectName", (message) => {
    messages.push(message);
    return { success: true };
});

// Notify the task of an incoming message
await driverMock.notifyRaw("SubjectName", payload);

// Connect the mock driver (required before notifyRaw in some tests)
await driverMock.connect();

// File operations (file-driver-based tasks)
driverMock.addFile("c:/temp/file.txt", "existing content");
driverMock.getFile("c:/temp/file.txt");  // returns content string

// SECSGEM reply simulation
driverMock.subscribeSecsGemReply("S7F5", (type: string, primary: any): any => {
    if (primary?.type === "S7F5") {
        return { type: "S7F6", item: { type: "L", value: [...] } };
    }
    return {};
});
```

### DataStoreMock API

```typescript
dataStoreMock.set("key", value, System.DataStoreLocation.Temporary);
dataStoreMock.get("key", System.DataStoreLocation.Temporary);  // undefined if not set
dataStoreMock.simulateRestart();  // clears Temporary, keeps Persistent
```

### SystemProxyMock API (MES tasks — subscribe/publish pattern)

```typescript
systemProxyMock = new SystemProxyMock();

// From trigger: publish a message that the task under test subscribed to
await systemProxyMock.publish("RecipeManagement.UploadRecipe", {
    content: { Name: "RecipeABC", Revision: "A" },
    reply: (_data) => {
        // Called when the task replies — assert here
        expect(_data.content).to.deep.equal({ accepted: true });
        resolve();
    }
});
```

### Extended logger mock (when you need to capture log output)

Define locally in the test file, or import from another test that already defines it:

```typescript
import * as inversify from "inversify";
import { Dependencies } from "@criticalmanufacturing/connect-iot-controller-engine";
import { LoggerMock } from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/logger.mock";

@inversify.injectable()
class ExtendedLoggerMock extends LoggerMock {
    public errors: string[]      = [];
    public warnings: string[]    = [];
    public information: string[] = [];
    public debugs: string[]      = [];

    error: Dependencies.LogMethod   = (msg: string) => { this.errors.push(msg);      return null as any; };
    warning: Dependencies.LogMethod = (msg: string) => { this.warnings.push(msg);    return null as any; };
    info: Dependencies.LogMethod    = (msg: string) => { this.information.push(msg); return null as any; };
    debug: Dependencies.LogMethod   = (msg: string) => { this.debugs.push(msg);      return null as any; };
}
```

Rebind with: `container.rebind<Dependencies.Logger>(TYPES.Dependencies.Logger).toConstantValue(loggerMock);`

### Inline `SystemAPI` mock (MES tasks that call `System.SystemAPI`)

```typescript
import { DI, System } from "@criticalmanufacturing/connect-iot-controller-engine";

container.unbind(TYPES.System.API);
container.bind(TYPES.System.API).toConstantValue(new (DI.Injectable()(
    class MockSystemAPI implements System.SystemAPI {
        addEventHandler(_handler: System.SystemAPIEventHandler): void { throw new Error("Not implemented"); }
        triggerActionGroupEvent(_actionGroup: any, _data?: any): Promise<any> { throw new Error("Not implemented"); }
        async call(input: System.LBOS.Cmf.Foundation.BusinessOrchestration.BaseInput): Promise<any> {
            return callHandler(input);  // callHandler is a test-level variable you set per-test
        }
    }
)));
```

---

## Step 4 — Wiring dynamic inputs

When the task has a variable number of inputs (e.g., `StoreTask._inputs[]`), generate outputs in `onBeforeInit` from the settings array:

```typescript
async onBeforeInit(): Promise<void> {
    settings.inputs.forEach((input) => {
        this[`out_${input.name}`] = new Task.Output<any>();
        this._outputs.set(input.name, this[`out_${input.name}`]);
    });
}
```

And generate the links array dynamically:

```typescript
[
    ...settings.inputs.map(input => ({
        sourceId: "1", outputName: `out_${input.name}`,
        targetId: "0", inputName: input.name
    })),
    { sourceId: "0", outputName: "success", targetId: "1", inputName: "success" },
    { sourceId: "0", outputName: "error",   targetId: "1", inputName: "error"   },
]
```

When only some links should be included, build conditionally:

```typescript
const links: any[] = [
    { sourceId: "0", outputName: "activate", targetId: "1", inputName: "activate" },
];
if (includeOutputs) {
    links.push({ sourceId: "0", outputName: "recipeName", targetId: "1", inputName: "recipeName" });
}
```

---

## Step 5 — Async patterns

**Guard against double-done (task emits multiple times):**

```typescript
let done_called = false;
(changes) => {
    if (changes["success"] && !done_called) {
        done_called = true;
        expect(changes["success"].currentValue).to.equal(true);
        done();
    }
}
```

**Guard against spurious changes (validate only when the expected output appears):**

```typescript
(changes) => {
    if (changes["myOutput"] !== undefined) {
        expect(changes["myOutput"].currentValue).to.deep.equal(expected);
        done();
    }
}
```

**Delayed triggers (order of events matters):**

```typescript
(outputs) => {
    outputs.get("value").emit(1);
    setTimeout(() => {
        outputs.get("value").emit(123);
        outputs.get("activate").emit(true);
    }, 500);
}
```

**`waitFor` polling utility** (each package has `test/unit/utilities.ts`):

```typescript
import { sleep, waitFor } from "../../utilities";

// Poll until condition is met, or timeout with error message
await waitFor(1000, "Task should have sent a message", () => messages.length === 1);

// Used inside async triggers:
async (outputs) => {
    outputs.get("success").emit(true);
    await waitFor(1000, "Expected message", () => messages.length === 1);
    expect(messages[0].content.id).to.equal("expected");
    done();
}
```

The `utilities.ts` file lives at `test/unit/utilities.ts` in each package (not imported from the engine). If it doesn't exist in the package you're working in, create it:

```typescript
export async function sleep(ms?: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
}

export async function waitFor(timeout: number, errorMessage: string, callback: any): Promise<void> {
    while (true) {
        if (callback()) { return; }
        if (timeout <= 0) { throw Error(errorMessage); }
        timeout -= 100;
        await sleep(100);
    }
}
```

**Execution context zones** (zone-aware tasks — validates that grouped inputs share the same context):

```typescript
import { ExecutionContextZoneJS } from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/execution-context.zonejs";

// Bind in container callback:
container.rebind<Dependencies.ExecutionContext>(TYPES.Dependencies.ExecutionContext)
    .to(ExecutionContextZoneJS).inSingletonScope();
container.bind<Task.ExecutionMode>(TYPES.Task.ExecutionMode)
    .toConstantValue(Task.ExecutionMode.DataFlow);

// In tests, capture the container and fork zones:
let testContainer: Container;
// (in binding callback): testContainer = container;

const executionContext = testContainer.get<Dependencies.ExecutionContext>(TYPES.Dependencies.ExecutionContext);
const context1 = executionContext.fork({ name: "context1", properties: {} });
context1.run(() => { outputs.get("id").emit("ID_001"); });
await sleep();
```

---

## Step 6 — Test what matters

Cover all meaningful branches. For each task, write tests for:

1. **Happy path** — normal input → expected output
2. **All working modes / enums** — one test per variant; use a loop for exhaustive coverage
3. **Error path** — required input missing, invalid value, or exception thrown
4. **Edge values** — null, undefined, empty string, zero where the task handles them specially
5. **Side effects** — if the task stores to DataStore, subscribes to driver, publishes to message bus, or sends via SECSGEM, assert those effects directly (not just `success`)
6. **Driver interaction** — if the task subscribes before activating, verify the subscription exists; simulate the reply; validate the outputs

---

## Gotchas

- **`reflect-metadata` import**: required at the top of every **task** test. Not needed for converter tests.
- **Module vs raw task class**: prefer the `*Module` class (`@Task.TaskModule` wrapper). If no `*task-module.ts` exists in source, use the task class directly.
- **Array order = init order**: `createTasks` initializes tasks in array order. If the task-under-test must subscribe to the driver before MockTask fires, put it first.
- **Task ID is just a string**: the `"0"` / `"1"` convention is common but not enforced. What matters is that the `id` values match the `sourceId`/`targetId` in the links array.
- **`TaskBase` shortcut**: MockTask can `extends TaskBase` instead of `implements Task.TaskInstance`, which provides empty default `onDestroy` etc.
- **`rebind` vs `unbind+bind`**: `rebind` for engine-module bindings; `unbind+bind` for package-specific bindings.
- **Driver as 3rd arg vs DI binding**: some tasks access the driver via the 3rd `createTasks` argument; others expect it bound as `TYPES.System.Driver`. Check the task source — if it `@DI.Inject(TYPES.System.Driver)`, also bind it in the container callback.
- **SECSGEM reply timing**: `subscribeSecsGemReply` must be set up before emitting `activate`, because the task initiates the SECS message in `onChanges` synchronously.
- **Mocha `--retries 3`**: CI runs with 3 retries. Don't rely on exact wall-clock timing.
- **`chai.should().fail()`**: requires `chai.should()` to be called at least once before use (it installs the `.should` property on `Object.prototype`). If you see `is not a function`, call `chai.should()` at the top of the test or in `before`.

---

## Import paths cheatsheet

From inside `test/unit/tasks/<name>/`:
```typescript
import "reflect-metadata";
import { Task, System, TYPES, Dependencies, DI, TaskBase, Container } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/test";
import { DriverProxyMock }        from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/driver-proxy.mock";
import { DataStoreMock }          from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/data-store.mock";
import { MessageBusMock }         from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/message-bus.mock";
import { SystemProxyMock }        from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/system-proxy.mock";
import { LoggerMock }             from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/logger.mock";
import { ExecutionContextZoneJS } from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/execution-context.zonejs";
import { expect } from "chai";

// Task under test (prefer Module wrapper if it exists)
import { MyTaskModule }   from "../../../../src/lib/tasks/my-task/my-task.task-module";
import { MyTaskSettings } from "../../../../src/lib/tasks/my-task/my-task.task";

// Local test utilities (relative from test file location)
import { sleep, waitFor } from "../../utilities";
```

For `EnvironmentSetup` (control-flow tasks):
```typescript
import * as inversify from "inversify";
import * as chai from "chai";
import { TYPES, EngineContainerModule } from "@criticalmanufacturing/connect-iot-controller-engine";
import { EnvironmentSetup, MockTask } from "@criticalmanufacturing/connect-iot-controller-engine/test";
import { DIBasicContainerModule, DIPackageManagerContainerModule } from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/di.basic";
import { MyControlFlowTask } from "../../../../src/lib/tasks/my-task/my-task.task";
```

From inside `test/unit/converters/<name>/`:
```typescript
import { Converter } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/test";
import { expect } from "chai";
import { MyConverter } from "../../../../src/lib/converters/my-converter/my-converter.converter";
```

---

## Real examples to read

| Scenario | File |
|---|---|
| Simple converter | `controller-engine-core-tasks/test/unit/converters/split-string/split-string.converter.test.ts` |
| Task with DataStore mock | `controller-engine-core-tasks/test/unit/tasks/store/store.task.test.ts` |
| Task with Logger mock | `controller-engine-core-tasks/test/unit/tasks/log-message/log-message.task.test.ts` |
| Task with DriverProxy (3rd arg) | `controller-engine-core-tasks/test/unit/tasks/driver-subscribe/driver-subscribe.task.test.ts` |
| Task with DriverProxy (DI bind) + file operations | `controller-engine-filedrivers-tasks/test/unit/tasks/create-file/create-file.task.test.ts` |
| SECSGEM reply simulation | `controller-engine-secsgem-tasks/test/unit/tasks/get-recipe-body/get-recipe-body.task.test.ts` |
| SystemProxyMock + async Promise pattern | `controller-engine-mes-tasks/test/unit/tasks/recipeUpload/recipeUploadRequest.task.test.ts` |
| Zone-aware + waitFor + container capture | `controller-engine-opcua-tasks/test/unit/tasks/command-result/command-result.task.test.ts` |
| EnvironmentSetup (control-flow branches) | `controller-engine-core-tasks/test/unit/tasks/controlFlow/condition.task.test.ts` |
