using System.Data;
using System.Reflection;
using Cmf.Foundation.Common.Logging;
using Cmf.Community.SystemMonitoringView.Actions.Monitoring;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessOrchestration.ApplicationMonitoringManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.ApplicationMonitoringManagement.OutputObjects;

namespace Cmf.Community.SystemMonitoringView.UnitTests;

public class CustomAppendInstalledCustomizationPackagesTests
{
    private sealed class FakeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class Harness
    {
        public int Queries;
        public int PatternReads;
        public string? Patterns = "Cmf.Custom.%";
        public bool QueryThrows;
        public DataTable Rows = NewTable();
        public GetApplicationMonitoringInformationOutput Output = new() { MonitoringInformation = new MonitoringInformationCollection() };
        public List<MonitoringContext>? Contexts;

        public Dictionary<string, object> Input()
        {
            var input = Readers();
            // orchestration-level hook: typed service objects under keys we do not know
            input["anyKey1"] = new GetApplicationMonitoringInformationInput { MonitoringContexts = Contexts! };
            input["anyKey2"] = Output;
            return input;
        }

        private Dictionary<string, object> Readers() => new()
        {
            ["ServiceProvider"] = new FakeServiceProvider(),
            ["InstalledPackagesReader"] = new Func<DataTable>(() =>
            {
                Queries++;
                if (QueryThrows) throw new InvalidOperationException("Invalid object name 'UserDataModel.V_InstalledPackage'");
                return Rows;
            }),
            ["PackagePatternsReader"] = new Func<string?>(() => { PatternReads++; return Patterns; }),
        };

        public Dictionary<string, object> Run()
        {
            var input = Input();
            var dee = new CustomAppendInstalledCustomizationPackages();
            return dee.DeeTestCondition(input) ? dee.DeeActionCode(input) : input;
        }

        public Harness Add(string id, string version, DateTime installedOn)
        {
            Rows.Rows.Add(id, version, installedOn);
            return this;
        }

        public List<MonitoringInformation> Result => Output.MonitoringInformation.ToList();
    }

    private static DataTable NewTable()
    {
        var t = new DataTable();
        t.Columns.Add("PackageId", typeof(string));
        t.Columns.Add("PackageVersion", typeof(string));
        t.Columns.Add("InstalledOn", typeof(DateTime));
        return t;
    }

