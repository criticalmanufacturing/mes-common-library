---
name: automation-business-scenario
description: Generate a Critical Manufacturing Automation Business Scenario JSON file from a flowchart description (mermaid, prose, or bullet list). Use whenever the user provides a flowchart/diagram and asks to turn it into a business scenario, Cortex scenario, or `business_scenario_*.json` file. Triggers include phrases like "create a business scenario", "automation business scenario from this flowchart", "turn this mermaid into a scenario", or "generate scenario metadata".
---

# Automation Business Scenario Generator

You convert a flowchart description (mermaid diagram, prose, or a list of nodes/edges) into a valid Critical Manufacturing **Automation Business Scenario** JSON file.

A Business Scenario is metadata that drives the Cortex chatbot UI. It is a linked list of typed steps (`Message`, `Question`, `Script`, `Condition`, `Foreach`, `CallScenario`, `Debug`) with a `start`, optional `finally`, optional `end`, and a `resultType` (`Custom`, `MasterData`, `Script`).

## Workflow

Follow these steps in order. Don't skip clarification — a wrong step type or missing `resultKey` will produce a broken scenario.

### 1. Parse the flowchart
Identify every node and its outgoing edges. For each node capture:
- **Name** (unique within the scenario; PascalCase, matches the node id in mermaid)
- **Type** — infer from the node label or its shape:
  - `"Question: X"` → `Question`
  - `"Script: X"` → `Script`
  - `"Condition: X"` or a node with labeled edges (`|"x == 1"|`) → `Condition`
  - `"Message: X"` → `Message`
  - `"Foreach: X"` or iteration over a list → `Foreach`
  - `"CallScenario: X"` or "call other scenario Y" → `CallScenario`
  - `"Debug"` → `Debug`
- **resultKey** — the parenthesized value under the node label, e.g. `(integrationName)`. Required for `Question`, often used by `Script` and `Foreach`. Omit for `Message`/`Condition` unless explicitly provided.
- **next** — the destination of the single outgoing edge (unlabeled). For `Condition` nodes, the labeled edges become `settings.condition` entries and `next` is the fallback.

Also identify:
- The **start step** — usually pointed to by `StartStep["Start Step"] --> X` or the user labels it explicitly.
- The **finally step** — pointed to by `FinallyStep["Finally Step"] --> X` (optional).
- The **end step** — pointed to by `EndStep["End Step"] --> X` (optional). Required when `resultType` is `MasterData` or `Script` and the scenario must execute something at the end.

### 2. Ask the user for missing scenario-level metadata
If the user has not provided them, ask (concisely, in one message — bundle the questions) for:
- `name` — unique scenario name shown in the chatbot dropdown.
- `description` — short description.
- `scopes` — comma-separated scopes where the scenario is available (e.g. `Entity/AutomationManager`, `AutomationController`). Required.
- `resultType` — `MasterData`, `Script`, or `Custom`. Infer from the end step (a script execution → `Script`; building master data → `MasterData`; nothing → `Custom`).
- `condition` (optional) — JSONata expression like `entityInstance.UniversalState = 0`. Default empty.

Skip questions whose answer is obvious from the flowchart description and state your assumption.

### 3. For each `Question` step, ask for the dataType if not stated
Questions are useless without a `dataType`. Valid values: `String`, `Integer`, `Long`, `Decimal`, `Boolean`, `DateTime`, `Object`, `Password`, `Enum`, `EntityType`, `FindEntity`. Infer from the question wording when obvious (`"Do you wish to..."` → `Boolean`; a selection from a list → `Enum` with the listed values). If a `defaultValue` or `enumValues` are stated, include them.

### 4. For each `Script` step, decide between inline and external
- If the user provided pseudocode/logic, embed it as a `String[]` (one line per array entry) under `settings.script`.
- If the user wants the script in a separate file, use the placeholder `"${script(./scripts/<scenario-folder>/<name>.ts)}"` and tell the user to author that `.ts` file (mirroring the sample layout under `scenarios/scripts/<scenario>/`).
- If the user has not provided the script body at all, leave a `// TODO` comment line in the array and flag it in your final summary.

