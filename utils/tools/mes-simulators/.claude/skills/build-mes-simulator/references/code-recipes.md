# Code recipes

Copy these patterns. They come from MESSimulator (`Tools/MESSimulator`); paths below are relative to the
project folder. When you build from scratch, create the files with these names. When the code exists,
read the real file before you edit it: the recipes show the shape, and the code is the source of truth.

## Recipe 0: bootstrap the project

1. Create a .NET 8 console app and a unit test project (xUnit):

   ```powershell
   dotnet new console -n <Name> -f net8.0
   dotnet new xunit -n <Name>.UnitTests -o Tests/<Name>.UnitTests -f net8.0
   dotnet add Tests/<Name>.UnitTests reference <Name>.csproj
   ```

2. Packages: `Microsoft.Extensions.Hosting`, `Polly.Core`, `Newtonsoft.Json`. The MES client
   `Cmf.LightBusinessObjects` (and its dependencies `Cmf.LoadBalancing`, `Cmf.MessageBus.Client`, …) is
   referenced as a package from the CMF NuGet feed in the repo's `nuget.config` (it needs credentials, see the
   README, "Building"); never commit the DLLs. Copy the references from an existing simulator in the workspace if there is one. If you
   can't get the DLLs, stop and ask the user.

3. `appsettings.json` (the user fills in the token; **never print it**):

   ```json
   {
     "ClientConfiguration": {
       "Connection": { "EnvironmentAddress": "https://<mes-host>" },
       "Authentication": { "ClientId": "MES", "SecurityAccessToken": "<personal access token>" }
     },
     "Simulation": { "Speed": 100 },
     "Mes": { "RetryMaxAttempts": 5, "RetryDelay": "00:00:01" }
   }
   ```

4. Configure the client. The configuration binder can't build `ClientConfiguration` (its properties are
   abstract), so read the raw values and use the library's factory:

   ```csharp
   var section = configuration.GetSection("ClientConfiguration");
   ClientConfigurationProvider.ConfigurationFactory = () => ClientConfiguration.ForPersonalAccessToken(
       section["Connection:EnvironmentAddress"]
           ?? throw new InvalidOperationException("ClientConfiguration: Connection:EnvironmentAddress is required."),
       section["Authentication:ClientId"]
           ?? throw new InvalidOperationException("ClientConfiguration: Authentication:ClientId is required."),
       section["Authentication:SecurityAccessToken"]
           ?? throw new InvalidOperationException("ClientConfiguration: Authentication:SecurityAccessToken is required."));
   ```

5. Load `simulationConfiguration.json` as an extra configuration file, map `--speed` to `Simulation:Speed`, register every
   service in one `AddSimulator(...)` extension method (the tests reuse it), and run a `BackgroundService`.

6. **First live check:** one call that only reads, e.g. get the current user or get the product by name.
   If it times out, the MES or VPN is unreachable: tell the user; it is not a code problem.

## Recipe 1: configure a new step (no code)

Use this when the step only needs dispatch, track-in, process time, track-out and move-next, or when an
existing action fits.

1. Get the exact MES step name: the last segment of the lot's FlowPath, without the `:N` suffix. Example:
   `Y31_CSP_3L:A:1/PHOTO FUSE:A:1/COAT:3` → `COAT`.
2. Add an entry to `simulationConfiguration.json` → `Line:Steps`:

   ```json
   "METAL WET ETCH": {
     "MinSeconds": 600,
     "MaxSeconds": 900,
     "MinSecondsPerWafer": 60,
     "MaxSecondsPerWafer": 120,
     "Actions": [ "feeders" ]
   }
   ```

