# DEE unit test conventions — cross-solution reference

There is no written testing guideline in any of these repos — this table exists so you don't have to
re-derive it from scratch each session. It is a *starting point*, not ground truth for any
specific file: always check the sibling tests you're actually extending/matching, since
solutions (and even folders within a solution) diverge from each other.

## What's universal (safe to assume everywhere)

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
  is CMF framework code, not something custom in the repo). It exposes:
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

## What varies — check before you assume

| Aspect | Active | Baseline | Cochlear |
|---|---|---|---|
| DEE category subfolders | `NameGenerator`, `MaterialActions`, `ContainerActions`, `ProcessRules`, etc. (30+) | `BatchManagement`, `DocumentManagement`, `NameGenerator`, `Rules`, `Snippets`, `WorkaroundActions`, etc. | `CochlearRules`, `NameGenerators` (plural!), `MaterialManagement`, `MIP`, `Notification`, etc. |
| Test namespace | `Cmf.Custom.<Project>.UnitTests.Actions` | `Cmf.Custom.<Project>.UnitTests.Actions` | Flatly `Cmf.Custom.UnitTests.Actions` — **no solution segment at all** |
| Assertion style | FluentAssertions (`.Should()...`) | FluentAssertions | Mixed: some files use FluentAssertions, others plain xUnit `Assert.*` (often aliased `using Assert = Xunit.Assert;` to disambiguate from MSTest) |
| Extra attributes | xUnit only | xUnit only | Some files stack `[TestClass]`/`[TestMethod]` (MSTest) directly alongside `[Fact]`/`[Theory]` on the same method |
| `Cmf.Custom.UnitTests.Utils` richness | Rich: per-utility `MockBuilders/`, `Constants/` (localized messages, SAP responses), `Utilities/` (static mock-graph builders like `<Project>TestUtilitiesMES.GetContainerMock(...)`) | Similarly rich, plus `DataTypes/` | Minimal: only `AbstractMockBuilder.cs` + one utilities mock builder — don't assume a rich helper library is available here |

## Test class naming actually observed (pick your default, but match existing files)

`<DeeClassName>UnitTests` (most common — use this for new files), `<DeeClassName>Tests`,
`<DeeClassName>UnitTest` (singular) all coexist within the same solution. File name and
class name are not guaranteed to match.

## Test method naming actually observed (no repo-wide convention — match the local folder)

- `<DeeClassName>ActionTest[_<Scenario>]` — common in Active.
- `ActionCode_<DeeClassName>` — common in Baseline.
- `<Scenario>_HappyPath` — seen in Cochlear DEE tests and Baseline orchestration tests.
- `DeeActionCode_<Scenario>_<Outcome>` / `DeeTestCondition_AlwaysReturnsTrue` — very
  descriptive Given-When-Then style, seen in Cochlear.

If the folder you're adding to has no single dominant style (some don't), default to
`<DeeClassName>_<Scenario>`.

## Worked example

Source — `TplGetMaterialBaseName.cs`:
```csharp
public class TplGetMaterialBaseName : DeeDevBase
{
    public override bool DeeTestCondition(Dictionary<string, object> Input) { return true; }

    public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
    {
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

Test — `TplGetMaterialBaseNameUnitTests.cs`:
```csharp
public class TplGetMaterialBaseNameUnitTests : ActionBaseTests
{
    [Theory]
    [InlineData("MAT001")]
    [InlineData("MAT001-S-0001")]
    public void ActionCode_TplGetMaterialBaseName(string materialName)
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
