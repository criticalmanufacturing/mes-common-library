# Cmf.Custom.MESProject.IoT.CodeCSharp

Angular workspace for the Connect IoT Controller Engine **C# Roslyn Code** task: a task that lets
you write C# directly in the flow designer and run it, without creating and compiling a full
custom C# task project in Visual Studio.

It contains a single publishable library, `controller-engine-codecsharp-tasks`
(npm package `@criticalmanufacturing/connect-iot-controller-engine-codecsharp-tasks`), built with
Angular CLI 17.

## Structure

```
Cmf.Custom.MESProject.IoT.CodeCSharp/
├── Manager/                                    Connect IoT Automation Manager, used to run/debug
│                                                the controller locally (scaffolded by cmf-dev,
│                                                not part of the library)
├── dist/                                       npm-packagable build output (ng-packagr)
└── projects/controller-engine-codecsharp-tasks/
    ├── src/lib/tasks/c-sharp-roslyn-code/       the task: designer, settings UI, task/module
    │   └── roslyn/                              browser Roslyn compiler wrapper + node execution manager
    ├── roslyn-compiler/                         RoslynCode.Compiler — Blazor WebAssembly project
    │                                             that compiles C# in the browser via Roslyn
    ├── roslyn-host/
    │   ├── RoslynCode.Contracts/                interfaces/contracts shared by compiler and host
    │   ├── RoslynCode.Host/                     console app that executes a compiled assembly
    │   │                                        on the Controller Engine host machine
    │   └── RoslynCode.Sample/                   sample/manual test harness
    └── test/unit/                               mocha unit tests
```

## How it works

Compilation and execution are split across two stages:

1. **Design time (browser).** The task's settings component loads the bundled
   `RoslynCode.Compiler` Blazor WebAssembly bundle and compiles the pasted C# in the browser,
   against a fixed .NET 8 reference catalog plus the `RoslynCode.Contracts` assembly. Arbitrary
   NuGet resolution is **not** available at this stage — any reference needed by user code must be
   added to the compiler's catalog when the package itself is built. Both the editable source
   (`csCodeBase64`) and the resulting compiled assembly (`roslynAssemblyBase64`) are persisted in
   the task settings, together with a `roslynCatalogVersion` used to detect a stale compile.
2. **Runtime (Controller Engine host).** `RoslynExecutionManagerHandler` writes the persisted
   assembly to a local cache and launches the bundled `RoslynCode.Host` through `dotnet`, passing
   the task inputs as JSON over stdin and reading the result back over stdout. This only requires
   the **.NET 8 runtime** on the host — it does not invoke `dotnet publish`, NuGet restore, the
   SDK, or `wasm-tools`, since compilation already happened in the browser at design time.

## Prerequisites

- Node.js / npm, and the Angular CLI version pinned in `package.json`.
- .NET 8 SDK — required to build `RoslynCode.Compiler` and `RoslynCode.Host` (`dotnet publish`).
  Only the .NET 8 **runtime** is required on the Controller Engine host machine that will actually
  execute compiled tasks.
- This is an npm workspace (`"workspaces": ["./projects/*"]`); run npm commands from the workspace
  root so the library's dependencies are hoisted/linked correctly.

## Build

```bash
npm install
npm run build
```

`npm run build` runs `build -ws`, which for `controller-engine-codecsharp-tasks` chains:

- `build:roslyn-host` — `dotnet publish` for `RoslynCode.Host` into `roslyn-host/publish`, plus a
  `dotnet build` of `RoslynCode.Sample`.
- `build:roslyn-compiler` — `dotnet publish` for `RoslynCode.Compiler` into
  `roslyn-compiler/publish/wwwroot`.
- `build:designer` — `ng build` (development configuration) for the Angular library.
- `build:runtime` — `tsc` against `tsconfig.lib.runtime.json`.
- `build:tests` — `tsc` for the mocha unit tests.

The final npm package (`dist/controller-engine-codecsharp-tasks`) embeds the published
`roslyn-host` and `roslyn-compiler/publish/wwwroot` outputs as package assets — see
`ng-package.json`.

Other useful scripts (run inside `projects/controller-engine-codecsharp-tasks`, or via
`npm run <script> -w controller-engine-codecsharp-tasks` from the root):

- `npm run watch` — incremental `ng build --watch` for the designer during development.
- `npm run build:roslyn-compiler` / `npm run build:roslyn-host` — rebuild just one native piece
  after changing C# code, without rerunning the full Angular build.

## Testing

```bash
npm run test          # pretest rebuilds, then runs mocha against test/**/*.test.js
npm run vs:buildAndTest # CI-friendly: build + mocha with nyc coverage and a JUnit report
```

## Publishing / consuming the library

After `npm run build`, publish `dist/controller-engine-codecsharp-tasks` the same way as any other
Connect IoT controller engine task package, and register the `c-sharp-roslyn-code` task module in
the consuming app as usual.

**Important — extra Angular asset required.** The in-browser Roslyn compiler
(`RoslynCode.Compiler`) is a Blazor WebAssembly bundle loaded by the designer at runtime via HTTP,
not bundled by webpack/esbuild through an import. Any Angular app that hosts this task's designer
— e.g. `Cmf.Custom.MESProject.HTML` — must copy those static files into its own build output by
adding the following entry to the `assets` array of its `angular.json`
(`projects.<app>.architect.build.options.assets`):

```json
{
  "glob": "**/*",
  "input": "node_modules/@criticalmanufacturing/connect-iot-controller-engine-codecsharp-tasks/roslyn-compiler",
  "output": "roslyn-compiler"
}
```

Without this entry, the `c-sharp-roslyn-code` designer fails to load the Roslyn compiler bundle
(404s under `/roslyn-compiler/...`) and C# code cannot be compiled in the browser.

> This entry has already been added to `Cmf.Custom.MESProject.HTML/angular.json` in this
> repository. It's called out here so it isn't accidentally reverted, and as a reminder for any
> other app that consumes this package.

## Known limitations

- **No arbitrary NuGet resolution.** References available to user C# code are limited to what's
  embedded in the compiler's reference catalog at package-build time (see the `EmbeddedResource`
  items in `roslyn-compiler/RoslynCode.Compiler.csproj`).
- **No `lbos` equivalent.** Unlike the Python task, there is no dynamic MES business-object bridge
  exposed to user code — it would require a generic dynamic-invocation bridge that hasn't been
  designed/validated for this task.
- A `roslynCatalogVersion` mismatch (e.g. after upgrading the package) invalidates a previously
  compiled task — it must be reopened and saved in the designer to recompile against the new
  catalog.

## Further help

To get more help on the Angular CLI use `ng help` or check the
[Angular CLI Overview and Command Reference](https://angular.io/cli).