3. Pick the options from this table. Other step options: `Resources` (restricts the MES's resource list),
   `LineStepMinSeconds`/`LineStepMaxSeconds` (`lineFlow` chamber times), `ShipTo` (destination facility),
   `Receive: false` (ship and leave the lot in transit: the end of the line), `ClosesProductionOrder`.

   | The step… | Add |
   |---|---|
   | needs consumables on feeders (BOM) | `"Actions": ["feeders"]` |
   | needs durables (reticle, blade) | `"Actions": ["durables"]` |
   | holds one lot at a time (stepper) | `"SingleMaterial": true` |
   | tracks the wafers onto a chamber | `"Actions": ["subMaterialTracking"]` |
   | splits the lot | `"Actions": ["splitTrackOut"]` |
   | is a batch step | `"Batch": { "MinQuantity": 70, "MaxQuantity": 100, "CheckIntervalSeconds": 600 }` |
   | has no track-in | `"PassThrough": true` |
   | is followed by a step in another facility | `"Actions": ["ship"]`, or `PassThrough` + `Ship` |
   | is conditional in the MES | nothing: the MES skips it. Add a `Condition` only if the MES doesn't. |

4. Process times are **simulated seconds**: real-fab values, scaled by `--speed`. Examples from a
   wafer-level-packaging front end (per lot + per wafer): spin coat 3–7 min + 1.5–2.5 min/wafer; stepper
   expose 10–15 min + 1–1.5 min/wafer; back grinding 10–15 min + 4–6 min/wafer; sampled metrology
   10–20 min; oven batch cure 1.5–4.5 h.
5. Run the unit tests. The service-wiring test loads the real `simulationConfiguration.json` and must fail on an unknown action
   key, two `TrackOut` actions on one step, or a pass-through step that also has actions or resources.

## Recipe 2: add a new step action

Use this only when no existing action fits. One class per action, in `Steps/Actions/`.

```csharp
using Microsoft.Extensions.Logging;
using MESSimulator.Mes;

namespace MESSimulator.Steps.Actions
{
    /// <summary>
    /// <Step name>: <what the action does, in one sentence>.
    /// </summary>
    public sealed class MyAction(IMesGateway mes, IMaterialTracker tracker, ILogger<MyAction> logger) : IStepAction
    {
        public const string ActionKey = "myAction";            // the key used in simulationConfiguration.json
        public const StepHook ActionHook = StepHook.AfterTrackIn;

        public string Key => ActionKey;

        public StepHook Hook => ActionHook;

        public Task ExecuteAsync(StepContext context)
        {
            // Always work on the latest MES version of the lot
            var lot = tracker.Reload(context.Lot);

            logger.LogDebug($"Doing X for Lot '{lot.Name}' on '{context.ResourceName}'");

            // Call the MES only through the gateways (mes.Materials, mes.Resources, ...)
            // context.Lot = mes.Materials.SomeCall(lot, ...);

            logger.LogInformation($"Did X for Lot '{lot.Name}'");
            return Task.CompletedTask;
        }
    }
}
```

Rules:

- **Pick the hook** from the SKILL.md hook table. A `TrackOut` action replaces the standard track-out and
  move-next, so it must do both itself. Put the lots that leave the step in `context.OutputLots` (empty
  means `context.Lot`).
- **Replace `context.Lot`** with the lot the MES returns when your call changes it. The next actions and
  the executor use it.
- **Resource can't run this lot** (in a `BeforeTrackIn` action): throw
  `new ResourceUnsuitableException("why")`. The executor then tries the next resource. Use
  `temporary: true` when it may work later (for example a shared feeder in use); the step then waits and
  retries.
- **Resumable:** a lot can be stopped mid-step and resumed by `diag`. Check the MES state first and skip
  what is already done (`pack` skips a lot that is already packed; `lineFlow` resumes).
- **No `Thread.Sleep`, no `new Random()`.** Use `SimulationClock` for waits (`Scale`, `PollInterval`,
  `PollCount`) and `IRandomSource` for random values, so runs scale with `--speed` and `Chaos:Seed` makes
  them repeatable.

Register it in `SimulatorServices.cs`, next to the other actions:

```csharp
services.AddStepAction<MyAction>(MyAction.ActionKey, MyAction.ActionHook);
```

Then add its key to the step in `simulationConfiguration.json`, add a row to the actions table in your build journal (README), and run the
unit tests.

## Recipe 3: add a step condition

Only when the MES does not decide the step itself. Example: `InspCdRequiredCondition` in
`Steps/Actions/MaterialActions.cs`.

```csharp
public sealed class MyCondition(IMesGateway mes) : IStepCondition
{
    public const string ConditionKey = "myCondition";

    public string Key => ConditionKey;

    public bool ShouldRun(Material lot)
    {
        // Read the MES data the rule depends on; return false to skip the step (the lot is moved next)
        return true;
    }
}
```

Register it with `services.AddStepCondition<MyCondition>(MyCondition.ConditionKey);` and set
`"Condition": "myCondition"` on the step.

**Ask the user for the rule.** A wrong guess (for example the INSPCD attribute rule) runs or skips the
step for the wrong lots without any error.

## Recipe 4: add a new MES call

1. **Find the service** (SKILL.md section 7.5): copy it from another simulator in `Tools/`, or find the
   input type with reflection in PowerShell:

   ```powershell
   $a=[Reflection.Assembly]::LoadFrom("bin\Debug\net8.0\Cmf.LightBusinessObjects.dll")
   try { $t=$a.GetTypes() } catch { $t=$_.Exception.InnerException.Types | ? { $_ } }
   $t | ? { $_.Name -match 'ProductionOrders?Input$' } | % FullName
   ```

   Then list the input's properties with `GetProperties()`. Each `XxxInput` has an `XxxSync()` method
   that calls the service.
2. **Try it in a probe** on one simulator-created object first.
3. **Add it to the right gateway** in `Mes/` (Materials, Resources, Batches, Setup, Labor or MasterData):
   a method on the interface and on the class. Every call goes through `IMesCall.Run`:

   ```csharp
   public BatchCollection Release(BatchCollection batches) =>
       mes.Run("ReleaseBatches", () => new ReleaseBatchesInput()
       {
           Batches = batches
       }.ReleaseBatchesSync(), $"{batches.Count} batch(es)").Batches;
   ```

   - The first argument is the MES service name. It is used in logs and in `--timings`.
   - The last argument names what the call acts on, for the logs.
   - `MesCall` retries transient errors ("has changed since last viewed", deadlocks). Do not add your own
     retries for those.
4. **Never call LBO `*Sync` methods outside `Mes/`.** Actions and the pipeline use the gateways only.

## Recipe 5: add a step option (a new simulationConfiguration.json field)

1. Add the property to `StepOptions` in `Line/LineOptions.cs`.
2. Add it to `StepDefinition` in `Steps/StepDefinition.cs`, with an XML doc comment.
3. Map it in `Line/LineDefinition.cs`, where the other options are copied to the `StepDefinition`.
4. Add a rule to `Line/LineOptionsValidator.cs` if some combinations are invalid.
5. Add a row to the step options table in your build journal (README).
6. Add a unit test in `Tests/MESSimulator.UnitTests/LineTests.cs`.

## Recipe 6: a unit test

Tests use xUnit and hand-written fakes (no mocking library). Reuse the fakes in
`Tests/MESSimulator.UnitTests/StepFakes.cs` and `TestDoubles.cs`. Add a new fake there when needed.

- Test MES-free logic: routing decisions, validation, scheduling, batch planning, random ranges.
- Code that mostly calls the MES (for example `ProductionOrderCompletion`) is validated with a live run
  instead. Say so in your report.
- Run: `dotnet test Tests/MESSimulator.UnitTests`.