    [Fact]
    public void Versioning_context_adds_one_row_per_matching_package()
    {
        var h = new Harness { Contexts = new() { MonitoringContext.CustomizationVersioning } };
        h.Add("Cmf.Custom.MESProject.Data", "11.3.5", new DateTime(2026, 10, 29, 10, 0, 0))
         .Add("Cmf.Custom.MESProject.Business", "11.3.4", new DateTime(2026, 10, 28, 9, 30, 0))
         .Add("Cmf.Environment", "11.3.5", new DateTime(2026, 10, 1));

        h.Run();

        Assert.Equal(2, h.Result.Count);
        var row = h.Result.Single(r => r.Name == "Cmf.Custom.MESProject.Data");
        Assert.Equal("11.3.5", row.Value);
        Assert.Equal("2026-10-29 10:00:00", row.Text);
        Assert.Equal(MonitoringContext.CustomizationVersioning, row.Context);
        Assert.Equal(MonitoringPath.Application, row.ParentPath);
        Assert.Equal(MonitoringPath.Versioning, row.ChildPath);
        Assert.Equal(MonitoringStatus.OK, row.Status);
        Assert.Equal(1, h.Queries);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Null_or_empty_context_filter_adds_rows(bool useNull)
    {
        var h = new Harness { Contexts = useNull ? null : new() };
        h.Add("Cmf.Custom.A", "1.0.0", new DateTime(2026, 1, 1));

        h.Run();

        Assert.Single(h.Result);
    }

    [Fact]
    public void Non_matching_packages_are_not_shown()
    {
        var h = new Harness();
        h.Add("Cmf.Environment", "11.3.5", new DateTime(2026, 1, 1)).Add("Cmf.LBOs.Core", "11.3.5", new DateTime(2026, 1, 1));

        h.Run();

        Assert.Empty(h.Result);
    }

    [Fact]
    public void Other_contexts_only_adds_nothing_and_does_not_query()
    {
        var h = new Harness { Contexts = new() { MonitoringContext.ModuleLicensing, MonitoringContext.BusinessVersioning } };
        h.Add("Cmf.Custom.A", "1.0.0", new DateTime(2026, 1, 1));

        h.Run();

        Assert.Empty(h.Result);
        Assert.Equal(0, h.Queries);
        Assert.Equal(0, h.PatternReads);
    }

    [Fact]
    public void Patterns_are_read_on_every_call()
    {
        var h = new Harness();
        h.Add("Cmf.Custom.A", "1.0.0", new DateTime(2026, 1, 1)).Add("Site.Pkg", "2.0.0", new DateTime(2026, 1, 1));

        h.Run();
        Assert.Equal(new[] { "Cmf.Custom.A" }, h.Result.Select(r => r.Name));

        h.Patterns = "Site.%";
        h.Output = new() { MonitoringInformation = new MonitoringInformationCollection() };
        h.Run();

        Assert.Equal(new[] { "Site.Pkg" }, h.Result.Select(r => r.Name));
        Assert.Equal(2, h.PatternReads);
    }

    [Theory]
    [InlineData("Cmf.Custom.%;Site.%", 2)]
    [InlineData(" Cmf.Custom.% ; ; Site.% ", 2)]
    [InlineData("cmf.custom.%", 1)]
    [InlineData("Site.Pk_", 1)]
    [InlineData("Site.Pkg%", 1)]
    [InlineData("Site_Pkg", 1)]
    [InlineData("Site__Pkg", 0)]
    [InlineData("Cmf.Custom.A'; DROP TABLE x;--", 0)]
    [InlineData("Cmf.Custom.A (1)", 0)]
    public void Patterns_follow_sql_like_semantics(string patterns, int expected)
    {
        var h = new Harness { Patterns = patterns };
        h.Add("Cmf.Custom.A", "1.0.0", new DateTime(2026, 1, 1)).Add("Site.Pkg", "2.0.0", new DateTime(2026, 1, 1));

        h.Run();

        Assert.Equal(expected, h.Result.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(";")]
    [InlineData(" ; ; ")]
    public void Blank_patterns_show_nothing_and_do_not_query_the_view(string? patterns)
    {
        var h = new Harness { Patterns = patterns };
        h.Add("Cmf.Custom.A", "1.0.0", new DateTime(2026, 1, 1));

        h.Run();

        Assert.Empty(h.Result);
        Assert.Equal(0, h.Queries);
    }

    [Fact]
    public void Missing_config_entry_shows_nothing_and_does_not_query_the_view()
    {
        var h = new Harness();
        h.Add("Cmf.Custom.A", "1.0.0", new DateTime(2026, 1, 1));
        var input = h.Input();
        input.Remove("PackagePatternsReader"); // FakeServiceProvider has no IConfig: entry missing
        var dee = new CustomAppendInstalledCustomizationPackages();

        if (dee.DeeTestCondition(input)) dee.DeeActionCode(input);

        Assert.Empty(h.Result);
        Assert.Equal(0, h.Queries);
    }

    [Fact]
    public void Blank_items_between_patterns_are_ignored()
    {
        var h = new Harness { Patterns = "Cmf.Custom.A;;Cmf.Custom.B" };
        h.Add("Cmf.Custom.A", "1.0.0", new DateTime(2026, 1, 1)).Add("Cmf.Custom.B", "1.0.0", new DateTime(2026, 1, 1)).Add("Site.Pkg", "1.0.0", new DateTime(2026, 1, 1));

        h.Run();

        Assert.Equal(new[] { "Cmf.Custom.A", "Cmf.Custom.B" }, h.Result.Select(r => r.Name));
    }

    [Fact]
    public void No_match_leaves_product_rows_untouched()
    {
        var h = new Harness();
        h.Output.MonitoringInformation.Add(new MonitoringInformation(MonitoringContext.BusinessVersioning, MonitoringPath.Application, MonitoringPath.Versioning) { Name = "Product" });
        h.Add("Cmf.Environment", "11.3.5", new DateTime(2026, 1, 1));

        h.Run();

        Assert.Equal(new[] { "Product" }, h.Result.Select(r => r.Name));
    }

    [Fact]
    public void Query_failure_leaves_product_rows_and_does_not_throw()
    {
        var h = new Harness { QueryThrows = true };
        h.Output.MonitoringInformation.Add(new MonitoringInformation(MonitoringContext.BusinessVersioning, MonitoringPath.Application, MonitoringPath.Versioning) { Name = "Product" });

        var ex = Record.Exception(() => h.Run());

        Assert.Null(ex);
        Assert.Equal(new[] { "Product" }, h.Result.Select(r => r.Name));
    }

    [Fact]
    public void Missing_output_object_does_not_throw()
    {
        var h = new Harness();
        var input = h.Input();
        input.Remove("anyKey2");
        var dee = new CustomAppendInstalledCustomizationPackages();

        var ex = Record.Exception(() => { if (dee.DeeTestCondition(input)) dee.DeeActionCode(input); });

        Assert.Null(ex);
    }

    [Fact]
    public void Appended_rows_follow_the_existing_product_rows()
    {
        var h = new Harness();
        h.Output.MonitoringInformation.Add(new MonitoringInformation(MonitoringContext.BusinessVersioning, MonitoringPath.Application, MonitoringPath.Versioning) { Name = "Product" });
        h.Add("Cmf.Custom.A", "1.0.0", new DateTime(2026, 1, 1));

        h.Run();

        Assert.Equal(new[] { "Product", "Cmf.Custom.A" }, h.Result.Select(r => r.Name));
    }

    [Theory]
    [InlineData("Cmf.Custom.%", "cmf.custom.a", true)]
    [InlineData("Cmf.Custom.%", "Cmf.Custom.", true)]
    [InlineData("Cmf.Custom.%", "Xcmf.custom.a", false)]
    [InlineData("%.Data", "Cmf.Custom.Data", true)]
    [InlineData("%.Data", "Cmf.Custom.Data2", false)]
    [InlineData("%Custom%", "Cmf.Custom.A", true)]
    [InlineData("C_f", "Cmf", true)]
    [InlineData("C_f", "Cf", false)]
    [InlineData("C_f", "Cmmf", false)]
    [InlineData("a%b%c", "aXbYYc", true)]
    [InlineData("a%b%c", "aXbYYcZ", false)]
    [InlineData("%", "a", true)]
    [InlineData("Cmf.Custom", "CmfXCustom", false)]
    [InlineData("A(1)", "A(1)", true)]
    [InlineData("A(1)", "A1", false)]
    [InlineData("A$", "A$", true)]
    [InlineData("A$", "A", false)]
    [InlineData("^A", "A", false)]
    [InlineData("A[bc]", "Ab", false)]
    [InlineData("A[bc]", "A[bc]", true)]
    [InlineData("A.*", "Axx", false)]
    public void Like_matcher_semantics(string pattern, string id, bool expected)
    {
        var h = new Harness { Patterns = pattern };
        h.Add(id, "1.0.0", new DateTime(2026, 1, 1));

        h.Run();

        Assert.Equal(expected, h.Result.Count == 1);
    }

    [Fact]
    public void Request_with_null_MonitoringContexts_value_adds_rows()
    {
        var h = new Harness { Contexts = null };
        h.Add("Cmf.Custom.A", "1.0.0", new DateTime(2026, 1, 1));

        h.Run();

        Assert.Single(h.Result);
    }

    [Fact]
    public void Missing_request_or_unexpected_request_type_is_treated_as_all_contexts()
    {
        foreach (var request in new object?[] { null, new object() })
        {
            var h = new Harness();
            h.Add("Cmf.Custom.A", "1.0.0", new DateTime(2026, 1, 1));
            var input = h.Input();
            if (request == null) input.Remove("anyKey1"); else input["anyKey1"] = request;
            var dee = new CustomAppendInstalledCustomizationPackages();

            if (dee.DeeTestCondition(input)) dee.DeeActionCode(input);

            Assert.Single(h.Result);
        }
    }

    // Pins the live defect: the LBO metadata group name is never fired, the URL-derived group is.
    [Fact]
    public void Registration_group_matches_the_pattern_derived_from_the_service_url()
    {
        const string url = "api/ApplicationMonitoring/GetApplicationMonitoringInformation";
        var parts = url.Split('/');
        string expected = $"{parts[1]}Management.{parts[1]}ManagementOrchestration.{parts[2]}.Post";

        string file = FindDataFile("MasterData", "11361.0.0", "000-SystemMonitoringDEEs.json");

        var order = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file))["DEEActionOrder"]!.Children<Newtonsoft.Json.Linq.JProperty>().Select(p => p.Value).Single();
        Assert.Equal(expected, (string)order["ActionGroup"]!);
        Assert.Equal("CustomAppendInstalledCustomizationPackages", (string)order["Action"]!);
        Assert.Equal("1", (string)order["Order"]!);
        Assert.Contains("Action Groups: " + expected, File.ReadAllText(FindDeeSource()));
    }