### 5. Assemble the JSON
Use the template in `reference/template.json`. Strictly follow this top-level shape:

```json
{
  "name": "...",
  "description": "...",
  "scopes": "...",
  "conditionType": "JSONata",
  "condition": "",
  "metadata": {
    "start": "<StartStepName>",
    "finally": "<optional>",
    "end": "<optional>",
    "resultType": "Custom | MasterData | Script",
    "steps": [ /* every step listed once */ ]
  }
}
```

### 6. Validate before returning
Self-check the generated JSON:
- Every `next`, every `settings.condition` value, and every `start`/`finally`/`end` references a step that exists in `steps` (or is `""`).
- Every step name is unique.
- Every `resultKey` is unique across steps.
- `Condition` steps use `settings.condition` (an object of `"<JSONata expr>": "<nextStepName>"`) and a fallback `next` (point at an `Error` step or `""`).
- `Foreach` steps have either `items` or `itemsKey` plus `itemKeyName`; `subSteps` is optional.
- `Question` steps have `message` and `dataType`; `Enum` questions have `settings.settings.enumValues`.
- `Message` steps have a `message` or `messageKey`.
- `Script` steps have `settings.script` as a `String[]` or base64 `String`.

### 7. Write the file
By default write the scenario as `business_scenario_<kebab-name>.json` under a `scenarios/` folder relative to the user's current working directory, unless the user specifies otherwise. If you generate external script placeholders (`${script(...)}`), also create the `scripts/<scenario>/<file>.ts` skeleton.

### 8. Summarise
End with a brief summary: file path written, any TODOs (e.g. unimplemented scripts), and any assumptions you made (e.g. inferred `dataType` for a question).

## Quick step-type reference

| Type | Required settings | Typical resultKey? | Notes |
|---|---|---|---|
| `Message` | `message` *or* `messageKey` | No | Informational only. |
| `Question` | `message`, `dataType` | Yes | Sub-`settings` for `Enum`/`EntityType`/`FindEntity`. |
| `Script` | `script` (`String[]` or base64 `String`) | Optional | Scope: `this.answers`, `this.lboUtilities`, `this.iotUtilities`, `this.masterdataDirector`, `this.workflowBuilder`. |
| `Condition` | `condition` (`{ "<expr>": "<step>" }`) | No | `next` is the fallback when no expression matches. |
| `Foreach` | `itemKeyName` + (`items` or `itemsKey`) | Yes | Each iteration runs an isolated `subSteps` flow with a clean answers object. |
| `CallScenario` | `name` | Optional | Calls another scenario; result lands under `resultKey`. |
| `Debug` | `action` + optional `answers` | No | For testing only — strip before publishing. |

See `reference/step-types.md` for fuller per-type detail and `reference/template.json` for a copy-pasteable skeleton.

## Notes on mermaid input

The mermaid diagrams in scenarios follow a consistent format. Examples from real scenarios:

```
StartStep["Start Step"]:::startClass --> CheckIfUserIsIntegrationUser
CheckIfUserIsIntegrationUser["Script:
CheckIfUserIsIntegrationUser
(selectedUser)"] --> Mode
ModeCondition["Condition:
ModeCondition"] -->
|"selectionMode == 'Manual'"|CommaSeparatedManagers
EndStep["End Step"]:::endClass --> DeployManagers
```

Mapping rules:
- The first line in the `[]` label after the colon is the step type.
- The second line is the step name (same as the node id).
- The third line in parentheses is the `resultKey`.
- An unlabeled `-->` becomes the step's `next`.
- A labeled `-->|"expr"|Target` from a Condition node becomes one entry in `settings.condition`.
- `StartStep --> X` ⇒ `metadata.start = "X"`.
- `FinallyStep --> X` ⇒ `metadata.finally = "X"`.
- `EndStep --> X` ⇒ `metadata.end = "X"`.

## When the user has not yet provided a flowchart

If the user only describes the *intent* of the scenario in prose, draft the flowchart yourself (as a mermaid diagram), show it to them for confirmation, and only then generate the JSON. Do not silently invent steps.
