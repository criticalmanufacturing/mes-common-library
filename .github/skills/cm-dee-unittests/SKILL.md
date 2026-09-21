---
name: cm-dee-unittests
description: Write, add, or update unit tests for CM (Critical Manufacturing) DEEs — Dynamic Execution Engine / custom Actions that derive from `DeeDevBase` and live under a feature's `Data/DEEs` package. Use this whenever the user asks to test a DEE, action, name generator, rule, or "custom action", or mentions `DeeTestCondition`/`DeeActionCode`, `ActionBaseTests`, or a class/file under a `DEEs` folder — even if they just paste a DEE class and ask "can you cover this with tests" without saying "DEE" explicitly. This targets this repository's feature layout - each folder under `features/` is a separate feature, with sibling packages such as `Cmf.Community.<FeatureName>.Business`, `Cmf.Community.<FeatureName>.Data`, and `Cmf.Community.<FeatureName>.IoT` where applicable.
---

# CM DEE Unit Tests

Generates or extends xUnit unit tests for CM DEEs (custom Actions), matching whatever
conventions the selected feature's packages already use. Each folder under `features/` is a
separate feature; package types such as `Business`, `Data`, and `IoT` are sibling packages
under that feature. DEE source normally belongs to the feature's `Data` package, while its
compiled action dependencies commonly come from the sibling `Business` package. Package and
namespace names follow `Cmf.Community.<FeatureName>(.<PackageType>)`. The rule throughout
this skill is: **read neighboring files before writing code, and mirror what they do** rather
than assuming any single convention holds everywhere. The `Conventions` section has a
cheat-sheet distilled from real code — read it once per session so you don't have to re-derive
the basics, but still verify against the actual local files each time.

## Workflow

### 1. Find the DEE

If given a class name only, search `**/DEEs/**/<Name>.cs` (case-insensitive glob/grep — DEE
file names don't always exactly match the class name, e.g. `<Project>SerialNumberGeneration.cs`
contains class `<Project>SerialNumberGenerator`). If given a path or pasted code, use that
directly. DEE classes derive from `DeeDevBase` and override
`DeeTestCondition(Dictionary<string, object> Input)` (the gate condition) and
`DeeActionCode(Dictionary<string, object> Input)` (the main logic). First identify the feature
folder under `features/`, then confirm which `<FeatureName>.Data/DEEs` package contains the
class — everything downstream is scoped to that feature and package.

### 2. Locate the test home and existing test

The test project is determined from the selected feature and package layout. Look first in the
package's `Business` package for a test project named `Cmf.Community.<FeatureName>.UnitTests`, or a local
equivalent) that references the DEE project. DEEs are grouped
into category folders (e.g. `Material`, `ProtocolReportValidation`, `DataCollections`) that
don't always match 1:1 with the test project's own subfolders, and even the nesting depth
within a category varies (some put tests directly in `<Category>/`, others one level deeper
in `<Category>/Actions/` or `<Category>/Rules/`) — don't assume, look at what's already there.

Also check the selected feature's test package for an `Abstractions/` folder and a
project-specific base test class
(commonly named `<FeatureName>ActionBaseTests`) that extends the framework's `ActionBaseTests`
and pre-registers commonly used mocks (e.g. generic utils, entity factory, context utils) via
`AddMockToActionInput<T>()` in its constructor. When one exists for the solution, extend it
instead of the bare `ActionBaseTests` — it's there precisely so individual test classes don't
re-declare the same boilerplate mocks.

Search the main and sub-folders for an existing test of this DEE **by class name, not just file
name** — file/class name mismatches happen (e.g. `CheckParametersLimitsUnitTests.cs` holds
class `CustomCheckParametersLimitsUnitTests`). If found, read it fully: you'll extend this
file with new `[Fact]`/`[Theory]` methods rather than creating a duplicate test class.

If nothing exists yet, pick 1-2 sibling tests in that same category folder whose DEE has a
similar shape to the one you're testing (same kind of dependency, e.g. both resolve
`INameGenerator`, or both validate input and throw) and use them as your concrete style
template — for imports, base class, namespace, mock setup idioms, and assertion style.
Local precedent always outranks the generic defaults in `Conventions`.

### 2a. Maintain one DEE per test file

Every substantive DEE source must have exactly one dedicated sibling test file named
`<DeeSourceFileName>Tests.cs`. For example, `Material/<Project>RetrieveConfigurationData.cs`
maps to `<Project>RetrieveConfigurationDataTests.cs` somewhere under the matching category
folder in the test project (see step 2 for how nesting depth varies). Do not combine
unrelated DEEs in a generic `DeeActionTests.cs` file, and do not create multiple test
files for one DEE.

Exclude framework/build support files from this rule: assembly metadata under `Properties/`,
and generated `bin/` and `obj/` sources. The requirement applies to concrete DEE
implementations, including name generators and process rules. A dedicated file is not
permission for a placeholder test: cover an observable action contract when it can run in
isolation, or at minimum the executable condition/guard behavior when the action needs a
live MES request context, database query, or concrete entity model. Not every existing DEE
in the solution will already have a matching test file — treat that as a gap to close for
the DEE you're touching, not a license to backfill unrelated ones unless asked.

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
`Conventions`. Concretely, that almost always means:

- Base class: the feature/package's own `<FeatureName>ActionBaseTests` (look under `Abstractions/` in
  the test project) when one exists, otherwise `ActionBaseTests` directly from
  `Cmf.Foundation.BaseTestsUtils`.
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
  same category folder (styles seen include `<DeeClassName>ActionTest[_<Scenario>]`,
  `ActionCode_<DeeClassName>`, `<Scenario>_HappyPath`). If the folder has no clear dominant
  style, default to `<DeeClassName>_<Scenario>` (e.g.
  `TplGetMaterialBaseName_ReturnsBaseNameWhenSuffixPresent`).
