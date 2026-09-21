---
name: cm-service-unittests
description: Write, add, or update unit tests for CM (Critical Manufacturing) custom services / orchestrations — the classes in a feature's `Business` package that implement an `I<FeatureName>Orchestration` interface and expose service methods with `*Input`/`*Output` DTOs. Use this whenever the user asks to test a custom service, orchestration method, or business-logic service, or mentions an Orchestration class/interface, an `*Input`/`*Output` pair, or a class/file under an `Orchestration`/`Orchestrations` folder — even if they just paste a service method and ask "can you cover this with tests" without saying "orchestration" explicitly. This targets this repository's feature layout - each folder under `features/` is a separate feature, with sibling package folders such as `<FeatureName>.Business`, `<FeatureName>.Data`, and `<FeatureName>.IoT` where applicable. Package and namespace names follow `Cmf.Community.<FeatureName>(.<PackageType>)`, including `Cmf.Community.<FeatureName>.Business`, `Cmf.Community.<FeatureName>.Orchestration`, and `Cmf.Community.<FeatureName>.UnitTests` where those packages exist.
---

# CM Custom Service (Orchestration) Unit Tests

Generates or extends xUnit unit tests for CM custom services — the orchestration classes
that implement business logic behind `I<FeatureName>Orchestration` or `ICommunityOrchestration` interfaces, each
exposing service methods that take an `*Input` DTO and return an `*Output` DTO. This skill
assumes this repository's feature layout: each folder under `features/` is a separate
feature, and package folders such as `<FeatureName>.Business`, `<FeatureName>.Data`, and
`<FeatureName>.IoT` are siblings under that feature. Package and namespace names follow
`Cmf.Community.<FeatureName>(.<PackageType>)`. A feature may contain a shared
`Cmf.Community.<FeatureName>.UnitTests` project, or tests may live in a package-local
equivalent, so always verify the actual local files before writing anything. There is no
single testing standard here — orchestration test coverage tends to be much patchier than
DEE coverage, and a feature may have no orchestration tests at all.

## Workflow

### 1. Find the service and confirm the solution's shape

If given a method or interface name only, first identify the feature folder under `features/`.
Then search that feature's `Business` package for the interface
(`I<FeatureName>Orchestration` or similar, commonly under an `Abstractions/` subfolder) and
its implementing class. Package and namespace names generally use
`Cmf.Community.<FeatureName>.Business` and `Cmf.Community.<FeatureName>.Orchestration`, but
filesystem folders commonly use `<FeatureName>.Business` and `<FeatureName>.Orchestration`.
Before assuming a folder layout, check what's actually there — this varies more than in the
DEE world:

- Many solutions put every service method in **one large class**
  (`<FeatureName>Orchestration.cs` implementing `I<FeatureName>Orchestration`, often hundreds or
  thousands of lines with dozens of methods) rather than one file per method.
- `InputObjects`/`OutputObjects` subfolders hold the `*Input`/`*Output` DTOs referenced by
  each service method's signature; they extend `BaseInput`/`BaseOutput` and are decorated
  with `[DataContract]`/`[DataMember]`.
- A `<FeatureName>OrchestrationStartupModule.cs` registers the orchestration class and its
  utility dependencies for DI — useful for discovering the full set of interfaces the
  orchestration class depends on, but it is not itself a target for testing.

### 2. Locate the test home — and check whether one exists at all

Look within the selected feature for a `UnitTests` package or equivalent, especially
`features/<FeatureName>/<FeatureName>.Business/Cmf.Community.<FeatureName>.UnitTests/`. **Don't assume this
folder exists** — it's common for the feature's shared
`Cmf.Community.<FeatureName>.UnitTests` project to already exist (built up from DEE tests) while having no
orchestration coverage at all yet. If you find no orchestration test folder, check whether
the DEE unit tests already exercise this service indirectly before concluding tests need to
be written from scratch — and consider whether direct service-level tests or DEE-level tests
(or both) best match how that solution already verifies orchestration behavior. When in
doubt, ask the user which they want rather than silently picking one.

Where a dedicated orchestration test folder exists, search it for an existing test of this
service method by class name (one test class per method is the norm: `<MethodName>UnitTests`
or `<MethodName>Tests`). If found, read it fully and add new test methods to it rather than
duplicating the file.