    // Pins the live defect: the install-time DEE compiler does not resolve orchestration types (clean install #1 failed).
    [Fact]
    public void Dee_does_not_reference_orchestration_types()
    {
        string source = File.ReadAllText(FindDeeSource());
        Assert.DoesNotContain("GetApplicationMonitoringInformationOutput ", source.Replace("OutputTypeName", ""));
        Assert.DoesNotContain("using Cmf.Foundation.BusinessOrchestration", source);
    }

    [Fact]
    public void Nothing_usable_warns_listing_input_keys_and_value_types_but_no_values()
    {
        var h = new Harness();
        var input = h.Input();
        input.Remove("anyKey2");
        input["secretKey"] = "TOP-SECRET-VALUE";
        var dee = new CustomAppendInstalledCustomizationPackages();
        var original = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            Assert.Null(Record.Exception(() => { if (dee.DeeTestCondition(input)) dee.DeeActionCode(input); }));
        }
        finally { Console.SetError(original); }

        string message = writer.ToString();
        Assert.Contains("anyKey1", message);
        Assert.Contains(typeof(GetApplicationMonitoringInformationInput).FullName!, message);
        Assert.Contains("secretKey (System.String)", message);
        Assert.DoesNotContain("TOP-SECRET-VALUE", message);
        Assert.Equal(0, h.Queries);
    }

    private class RecordingLogging : DispatchProxy
    {
        public static readonly List<string> Warnings = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Warning" && args?.Length > 0 && args[0] is string text) Warnings.Add(text);
            return null;
        }
    }

    private sealed class LoggingServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(ILoggingService) ? DispatchProxy.Create<ILoggingService, RecordingLogging>() : null;
    }

    // Pins the live defect: NgpDataSet.FromQuery throws without a TotalRows column.
    [Fact]
    public void Installed_packages_statement_selects_TotalRows_like_platform_queries()
    {
        const string expected = "SELECT PackageId, PackageVersion, InstalledOn, COUNT(*) OVER() AS TotalRows FROM UserDataModel.V_InstalledPackage";
        Assert.Contains("const string InstalledPackagesQuery = \"" + expected + "\";", File.ReadAllText(FindDeeSource()));
    }

    [Fact]
    public void Extra_TotalRows_column_is_tolerated_and_columns_are_read_by_name()
    {
        var h = new Harness();
        // different column order plus the TotalRows column that FromQuery returns
        h.Rows = new DataTable();
        h.Rows.Columns.Add("TotalRows", typeof(int));
        h.Rows.Columns.Add("InstalledOn", typeof(DateTime));
        h.Rows.Columns.Add("PackageVersion", typeof(string));
        h.Rows.Columns.Add("PackageId", typeof(string));
        h.Rows.Rows.Add(1, new DateTime(2026, 3, 4, 5, 6, 7), "2.0.0", "Cmf.Custom.A");

        h.Run();

        var row = Assert.Single(h.Result);
        Assert.Equal("Cmf.Custom.A", row.Name);
        Assert.Equal("2.0.0", row.Value);
        Assert.Equal("2026-03-04 05:06:07", row.Text);
    }

    [Fact]
    public void Query_failure_warning_in_the_logging_service_carries_exception_type_and_message()
    {
        RecordingLogging.Warnings.Clear();
        var h = new Harness { QueryThrows = true };
        var input = h.Input();
        input["ServiceProvider"] = new LoggingServiceProvider();
        var dee = new CustomAppendInstalledCustomizationPackages();

        if (dee.DeeTestCondition(input)) dee.DeeActionCode(input);

        string warning = Assert.Single(RecordingLogging.Warnings);
        Assert.Contains(nameof(InvalidOperationException), warning);
        Assert.Contains("Invalid object name 'UserDataModel.V_InstalledPackage'", warning);
    }

    // walks up from the test output to the Data package and returns the file under it
    private static string FindDataFile(params string[] relativeParts)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(new[] { dir, "Cmf.Community.SystemMonitoringView.Data" }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException(Path.Combine(relativeParts));
    }

    private static string FindDeeSource() => FindDataFile("DEEs", "Monitoring", "CustomAppendInstalledCustomizationPackages.cs");

    [Fact]
    public void Output_without_MonitoringInformation_does_not_throw_or_query()
    {
        var h = new Harness();
        h.Output = new GetApplicationMonitoringInformationOutput();
        h.Add("Cmf.Custom.A", "1.0.0", new DateTime(2026, 1, 1));

        var ex = Record.Exception(() => h.Run());

        Assert.Null(ex);
        Assert.Equal(0, h.Queries);
    }

    // Pins the live defect: usings outside the install-time compiler references fail the install.
    [Fact]
    public void Dee_usings_stay_within_what_the_runtime_dee_compiler_resolves()
    {
        // The platform recompiles the DEE source with a restricted reference set: a using outside this list compiles here but fails at install.
        // Derived from the usings of the product DEE sources (Dee.T_Action) plus the core library namespaces.
        string[] allowed =
        {
            "System", "System.Collections.Generic", "System.Data", "System.Globalization", "System.Linq",
            "Cmf.Foundation.BusinessObjects", "Cmf.Foundation.Common", "Cmf.Foundation.Common.Logging",
            "Cmf.Foundation.Configuration.Abstractions", "Microsoft.Extensions.DependencyInjection",
        };
        string file = FindDeeSource();

        var offenders = File.ReadAllLines(file)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("using ") && l.EndsWith(";") && !l.Contains('('))
            .Select(l => l.Substring(6, l.Length - 7).Trim())
            .Where(ns => !allowed.Contains(ns))
            .ToList();

        Assert.Empty(offenders);
    }
}
