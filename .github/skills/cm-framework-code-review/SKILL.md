---
name: cm-framework-code-review
description: Best practices and conventions for Critical Manufacturing's MES internal framework.
---

## CM Framework Code Review

Scans C#/.NET code for adherence to CM's MES internal framework best practices, conventions and patterns described below. Returns prioritized findings with concrete fixes.

## When to Use

- Reviewing CM project business logic code (e.g., DEE Actions, custom services, etc.) for adherence to internal framework best practices.

## Performance anti-patterns

### Levels To Load
[🔴 CRITICAL] `levelsToLoad` > 1 on `Load`/`LoadRelations` can fetch an enormous amount of data.
```csharp
material.Load(levelsToLoad: 2);              // single entity
materialCollection.Load(levelsToLoad: 2);     // collection
material.LoadRelations(levelsToLoad: 2);      // relations
```

### Loading entities inside a loop
[🟠 HIGH] Prefer calling `.Load()` on the collection once, not per-entity inside a loop. ALWAYS confirm the collection's `.Load()` is implemented before reporting.
```csharp
// ❌ material.Load(materialName) called once per iteration
foreach (string materialName in materials)
{
  Material material = entityFactory.Create<IMaterial>();
  material.Load(materialName);
}

// ✅ build the collection, then Load() once
IMaterialCollection materials = entityFactory.CreateCollection<IMaterialCollection>();
foreach (string materialName in materialNames)
{
    IMaterial material = entityFactory.Create<IMaterial>();
    material.Name = materialName;
    materials.Add(material);
}
materials.Load();
```

### Entity-level operations over collection-level operations
[🟠 HIGH] Same idea for any entity op, not just `Load()` — e.g. `LoadChildren()`. Build the collection first, then call the op once. ALWAYS confirm the collection-level method exists before reporting.
```csharp
// ❌ per-entity LoadChildren() inside the loop
foreach (var order in orders)
{
    material.Load(order.LotNum);
    material.LoadChildren();
}

// ✅ batch, then one LoadChildren() call for all
IMaterialCollection materials = _entityFactory.CreateCollection<IMaterialCollection>();
foreach (var order in orders)
{
    IMaterial material = _entityFactory.Create<IMaterial>();
    material.Name = order.LotNum;
    materials.Add(material);
}
materials.Load();
materials.LoadChildren();
```

### Entity object direct instantiation
[🟡 MEDIUM] Use the entity factory, not `new()`. Does **not** apply inside test code — see "CM Framework Test Conventions" below.
```csharp
Material material = new Material();                    // ❌
Material material = entityFactory.Create<IMaterial>();  // ✅
```

### Extension methods referencing ApplicationContext
[🟡 MEDIUM] Extension methods that pull services from `ApplicationContext` inside their body aren't mockable, so they're hard to test.
```csharp
public static class MaterialExtensions
{
  public static void DoSomething(this Material material)
  {
    var entityFactory = ApplicationContext.CurrentServiceProvider.GetService<IEntityFactory>();  // ❌ not mockable
  }
}
```

### Excessive lazy loading
[🟠 HIGH] LINQ chains touching multiple reference properties (`m.Product`, `m.Facility`, ...) trigger one lazy load per property per item. Load references up front instead.
```csharp
// ❌ Product/Facility/Step lazily loaded per item
var steps = materials.Where(m => m.Product.Name == "P1").Where(m => m.Facility.Name == "FAB1").Select(m => m.Step.Name).ToList();

// ✅ load references first
materials.LoadReferences(new List<string> { "Product", "Facility", "Step" });
var steps = materials.Where(m => m.Product.Name == "P1").Where(m => m.Facility.Name == "FAB1").Select(m => m.Step.Name).ToList();
```

### Load reference entities just to access Foreign Key (FK) values
[🟡 MEDIUM] FK values are already loaded with the entity. Use `GetNativeValue` instead of lazy-loading the reference just for its Id.
```csharp
long productId = material.Product.Id;                    // ❌ lazy-loads Product
long productId = material.GetNativeValue<long>("Product"); // ✅
```