If the feature's shared `Cmf.Community.<FeatureName>.UnitTests` project exists but has never
referenced the orchestration project, add a `ProjectReference` to the local
`Cmf.Community.<FeatureName>.Orchestration.csproj`
alongside its existing references (e.g. to the DEE `Actions` project) before writing the
first orchestration test — don't create a second, separate test project for orchestration
coverage. Only scaffold a brand-new standalone test project (`net8.0`, `IsTestProject`,
xunit + Moq + FluentAssertions + `Cmf.Foundation.BaseTestsUtils`, referencing the feature's
orchestration project) in the rare case where the feature has no unit test
project of any kind yet — mirror the csproj shape described in `cm-dee-unittests` rather
than inventing a new structure.

### 3. Maintain one orchestration per test file

Every concrete orchestration source has exactly one dedicated test file named
`<OrchestrationSourceFileName>Tests.cs`. For example,
`Cmf.Community.<FeatureName>.Orchestration/<FeatureName>Orchestration.cs` maps to
`<FeatureName>OrchestrationTests.cs` in the test project's `Orchestration(s)` folder. This maps
source classes, not individual DTO files or methods: keep related method tests in the single
matching orchestration test file.

Exclude non-implementation files from this rule: `<FeatureName>OrchestrationStartupModule.cs`,
the `InputObjects`/`OutputObjects`/`Abstractions` folders (DTOs and interfaces, not
orchestration logic), assembly metadata, and generated `bin/`/`obj/` sources. Avoid generic
shared test files that cover multiple orchestration classes. Each matching file needs
meaningful behavior coverage; where a method genuinely needs a live MES request context to
run (rather than just an injected dependency you can mock), test the isolated
guards/branches you can reach and document the runtime dependency instead of fabricating
framework state.

### 4. Read the service method thoroughly

Before writing anything, work out:
- The full constructor dependency list of the orchestration class (every interface it
  takes in) — you need a mock (or `null`, for genuinely unused-by-this-method
  dependencies) for each one when constructing it.
- What the specific method under test reads off its `*Input` DTO, what it calls on each
  dependency, what conditions it checks (holds, statuses, attribute values, existence
  checks), and what it returns in the `*Output` DTO or throws (commonly wrapped in
  `CmfBaseException`).
