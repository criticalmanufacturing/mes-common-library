# Step Types — Detailed Reference

Each step has `name`, `type`, optional `resultKey`, `next`, and a type-specific `settings` object. Names must be unique within the scenario.

---

## Message

Display a message; no interaction needed.

| Field | Required | Notes |
|---|---|---|
| `message` | one of `message`/`messageKey` | String to show. |
| `messageKey` | one of `message`/`messageKey` | Key in `answers` to read the message from. |
| `messageType` | No | `String` (default) or `Entities` for rendering structured objects. |

```json
{
  "name": "MessageEnd",
  "type": "Message",
  "settings": { "message": "Reload the tab to see the changes." },
  "next": ""
}
```

---

## Question

Ask the user for input.

| Field | Required | Notes |
|---|---|---|
| `message` | Yes | The prompt. |
| `dataType` | Yes | `String`, `Integer`, `Long`, `Decimal`, `Boolean`, `DateTime`, `Object`, `Password`, `Enum`, `EntityType`, `FindEntity`. |
| `defaultValue` | No | Pre-filled value. |
| `settings` | Sometimes | Sub-settings for the input control (see below). |

### DataType-specific sub-settings (`settings.settings`)

- **`Enum`**: `enumValues` (array of strings or `{label: value}` object) — OR — `data` (script returning `{Id, Name}[]`).
- **`Integer` / `Decimal`**: `min`, `max`.
- **`EntityType`**: `data` (script or base64 array of entity types) or `query` (script returning a QueryObject); `displayField` (default `Name`).
- **`FindEntity`**: `query` (base64-encoded QueryObject or `${script(./scripts/.../query.ts)}`).

```json
{
  "name": "Mode",
  "type": "Question",
  "resultKey": "selectionMode",
  "settings": {
    "message": "Manual or Interactive?",
    "dataType": "Enum",
    "settings": { "enumValues": ["Manual", "Interactive"] },
    "defaultValue": "Interactive"
  },
  "next": "ModeCondition"
}
```

---

## Script

Execute JavaScript/TypeScript-style code in the scenario scope.

| Field | Required | Notes |
|---|---|---|
| `script` | Yes | `String[]` (one line per entry) or base64-encoded `String`. |

### Available scope inside the script
- `this.answers` — read/write all answers so far.
- `this.stepDefinition` — read-only step definition.
- `this.lboUtilities` — Lightweight Business Objects helpers (e.g. `iotEnabledEntities`, `createMasterdata`, `entityTypeStateModels`, `convertArrayOfStringsToEnum`).
- `this.iotUtilities` — IoT helpers (`allEntityTypesFromController`, `allDriversFromController`, `allEventNamesFromDriver`, `getPropertyByName`, `generateOnEquipmentEventSettings`, etc.).
- `this.masterdataDirector` — `setBuilder`, `buildProtocol`, `buildDriverDefinition`, `buildController`, `getMasterdata`.
- `this.workflowBuilder` — `reset`, `loadDefaultATLs`, `addRootTask`, `addTask`, `getWorkflow`.
- `this.System` / `this.securityService` / `entityInstance` — runtime services and the current scope entity.

```json
{
  "name": "ResolveIoTEnabledEntities",
  "type": "Script",
  "resultKey": "iotEnabledEntities",
  "settings": {
    "script": [
      "(async () => {",
      "  const entities = await this.lboUtilities.iotEnabledEntities();",
      "  if (entities.length === 1) { this.answers.iotEnabledEntity = entities[0]; }",
      "  return entities;",
      "})()"
    ]
  },
  "next": "IoTEntityEnabled"
}
```

External script reference (preferred for non-trivial logic):

```json
{
  "name": "DeployManagers",
  "type": "Script",
  "settings": { "script": "${script(./scripts/mass-deploy/mass_deploy.ts)}" },
  "next": ""
}
```

---

## Condition

Branch to one of several steps based on JSONata expressions over `answers`.

| Field | Required | Notes |
|---|---|---|
| `condition` | Yes | Object `{ "<JSONata expr>": "<nextStepName>" }`. Expressions are evaluated top to bottom; first truthy wins. |

The step's own `next` is the **fallback** when no condition matches — point it at an `Error` step or `""`.

```json
{
  "name": "ModeCondition",
  "type": "Condition",
  "settings": {
    "condition": {
      "selectionMode == 'Manual'": "CommaSeparatedManagers",
      "selectionMode == 'Interactive'": "SelectManagerToDeploy"
    }
  },
  "next": "Error"
}
```

---

## Foreach

Iterate over an array, running an isolated sub-flow per item.

| Field | Required | Notes |
|---|---|---|
| `items` | one of `items`/`itemsKey` | Inline array. |
| `itemsKey` | one of `items`/`itemsKey` | Key in `answers` containing the array. |
| `itemKeyName` | Yes | Name under which the current item is exposed in the sub-flow's `answers`. |
| `subSteps` | No | Array of steps executed per item. Each iteration starts at the first sub-step. |

Each iteration has its own clean `answers` object (only `itemKeyName` is populated). Iteration results are collected into the parent's `resultKey` as an array.

```json
{
  "name": "EachManager",
  "type": "Foreach",
  "resultKey": "deployResults",
  "settings": {
    "itemsKey": "selectedManagers",
    "itemKeyName": "manager",
    "subSteps": [
      {
        "name": "DeployOne",
        "type": "Script",
        "settings": { "script": ["// use this.answers.manager"] },
        "next": ""
      }
    ]
  },
  "next": "Done"
}
```

---

## CallScenario

Execute another scenario in-line; its answers/results land under this step's `resultKey`.

```json
{
  "name": "RunSub",
  "type": "CallScenario",
  "resultKey": "subResult",
  "settings": { "name": "AnotherScenarioName" },
  "next": "AfterSub"
}
```

---

## Debug

Manipulate the `answers` object for testing. Remove before publishing.

| `action` | Effect |
|---|---|
| `None` | No change. |
| `Replace` | Replace existing answers with provided ones. |
| `MergeBefore` | Merge debug answers under existing ones. |
| `MergeAfter` / `Merge` / `Append` | Merge debug answers over existing ones. |
| `Clear` | Wipe all answers. |

```json
{
  "name": "Seed",
  "type": "Debug",
  "settings": {
    "action": "Replace",
    "answers": { "selectionMode": "Manual", "managersToDeployComma": "M1,M2" }
  },
  "next": "ModeCondition"
}
```

---

## End-of-flow behaviour

The execution engine walks `start → next → next ...` until `next` is empty. Then:
1. If `finally` is set, walk it the same way.
2. The engine then injects pre-defined steps based on `resultType`:
   - `MasterData`: confirms with the user and creates a master data package.
   - `Script`: confirms with the user and executes the `end` step's script.
   - `Custom`: no injected steps.
3. The `end` step runs last (typically a `Script` that calls `lboUtilities.createMasterdata(masterdataDirector.getMasterdata())` or applies entity changes).

So: when `resultType` is `MasterData` or `Script`, you almost always need an `end` step that produces the result.
