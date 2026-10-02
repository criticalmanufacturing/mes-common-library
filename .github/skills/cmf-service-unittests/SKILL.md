---
name: cmf-service-unittests
description: Write, add, or update unit tests for CMF (Critical Manufacturing) custom services / orchestrations — the classes under Cmf.Custom.*.Business\...\Cmf.Custom.Orchestration\Orchestrations that implement an I*Orchestration interface and expose service methods with *Input/*Output DTOs. Use this whenever the user asks to test a custom service, orchestration method, or business-logic service, or mentions an Orchestration class/interface, ServiceOrchestration, an *Input/*Output pair, or a class/file under an `Orchestration`/`Orchestrations` folder — even if they just paste a service method and ask "can you cover this with tests" without saying "orchestration" explicitly. This is for Business solutions in Business packages and DEEs in a Data Package.
---

# CMF Custom Service (Orchestration) Unit Tests

Generates or extends xUnit unit tests for CMF custom services — the orchestration classes
that implement business logic behind `I*Orchestration` interfaces, each exposing service
methods that take an `*Input` DTO and return an `*Output` DTO. As with the DEE skill, there
is no written testing standard here: each business solution grew its own local dialect,
and one solution (Cochlear) doesn't even have a dedicated orchestration test folder at
all — it tests orchestration behavior indirectly, through the DEEs that consume it. Always
verify against the actual local files before writing anything, since structure varies
solution-to-solution more than it does for DEEs.

## Workflow

### 1. Find the service and confirm the solution's shape

If given a method or interface name only, search for the interface
(`I<Solution>Orchestration` or similar) and its implementing class under
`Cmf.Custom.<Solution>.Business\...\Cmf.Custom.Orchestration\`. Before assuming a folder
layout, check what's actually there — this varies more than in the DEE world:

- Some solutions put every service method in **one large class**
  (`<Solution>Orchestration.cs` implementing `I<Solution>Orchestration`, often thousands
  of lines with dozens of methods) rather than one file per method.
- The folder holding it may be named `Orchestration` (singular) or `Orchestrations`
  (plural) — don't assume either.
- `InputObjects`/`OutputObjects` subfolders (or similarly named) hold the `*Input`/
  `*Output` DTOs referenced by each service method's signature.

### 2. Locate the test home — and check whether one exists at all

Look for `Cmf.Custom.<Solution>.Business\...\Cmf.Custom.UnitTests\Orchestration(s)\` (check
both spellings). **Don't assume this folder exists** — at least one solution in this
workspace has no dedicated orchestration test folder; instead, the orchestration
interface is mocked as a dependency inside the *DEE* tests that call it
(`serviceProvider.GetService<I*Orchestration>()`), living under that solution's `Actions`
test folder. If you find no orchestration test folder and no orchestration test project
reference, check whether the DEE unit tests already exercise this service indirectly
before concluding tests need to be written from scratch — and consider whether direct
service-level tests or DEE-level tests (or both) best match how that solution already
verifies orchestration behavior. When in doubt, ask the user which they want rather than
silently picking one.

Where a dedicated orchestration test folder exists, search it for an existing test of this
service method by class name (one test class per method is the norm: `<MethodName>UnitTests`
or `<MethodName>Tests`). If found, read it fully and add new test methods to it rather than
duplicating the file.

Also check the sibling `Cmf.Custom.UnitTests.Utils` project for a `MockBuilders/` entry
matching this orchestration class (e.g. `<Solution>OrchestrationMockBuilder.cs` or
`TemplateOrchestrationMockBuilder.cs`) — if one exists, it's the standard way to construct
the class under test, and you should use it rather than hand-rolling construction.

### 2b. Maintain one orchestration per test file

Every concrete orchestration source has exactly one dedicated test file named
`<OrchestrationSourceFileName>Tests.cs`. For example,
`Orchestration/CustomAutomationConfigurationOrchestration.cs` maps to
`CustomAutomationConfigurationOrchestrationTests.cs` in the test project. This maps source
classes, not individual DTO files or methods: keep related method tests in the single
matching orchestration test file.

Exclude composition/support files such as `OrchestrationStartupModule.cs`, assembly
metadata, and generated `bin/`/`obj/` sources. Avoid generic shared test files that cover
multiple orchestration classes. Each matching file needs meaningful behavior coverage;
where MES static context blocks a method, test isolated guards/branches and document the
runtime dependency instead of fabricating framework state.

When the solution has a dedicated test project, enforce both DEE and orchestration parity
in its `.csproj`. Extend the DEE parity target with the root orchestration sources:

```xml
<OrchestrationSource Include="../../Orchestration/*.cs"
  Exclude="../../Orchestration/OrchestrationStartupModule.cs" />
<ExpectedProductionTest Include="@(OrchestrationSource->'$(MSBuildProjectDirectory)/%(Filename)Tests.cs')" />
```

Combine those items with the DEE `ExpectedProductionTest` items in one
`ValidateProductionTestFileParity` target and fail the build when `MissingProductionTest`
is non-empty. Test the target by running the focused `dotnet test` command; it runs before
the test assembly is built.

### 2a. When no test project exists

Use the projects under `EXAMPLES/` as the golden template. Create a focused `net8.0` test
project under `Tests/Orchestration.UnitTests/` with:

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <IsPackable>false</IsPackable>
  <IsTestProject>true</IsTestProject>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Cmf.Foundation.BaseTestsUtils" Version="<MES version>" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="<current compatible version>" />
  <PackageReference Include="xunit" Version="<current compatible version>" />
  <PackageReference Include="xunit.runner.visualstudio" Version="<current compatible version>">
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="../../Orchestration/Orchestration.csproj" />
</ItemGroup>
```

Match the CMF package version to the feature's MES version (for example, use
`Cmf.Foundation.BaseTestsUtils` `11.3.3` for an MES 11.3.3 feature). Inherit direct
orchestration test classes from `BaseTests`, as in the Active and Baseline examples.

### 3a. Static MES instrumentation and request context

`BaseTests` does not provision a live MES tenant or service provider. Do not assume it
makes static framework calls testable. In particular, a method that calls
`Cmf.Foundation.Common.Utilities.StartMethod`/`EndMethod` directly will fail before its
business logic with `CallContext is not defined`.

`ApplicationContext.CreateRequestContext("tenant")` is appropriate only when the test has
a fully configured CMF tenant registry and service provider; otherwise it can fail while
resolving tenant information. Do not use reflection or a hand-built `CallContext` as a
test workaround.

When static instrumentation is the only blocker, introduce a narrow protected virtual seam
that preserves production behavior:

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

Replace the relevant direct calls with these methods. In the test, derive a private test
subclass that overrides both methods with no-ops. This isolates instrumentation only; the
production implementation still executes the original static calls. Prefer constructor
injection over this seam for new code and use the seam only when changing the existing
static dependency is not practical.

### 3. Read the service method thoroughly

Before writing anything, work out:
- The full constructor dependency list of the orchestration class (every interface it
  takes in) — you need a mock (or `null`, for genuinely unused-by-this-method
  dependencies) for each one when constructing it.
- What the specific method under test reads off its `*Input` DTO, what it calls on each
  dependency, what conditions it checks (holds, statuses, attribute values, existence
  checks), and what it returns in the `*Output` DTO or throws (commonly wrapped in
  `CmfBaseException`).
- Whether the method delegates to other injected "utilities" interfaces
  (`I<Solution>UtilitiesCore/Custom/MES/Generic` or similar) rather than doing raw entity
  manipulation itself — if so, your mocks target those utility calls, not lower-level
  entity behavior.

### 4. Construct the class under test

Two patterns coexist; prefer whichever the sibling tests/MockBuilders for this class
already use:

**(a) Fluent MockBuilder** (preferred when one exists for this class) — subclasses
`AbstractMockBuilder<TBuilder, TClass>` (shared framework-style base already in
`Cmf.Custom.UnitTests.Utils\MockBuilders\AbstractMockBuilder.cs`, identical across
solutions):
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

**(b) Direct construction** — `new <Solution>Orchestration(dep1, dep2, ..., null, null...)`,
passing mocks for dependencies the test cares about and `null` for unused constructor
parameters. Used when no MockBuilder exists yet, or when the sibling tests for this class
favor it.

If no MockBuilder exists for a class that has many constructor dependencies and you expect
more tests to be added for it later, consider proposing one (mirroring
`TemplateOrchestrationMockBuilder`'s shape) rather than repeating a long direct-construction
call in every test — but don't introduce this extra file for a one-off, isolated test.

### 5. Write the test(s)

- Base class `BaseTests` (from `Cmf.Foundation.BaseTestsUtils`) for direct orchestration
  tests. If you're instead adding orchestration coverage via a consuming DEE (per step 2's
  indirect-testing case), that test inherits `ActionBaseTests` per the
  `cmf-dee-unittests` skill's conventions.
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
  `<ServiceMethodName>_Success` / `<ServiceMethodName>_Fail`, or
  `<ServiceMethodName>_HappyPath`. Use `[Theory]`/`[InlineData]` when only input values
  vary across otherwise-identical failure scenarios (e.g. different combinations of hold
  status / attribute presence all expected to throw).
- Namespace: copy from the closest sibling orchestration test file verbatim.

Cover the happy path, each meaningful validation/branch, and thrown-exception cases,
verifying both the returned `*Output` and any expected calls on mocked dependencies
(`mock.Verify(u => u.SomeMethod(...), Times.Once)`) where the method's contract is really
about triggering a side effect rather than just computing a return value.

### 6. Report back

State which file you created or extended, list the scenarios covered, note which
construction pattern you used (MockBuilder vs. direct) and why, and flag anything you
couldn't confidently resolve (an ambiguous dependency, an unclear existing convention,
whether this solution wants direct or DEE-level orchestration coverage) instead of
guessing past it.