### Access Config using GetConfig instead of TryGetConfig
[🟡 MEDIUM] `TryGetConfig` hits the cache first; `GetConfig` hits the database directly.
```csharp
IConfig cfg = Config.GetConfig("/Cmf/Custom/App/MaxRetries/");   // ❌ always DB
if (Config.TryGetConfig("/Cmf/Custom/App/MaxRetries/", out IConfig cfg))  // ✅ cache first
    int maxRetries = cfg.GetConfigValue<int>();
```

### Writing to config entries in hot paths
[🟠 HIGH] Config entries should change rarely — don't use one as a counter/property for frequently-changing values (e.g. inside a DEE that runs often).
```csharp
IConfig counter = Config.GetConfig("/Cmf/Custom/Tracking/ProcessedCount/");  // ❌ hot-path write
counter.Value = ((int)counter.Value) + 1;
counter.Save();
```

### Resolving smart tables inside a loop
[🟠 HIGH] `BulkResolve` runs one stored procedure for all entries, instead of one SP call per iteration.
```csharp
// ❌ 1 stored procedure per material
foreach (IMaterial material in materials)
{
    INgpDataRow row = NgpDataRow.Create(smartTable);
    row["Material"] = material.Name;
    DataTable result = smartTable.Resolve(row, false, true, false);
}

// ✅ one SP call for all materials
var rows = new Dictionary<string, INgpDataRow>();
foreach (IMaterial material in materials)
{
    INgpDataRow row = NgpDataRow.Create(smartTable);
    row["Material"] = material.Name;
    rows[material.Name] = row;
}
Dictionary<string, DataTable> results = smartTable.BulkResolve(rows, false, true, false);
```

### Tables: LoadData() without Filters
[🔴 CRITICAL] `LoadData()` without filters loads every row — always pass filters.
```csharp
// ❌ SELECT * — no WHERE clause, every row into memory
IGenericTable table = new GenericTable { Name = "ProcessParameterConfig" };
table.Load(1);
table.LoadData();

// ✅ filtered
IFilterCollection filters = new FilterCollection();
filters.Add(new Filter { Name = "ProductType", Operator = FieldOperator.IsEqualTo, Value = "Wafer", LogicalOperator = LogicalOperator.AND });
filters.Add(new Filter { Name = "IsActive", Operator = FieldOperator.IsEqualTo, Value = true });
table.LoadData(filters);
```

Before flagging any of the above: confirm the suggested collection-level method actually exists in the codebase; re-evaluate CRITICAL/HIGH findings to make sure they're genuine; extend investigation elsewhere in the codebase if unsure.

## DEE Action internals

Consider the following about DEEs, applies to files under `*.Data/DEEs/**`:

1. Each DEE action contains two methods: `ValidateAction` and `EvaluateRule`. The `ValidateAction` is triggered first and is responsible for validating and verifying if the DEE should be executed. It returns a boolean (to be executed or not) and sometimes some context parameters that will be passed to the `EvaluateRule` method. The `EvaluateRule` method is responsible for executing the main logic of the DEE and it is only triggered if `ValidateAction` returns true.

2. Each DEE is already compiled with the correct `UseReference` statements for the internal frameworks, since it does not possess the knowledge to make that determination.

3. The following keys are ALWAYS present in the DEE Action's Input dictionary: `ServiceProvider`, `ActionGroupName`.

4. There is no difference in calling `GetService<T>()` vs `GetRequiredService<T>()` for fetching services from the service provider. All services are already registed in the service container.

## Validation

Before delivering results, verify:

- [ ] If you are not sure about a finding, extend the investigation to the rest of the codebase to gather more context before making a final decision.
- [ ] Each finding includes a concrete code fix.
- [ ] For CRITICAL and HIGH severity findings, re-evaluate the code to ensure the issue is indeed a high-priority problem.
- [ ] Verify and confirm proposed solution is valid (e.g., if proposing to use a collection-level method, confirm that the method is implemented in the codebase).
