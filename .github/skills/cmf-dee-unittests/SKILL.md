---
name: cmf-dee-unittests
description: Write, add, or update unit tests for CMF (Critical Manufacturing) DEEs — Design Enhancement Extensions / custom Actions that derive from DeeDevBase and live under Cmf.Custom.*.Data\DEEs. Use this whenever the user asks to test a DEE, action, name generator, rule, or "custom action", or mentions DeeDevBase, DeeActionCode, DeeTestCondition, ActionBaseTests, or a class/file under a `DEEs` folder — even if they just paste a DEE class and ask "can you cover this with tests" without saying "DEE" explicitly. This is specifically for the Active, Baseline, and <Project> business solutions in this workspace (and any sibling solution with the same Cmf.Custom.*.Data\DEEs / Cmf.Custom.UnitTests\Actions layout).
---

# CMF DEE Unit Tests

Generates or extends xUnit unit tests for CMF DEEs (custom Actions), matching whatever
conventions the target solution's own test suite already uses. There is no written
testing standard in these repos — three business solutions (Active, Baseline, <Project>)
each evolved their own local dialect of the same underlying pattern, and they disagree
with each other (and sometimes with themselves) on naming. So the rule throughout this
skill is: **read neighboring files before writing code, and mirror what they do** rather
than assuming any single convention holds everywhere. `references/conventions.md` has the
cross-solution cheat-sheet distilled from real code — read it once per session so you
don't have to re-derive the basics, but still verify against the actual local files each
time, since folder-by-folder variance is the norm here, not the exception.

## Workflow

### 1. Find the DEE

If given a class name only, search `**/DEEs/**/<Name>.cs` (case-insensitive glob/grep — DEE
file names don't always exactly match the class name, e.g. `<Project>SerialNumberGeneration.cs`
contains class `<Project>SerialNumberGenerator`). If given a path or pasted code, use that
directly. Note which business solution it belongs to from the path
(`Cmf.Custom.<Solution>.Data\DEEs\...`) — everything downstream is scoped to that solution.

### 2. Locate the test home and existing test

From the DEE's solution, find:
- `Cmf.Custom.<Solution>.Business\Cmf.Custom.<Solution>.Business\Cmf.Custom.UnitTests\Actions\`
  — this is a **flat** folder (no category subfolders even though DEEs themselves are
  organized into category subfolders like `NameGenerator/`, `Rules/`, `WorkaroundActions/`).
- The sibling `Cmf.Custom.UnitTests.Utils` project (same level as `Cmf.Custom.UnitTests`),
  which may hold `MockBuilders/`, `Constants/`, `Utilities/` — useful pre-built helpers,
  though not every solution has all three (<Project>'s is minimal: just an
  `AbstractMockBuilder` and one utilities mock builder).

Search the Actions folder for an existing test of this DEE **by class name, not just file
name** — file/class name mismatches happen (e.g. `CheckParametersLimitsUnitTests.cs` holds
class `CustomCheckParametersLimitsUnitTests`). If found, read it fully: you'll extend this
file with new `[Fact]`/`[Theory]` methods rather than creating a duplicate test class.

If nothing exists yet, pick 1-2 sibling tests in that same Actions folder whose DEE has a
similar shape to the one you're testing (same kind of dependency, e.g. both resolve
`INameGenerator`, or both validate input and throw) and use them as your concrete style
template — for imports, base class, namespace, mock setup idioms, and assertion style.
Local precedent always outranks the generic defaults in `references/conventions.md`.

### 2a. Maintain one DEE per test file

Every substantive DEE source must have exactly one dedicated sibling test file named
`<DeeSourceFileName>Tests.cs`. For example,
`Automation/CustomAutomationRetrieveConfigurationData.cs` maps to
`CustomAutomationRetrieveConfigurationDataTests.cs` in the test project. Do not combine
unrelated DEEs in a generic `DeeActionTests.cs` file, and do not create multiple test
files for one DEE.

Exclude framework/build support files from this rule: `DeeDevBase.cs`, assembly metadata
under `Properties/`, and generated `bin/` and `obj/` sources. The requirement applies to
concrete DEE implementations, including name generators and process rules. A dedicated
file is not permission for a placeholder test: cover an observable action contract when
it can run in isolation, or at minimum the executable condition/guard behavior when the
action needs a live MES request context, database query, or concrete entity model.

Where the solution controls its test project, enforce the mapping at build time. Add a
target that discovers source files, transforms each filename to `<Name>Tests.cs`, and
fails the build when a file is absent. Keep the paths local to the solution and exclude
generated files:

```xml
<Target Name="ValidateProductionTestFileParity" BeforeTargets="PrepareForBuild">
  <ItemGroup>
    <DeeSource Include="../../../<Solution>.Data/DEEs/**/*.cs"
      Exclude="../../../<Solution>.Data/DEEs/DeeDevBase.cs;../../../<Solution>.Data/DEEs/Properties/**/*.cs;../../../<Solution>.Data/DEEs/bin/**/*.cs;../../../<Solution>.Data/DEEs/obj/**/*.cs" />
    <ExpectedProductionTest Include="@(DeeSource->'$(MSBuildProjectDirectory)/%(Filename)Tests.cs')" />
    <MissingProductionTest Include="@(ExpectedProductionTest)" Condition="!Exists('%(FullPath)')" />
  </ItemGroup>
  <Error Condition="'@(MissingProductionTest)' != ''"
    Text="Missing dedicated test file(s): @(MissingProductionTest)." />