- Namespace: copy the namespace of the closest sibling test file verbatim — don't compute
  it from the folder path. Namespaces don't reliably mirror the test project's own folder
  nesting (e.g. a file physically under `<Category>/` may still declare the bare
  nesting. Keep the namespace within the `Cmf.Community.<FeatureName>...` namespace family.

Cover the happy path, each meaningful branch, and error/exception cases, but don't pad the
class with redundant scenarios that exercise the same code path with cosmetically
different data.

### 5. Report back

State which file you created or extended, list the scenarios you added (one line each is
fine), and call out anything you couldn't confidently resolve (an unresolved constant, an
ambiguous naming convention, a dependency you weren't sure how to mock) rather than
silently guessing past it.

## Conventions

- DEE source classes derive from `DeeDevBase`
  and override:
  ```csharp
  public override bool DeeTestCondition(Dictionary<string, object> Input)
  public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
  ```
- Inside `DeeActionCode`, dependencies come off a service locator, not constructor
  injection:
  ```csharp
  var serviceProvider = (IServiceProvider)Input["ServiceProvider"];
  var entityFactory = serviceProvider.GetService<IEntityFactory>();
  ```
- Test classes derive from `ActionBaseTests` (NuGet `Cmf.Foundation.BaseTestsUtils` — this
  is CMF framework code, not something custom in the repo) or `<FeatureName>ActionBaseTests` (a project-specific subclass) if exists. It exposes:
  - `ActionInput` — the `Dictionary<string, object>` you pass into `DeeActionCode`.
  - `AddMockToActionInput<T>()` — creates a `Mock<T>`, registers it so
    `serviceProvider.GetService<T>()` resolves it, and returns the `Mock<T>` for further
    setup.
  - `AddMockToActionInput(existingMock)` — same registration, but for a mock you already
    built and configured (use this when the mock's setup needs to reference the mock
    object itself, or when you're wiring up a more elaborate object graph before
    registering it).
- Moq (`Mock<T>`, `.Setup(...)`, `.Verify(...)`) is the mocking framework everywhere.
- `mock.MockCollectionWithList(List<T> items)` / `MockCollectionWithCollection(...)` — 
  extension methods from `Cmf.Foundation.BaseTestsUtils.Extensions` — back an
  `I*Collection` interface mock with a real list so indexing/enumeration/`.Add()` all
  behave like a normal collection. Use these instead of manually stubbing indexers.
- `[Fact]` / `[Theory]` + `[InlineData(...)]` (xUnit) is present in every solution's test
  project and is the attribute set actually used for DEE tests, even though MSTest
  packages are also referenced in every `.csproj`.

  ### Test method naming convention

- `DeeTestCondition[_<Scenario>][_<Outcome>]`
- `DeeActionCode[_<Scenario>][_<Outcome>]`
- `<DeeClassName>_HappyPath`

### Worked example

Source — `TplGetMaterialBaseName.cs`:
```csharp
public class TplGetMaterialBaseName : DeeDevBase
{
  public override bool DeeTestCondition(Dictionary<string, object> Input) { return true; }

  public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
    {
        /*
        Initial comments and UseReference statements removed for brevity
        */

        var serviceProvider = (IServiceProvider)Input["ServiceProvider"];
        var entityFactory = serviceProvider.GetService<IEntityFactory>();

        var ng = entityFactory.Create<INameGenerator>();
        ng.Name = BBraunBaselineConstants.NG_TPL_SAMPLE_MATERIAL;
        ng.Load();
        ng.LoadGeneratorContexts(out int totalRows);

        var material = Input["EntitySource"] as IMaterial;
        string fullName = material.Name;
        var formatLength = ng.Tokens.First(it => it.Name == "Counter").Format.Length;
        var pattern = $"-S-[0-9]{{{formatLength}}}$";
        string baseName = Regex.IsMatch(fullName, pattern)
            ? fullName.Substring(0, fullName.Length - ("-S-".Length + formatLength))
            : fullName;

        Input["Result"] = baseName;
        return Input;
    }
}
```

Test — `TplGetMaterialBaseNameTests.cs`:
```csharp
public class TplGetMaterialBaseNameTests : ActionBaseTests
{
    [Theory]
    [InlineData("MAT001")]
    [InlineData("MAT001-S-0001")]
    public void DeeActionCode_ReturnsMaterialBasename(string materialName)
    {
        // Arrange
        var entityFactory = AddMockToActionInput<IEntityFactory>();
        var material = new Mock<IMaterial>();
        var nameGenerator = new Mock<INameGenerator>();
        var counterToken = new Mock<IGeneratorToken>();
        var tokenCollection = new Mock<IGeneratorTokenCollection>();

        entityFactory.Setup(x => x.Create<INameGenerator>()).Returns(nameGenerator.Object);
        counterToken.Setup(x => x.Name).Returns("Counter");
        counterToken.Setup(x => x.Format).Returns("0000");
        tokenCollection.MockCollectionWithCollection(new List<IGeneratorToken>() { counterToken.Object });
        nameGenerator.Setup(x => x.Tokens).Returns(tokenCollection.Object);
        material.Setup(x => x.Name).Returns(materialName);
        ActionInput["EntitySource"] = material.Object;

        // Act
        var testAction = new TplGetMaterialBaseName();
        var output = testAction.DeeActionCode(ActionInput);

        // Assert
        output["Result"].Should().Be("MAT001");
    }
}
```

Note how the test never mocks `ng.Load()`/`ng.LoadGeneratorContexts(...)` explicitly — Moq's
default loose-mock behavior means unconfigured void calls are no-ops, so you only need to
set up members whose return values the code under test actually reads.
