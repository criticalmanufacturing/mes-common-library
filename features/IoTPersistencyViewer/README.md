# IoTPersistencyViewer

A Critical Manufacturing MES Community customization that adds a **"Persistency Viewer"** tab to the `AutomationControllerInstance` entity page, letting users view, browse, and edit the edge-layer key/value persistency data stored on a Connect IoT controller — directly from MES, without opening the controller itself.

This package is **UI-only** (MES-side customization). It talks to the controller over the platform message bus and expects a matching Connect IoT-side subscriber to actually read/write the persisted data — see [Connect IoT requirement](#connect-iot-requirement-required) below. Full write-up: <https://j-roque.com/posts/20260325-ui-persistencyviewer/>.

## Package layout

Deployed as a root package (`cmfpackage.json`, `packageId: Cmf.Community.IoTPersistencyViewer`, MES 11.3.3) with a single mandatory dependency:

- **`Cmf.Custom.HTML/`** — the Angular workspace (Angular 17.3, `cmf-mes-ui` release-1133) containing the whole feature, as the library `projects/customization-json-persistency/`:
  - `customization-json-persistency.component.ts` — `CustomizationJsonPersistencyComponent`, the tab itself.
  - `json-tree-viewer.component.ts` — collapsible tree rendering of the JSON value.
  - `json-graph-view.component.ts` — node/edge diagram rendering of the JSON value (via `@swimlane/ngx-graph`).
  - `metadata/` — `CustomizationJsonPersistencyMetadataService`/`...Module`, which registers the tab as a view on `AutomationControllerInstance`.
- **`Libs/`** — scaffolded .NET/DEE package (Business, Custom, EntityTypes, External, LBOs, PrivateFix, Tests); currently empty, no server-side code shipped.

## How it works

The tab is registered against entity type `AutomationControllerInstance` (view id `Custom.AutomationControllerInstancePersistencyViewer`, route `persistency-viewer`) and, on load, lets the user pick a persisted key from a dropdown, view its value (as raw JSON, a tree, or a graph), edit it, and save it back — all live, against the running controller.

There's no HTTP/REST call involved: every operation is a **message bus request/response** sent from the browser straight to the controller's message bus subject, using `MessageBusService.sendRequest()` with a 1s timeout.

## ⚠️ Message bus topic

Every request is sent to a topic built from the current controller's entity id:

```
CMF.Cmf.Foundation.BusinessObjects.AutomationControllerInstance.${controllerId}.SENDREQUEST
```

(`${controllerId}` = the `AutomationControllerInstance` id of the entity page currently open — `this._pageBag.context.id`.)

Three request payloads are sent on that topic, distinguished by a `type` field (there's no shared/formal envelope type — each call site inlines its own object literal):

| `type` | `data` | Sent when | Expected reply |
|---|---|---|---|
| `GetPersistencyKeyList` | `"Request"` | Tab loads / entity is refreshed | JSON array of key names |
| `GetPersistencyKeyValue` | the key name (plain string) | User selects a key in the dropdown | JSON value for that key — either the raw value or `{ "reply": <value> }` |
| `SetPersistencyKeyValue` | `JSON.stringify({ key, value })` (double-encoded) | User clicks Save | — |

## Connect IoT requirement (required)

**This package does not ship a Connect IoT task or workflow.** For the tab to work, the target controller must run a workflow that subscribes to its own
`CMF.Cmf.Foundation.BusinessObjects.AutomationControllerInstance.{id}.SENDREQUEST` topic and handles all three request types above — reading/writing the controller's persisted DataStore and replying with the shapes described. Without that Connect IoT-side handler deployed on the controller, every action in the tab will simply time out (1s) with no data returned. See the linked blog post for a worked example of that handler.

## Refresh behavior

The tab hooks the entity page's native reload rather than a custom button: it subscribes to `EntityPageService.epEntityLoaded` (an `EventEmitter` from `cmf-core-business-controls`, fired whenever the entity page reloads — including via the page's own Refresh action) and resets its own state (selected key, value, key list) whenever that fires, then re-fetches the key list.

## Dependencies

- Angular 17.3, `cmf-mes-ui` release-1133 (pulls in `cmf-core`, `cmf-core-controls`, `cmf-core-business-controls`).
- `@swimlane/ngx-graph` (dagre-based layout) for the graph view.
- `CodeEditorModule` for the raw-JSON edit mode.