- Whether the method delegates to other injected "utilities" interfaces (e.g.
  `I<FeatureName>GenericUtils`, `I<FeatureName>QueryUtils`, `I<FeatureName>MaterialUtils` - usually found in the `<FeatureName>.Business/Cmf.Community.<FeatureName>.Common` package.

### 5. Construct the class under test

Default to **direct construction** — `new <FeatureName>Orchestration(dep1, dep2, ..., null, null...)`, passing mocks for the dependencies the test actually exercises and `null` for
unused constructor parameters — unless the sibling tests for this class already use
something else. This is the simplest option and needs no supporting infrastructure.

Only if the solution already has a fluent MockBuilder pattern for its orchestration classes
(look for a `MockBuilders` folder under the test project, subclassing something like
`AbstractMockBuilder<TBuilder, TClass>`) should you follow it instead — don't introduce that
pattern from scratch for a single test class; it only pays for itself once a class has many
constructor dependencies and several tests already need them configured differently.

```csharp
Mock<TemplateOrchestration> service = new TemplateOrchestrationMockBuilder()
    .WithDependency(utilitiesMock.Object)
    .WithDependency(entityFactoryMock.Object)
    // ...only supply the dependencies this test actually needs to configure;
    // WithDependency's builder auto-fills the rest with plain Mock<T>().Object
    .Build();

var output = service.Object.SomeMethod(input);
```
`Build()` typically constructs `new Mock<TClass>(...ctor deps..., CallBase = true)` so the
real (non-overridden) method bodies run against the mocked dependencies — check the
specific builder's `Build()` override, since `CallBase` defaults differ.

If no MockBuilder exists for a class that has many constructor dependencies and you expect
more tests to be added for it later, you can propose adding one — but treat it as a
suggestion to raise with the user, not something to add unprompted for a one-off test.

#### Instrumentation dependencies (`StartMethod`/`EndMethod`)

Check how the method under test calls tracing instrumentation before assuming it's a
blocker. In most CM services this is an **injected** dependency (a constructor parameter
typed `IUtilities`, commonly named `_utilities` or `navigoUtilities`) — in that case
`StartMethod`/`EndMethod` are just instance calls on that mock like any other dependency
call, and need no special handling:

```csharp
var utilities = new Mock<IUtilities>();
var service = new <FeatureName>Orchestration(..., utilities.Object, ...);
```

Only if the method instead calls a genuinely **static** framework method directly (e.g.
`Cmf.Foundation.Common.Utilities.StartMethod`, not through an injected instance) does this
become a real problem: that call will fail before the business logic runs with
`CallContext is not defined`, because `BaseTests` does not provision a live MES tenant or
service provider. Do not reach for `ApplicationContext.CreateRequestContext("tenant")` as a
workaround (it needs a fully configured CMF tenant registry) and do not use reflection or a
hand-built `CallContext`. Instead, introduce a narrow protected virtual seam that preserves
production behavior:

```csharp
protected virtual void StartMethod(string methodName, params KeyValuePair<string, object>[] parameters)
{
    Utilities.StartMethod(OBJECT_TYPE_NAME, methodName, parameters);
}

protected virtual void EndMethod(params KeyValuePair<string, object>[] parameters)
{
    Utilities.EndMethod(-1, -1, parameters);
}
```

Replace the relevant direct static calls with these methods, then derive a private test
subclass that overrides both with no-ops. This isolates instrumentation only; the
production implementation still executes the original static calls. Prefer constructor
injection over this seam for new code, and reach for the seam only when changing an
existing static dependency isn't practical.

### 6. Write the test(s)

- Base class `BaseTests` (from `Cmf.Foundation.BaseTestsUtils`) for direct orchestration
  tests — check first whether the solution has its own thin subclass of `BaseTests` (same
  idea as the `<FeatureName>ActionBaseTests` pattern for DEEs) before defaulting to the bare
  framework class. If you're instead adding orchestration coverage via a consuming DEE (per
  step 2's indirect-testing case), that test inherits `ActionBaseTests` per the
  `cm-dee-unittests` skill's conventions.
- Moq + FluentAssertions, same idioms as DEE tests: `.Should().Be(...)`,
  `.Should().NotBeNull()`, and for expected failures,
  `Action act = () => service.Object.Method(input); act.Should().Throw<CmfBaseException>();`
- `mock.MockCollectionWithList(...)` / `MockCollectionWithCollection(...)` for any
  `I*Collection` dependency, same as in DEE tests.
- `// Arrange` / `// Act` / `// Assert` section comments.
- New standalone test file and class name: `<OrchestrationSourceFileName>Tests.cs` and
  `<OrchestrationSourceFileName>Tests`. Keep an existing mapped file's class name unchanged
  if it differs, but do not add a second file for the same orchestration.
- New test method names: match the dominant local style. Common patterns:
  `<ServiceMethodName>_Success` / `<ServiceMethodName>_Fail` /`<ServiceMethodName>[_<Scenario>][_<Outcome>]`. Use `[Theory]`/`[InlineData]` when only input values
  vary across otherwise-identical failure scenarios (e.g. different combinations of hold
  status / attribute presence all expected to throw).
- Namespace: copy from the closest sibling orchestration test file verbatim; if none exists
  yet, match the namespace style used by this solution's DEE tests rather than computing it
  from the folder path (see `cm-dee-unittests` on namespace variance).

Cover the happy path, each meaningful validation/branch, and thrown-exception cases,
verifying both the returned `*Output` and any expected calls on mocked dependencies
(`mock.Verify(u => u.SomeMethod(...), Times.Once)`) where the method's contract is really
about triggering a side effect rather than just computing a return value.

### 7. Report back

State which file you created or extended, list the scenarios covered, note which
construction pattern you used (direct construction vs. MockBuilder) and why, and flag
anything you couldn't confidently resolve (an ambiguous dependency, an unclear existing
convention, whether this solution wants direct or DEE-level orchestration coverage)
instead of guessing past it.