</Target>
```

### 3. Read the DEE thoroughly

Before writing anything, work out from the DEE source:
- Every dependency resolved off the service locator (`serviceProvider.GetService<T>()`)
  and every `entityFactory.Create<T>()` call — each of these needs a `Mock<T>` in the test.
- Every key read from `Input` (e.g. `Input["EntitySource"]`) — these need to be set on
  `ActionInput` in the Arrange step.
- What gets written to `Input["Result"]`, or what side effects happen (calls on mocked
  entities like `.Save()`, `.AddGeneratorContexts()`) — these are your assertion targets.
- Every branch in `DeeTestCondition` and `DeeActionCode`, and every exception path — each
  meaningful branch deserves its own scenario (via `[Theory]`/`[InlineData]` where the
  branches differ only by input values, or separate `[Fact]` methods where the setup
  differs more substantially).
- `UseReference(...)` calls are DLL/namespace declarations for the DEE runtime — ignore
  them, they have no bearing on the test.

If the DEE references a constant you can't resolve (e.g. from a `*Constants` class you
don't have visibility into), don't guess its value — either locate the constant's
definition or flag it to the user instead of inventing a plausible-looking string.

### 4. Write the test(s)

Match, in order of priority: (a) the existing test file for this DEE if one exists, then
(b) the sibling templates you picked in step 2, then (c) the cross-solution defaults in
`references/conventions.md`. Concretely, that almost always means:

- Base class `ActionBaseTests`, from `Cmf.Foundation.BaseTestsUtils`.
- `AddMockToActionInput<T>()` to get an auto-registered `Mock<T>` (or
  `AddMockToActionInput(existingMock)` when you need to configure the mock before
  registering it — e.g. it needs a `.Setup()` that depends on the mock instance existing
  first).
- Moq for all dependency mocking, FluentAssertions for assertions (`.Should().Be(...)`,
  `.Should().Throw<CmfBaseException>()`, etc.) unless the local file(s) you're matching
  use something else (a few files mix in plain xUnit `Assert.*`, and one solution's DEE
  tests stack MSTest `[TestClass]`/`[TestMethod]` alongside xUnit — replicate that only if
  the file you're extending already does it, don't introduce the mix into a clean file).
  `[Fact]` for one scenario, `[Theory]` + `[InlineData]` when only input values vary.
- `mock.MockCollectionWithList(list)` / `MockCollectionWithCollection(...)` to back any
  `I*Collection` interface (e.g. `IGeneratorContextCollection`) with a real, indexable,
  enumerable list — don't hand-stub indexers or `GetEnumerator()`.
- `// Arrange` / `// Act` / `// Assert` section comments.
- New standalone test file and class name: `<DeeSourceFileName>Tests.cs` and
  `<DeeSourceFileName>Tests`. When extending an existing mapped file, keep its existing
  class name if it differs, but do not rename it without coordinating the change.
- New test method names: follow whatever the dominant style is among sibling tests in the
  same Actions folder (styles seen include `<DeeClassName>ActionTest[_<Scenario>]`,
  `ActionCode_<DeeClassName>`, `<Scenario>_HappyPath`). If the folder has no clear dominant
  style, default to `<DeeClassName>_<Scenario>` (e.g.
  `TplGetMaterialBaseName_ReturnsBaseNameWhenSuffixPresent`).
- Namespace: copy the namespace of the closest sibling test file verbatim — don't compute
  it from a pattern, since at least one solution (<Project>) doesn't include the solution
  name in its test namespace at all.

Cover the happy path, each meaningful branch, and error/exception cases, but don't pad the
class with redundant scenarios that exercise the same code path with cosmetically
different data.

### 5. Report back

State which file you created or extended, list the scenarios you added (one line each is
fine), and call out anything you couldn't confidently resolve (an unresolved constant, an
ambiguous naming convention, a dependency you weren't sure how to mock) rather than
silently guessing past it.
