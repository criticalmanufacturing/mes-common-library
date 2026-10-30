# ControllerEnginePythonTasks

This library was generated with [Angular CLI](https://github.com/angular/angular-cli) version 17.3.0.

## Code scaffolding

Run `ng generate component component-name --project controller-engine-python-tasks` to generate a new component. You can also use `ng generate directive|pipe|service|class|guard|interface|enum|module --project controller-engine-python-tasks`.
> Note: Don't forget to add `--project controller-engine-python-tasks` or else it will be added to the default project in your `angular.json` file. 

## Build

Run `ng build controller-engine-python-tasks` to build the project. The build artifacts will be stored in the `dist/` directory.

## Publishing

After building your library with `ng build controller-engine-python-tasks`, go to the dist folder `cd dist/controller-engine-python-tasks` and run `npm publish`.

## Running unit tests

Run `ng test controller-engine-python-tasks` to execute the unit tests via [Karma](https://karma-runner.github.io).

## Further help

To get more help on the Angular CLI use `ng help` or go check out the [Angular CLI Overview and Command Reference](https://angular.io/cli) page.

## C# Code task (DotNetJS / Bootsharp)

`csharp-code` is the C# counterpart to the `python-code` task. It is **not** a drop-in
equivalent of Pyodide: Pyodide bundles a ready-to-run Python interpreter, while
[Bootsharp](https://bootsharp.com) (the DotNetJS project) is a build tool — it compiles a fixed
C# project ahead of time (`dotnet publish`, targeting `browser-wasm` with NativeAOT-LLVM) into a
WASM module with generated JS bindings. There is no `runPythonAsync`-style interpreter to hand
raw source text to.

To get the same "write code in the designer, it just runs" UX anyway, `DotNetJSManagerHandler`
scaffolds a runner project around the pasted `Code` class and shells out to the real `dotnet`
CLI, caching the published output on disk (keyed by a hash of the code + NuGet packages) so
unchanged code never recompiles. Concretely, this means:

* **Host prerequisites** (unlike Pyodide, which needs nothing beyond `npm install`): the .NET
  SDK and the `wasm-tools` workload (`dotnet workload install wasm-tools`) must be installed on
  every controller-engine host that runs this task. `DotNetJSManagerHandler.initialize()` checks
  for both and fails/warns with an actionable message if missing.
* **First run is slow, not instant**: the first activation after a code (or NuGet package) change
  pays a real `dotnet publish` cost (tens of seconds, more for larger scripts/more packages) —
  see `compileTimeoutMs`. Cached builds skip compilation entirely.
* **Inputs/outputs are `JsonObject`/`JsonNode`, not a dynamic dictionary**: Bootsharp's automatic
  marshalling has no case for the CLR `System.Object` type — a `Dictionary<string, object>`
  argument silently arrives on the JS side as `{}` for every value (verified empirically). Every
  value that crosses the JS↔C# boundary here is JSON-string-based instead; see the comments in
  `dotnetjs/dotnetjsBootstrap.ts` for the full rationale.
* **No `execute_with_retry`/`execute_with_system_error_retry` equivalent**: passing a live
  callback across the Bootsharp boundary needs its "instance binding" mechanism, which wasn't
  validated for this integration. Write retry loops directly in C# instead.
* **No `lbos` equivalent**: the Python task's `self.lbos` (dynamic MES business object access) is
  out of scope for this task — it would need a generic dynamic-invocation bridge that hasn't been
  designed/validated.
* Installing [Binaryen](https://github.com/WebAssembly/binaryen) (`wasm-opt` on `PATH`) is
  recommended but not required — without it, `dotnet publish` emits a warning and a
  slightly-larger, unoptimized WASM binary.

## C# Roslyn Code task

`c-sharp-roslyn-code` is a separate task under development. Its designer loads the bundled
Roslyn WebAssembly compiler and compiles C# in the browser against a fixed .NET 8 reference
catalog plus `RoslynCode.Contracts`. It persists both the editable source (`csCodeBase64`) and
the resulting assembly (`roslynAssemblyBase64`). Arbitrary NuGet resolution is deliberately not
available: references must be added to the compiler catalog during the package build.

At runtime, the controller launches the bundled `RoslynCode.Host` through `dotnet` to execute
the saved assembly. It requires the .NET 8 runtime, but does not invoke `dotnet publish`, NuGet
restore, the SDK, or `wasm-tools`.
