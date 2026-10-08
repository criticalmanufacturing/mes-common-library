using audit.Objects;
using Cmf.CLI.Core.Enums;
using Cmf.CLI.Core.Objects;
using Cmf.Cli.Plugin.Audit.Commands.install;
using Spectre.Console;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace specs
{
    public class DataAuditorTests
    {
        #region ColorLimitsPerContentType

        [Fact]
        public void ColorLimitsPerContentType_ExportedObjects_FollowsScale()
        {
            Assert.Equal(Color.DarkGreen, DataAuditor.ColorLimitsPerContentType(0, ContentType.ExportedObjects));
            Assert.Equal(Color.Green4, DataAuditor.ColorLimitsPerContentType(1, ContentType.ExportedObjects));
            Assert.Equal(Color.Green3, DataAuditor.ColorLimitsPerContentType(2, ContentType.ExportedObjects));
            Assert.Equal(Color.SeaGreen1, DataAuditor.ColorLimitsPerContentType(3, ContentType.ExportedObjects));
            Assert.Equal(Color.Red3, DataAuditor.ColorLimitsPerContentType(25, ContentType.ExportedObjects));
            Assert.Equal(Color.IndianRed, DataAuditor.ColorLimitsPerContentType(999, ContentType.ExportedObjects));
        }

        [Fact]
        public void ColorLimitsPerContentType_LowCountScale_AppliesToProcessRulesAndEntityTypes()
        {
            foreach (var contentType in new[] { ContentType.ProcessRulesPre, ContentType.ProcessRulesPost, ContentType.EntityTypes })
            {
                Assert.Equal(Color.DarkGreen, DataAuditor.ColorLimitsPerContentType(0, contentType));
                Assert.Equal(Color.Green4, DataAuditor.ColorLimitsPerContentType(1, contentType));
                Assert.Equal(Color.Orange3, DataAuditor.ColorLimitsPerContentType(2, contentType));
                Assert.Equal(Color.Red3, DataAuditor.ColorLimitsPerContentType(6, contentType));
                Assert.Equal(Color.IndianRed, DataAuditor.ColorLimitsPerContentType(100, contentType));
            }
        }

        [Fact]
        public void ColorLimitsPerContentType_UnknownContentType_ReturnsIndianRed()
        {
            var color = DataAuditor.ColorLimitsPerContentType(0, ContentType.Documents);

            Assert.Equal(Color.IndianRed, color);
        }

        #endregion

        #region ExtractTypeAndName

        [Fact]
        public void ExtractTypeAndName_ParsesTypeAndNameAttribute()
        {
            var xml = """
                <Objects>
                  <Object type="Cmf.Foundation.BusinessObjects.EntityType, Cmf.Foundation.BusinessObjects">
                    <Name value="MyEntity" />
                  </Object>
                </Objects>
                """;

            (string type, string name) = DataAuditor.ExtractTypeAndName(xml);

            Assert.Equal("Cmf.Foundation.BusinessObjects.EntityType", type);
            Assert.Equal("MyEntity", name);
        }

        [Fact]
        public void ExtractTypeAndName_FallsBackToDescendantName()
        {
            var xml = """
                <Objects>
                  <Object type="SomeType, SomeAssembly">
                    <Properties>
                      <Name value="NestedName" />
                    </Properties>
                  </Object>
                </Objects>
                """;

            (string type, string name) = DataAuditor.ExtractTypeAndName(xml);

            Assert.Equal("SomeType", type);
            Assert.Equal("NestedName", name);
        }

        [Fact]
        public void ExtractTypeAndName_NoObjectElement_ReturnsNulls()
        {
            (string type, string name) = DataAuditor.ExtractTypeAndName("<Root></Root>");

            Assert.Null(type);
            Assert.Null(name);
        }

        #endregion

        #region PackageMetadataSummary

        [Fact]
        public void PackageMetadataSummary_IncludesPackageAndContentDetails()
        {
            var package = TestSupport.CreateCmfPackage(
                "Cmf.Custom.Data",
                PackageType.Data,
                version: "2.1.0",
                contentToPack:
                [
                    new ContentToPack { Source = "Data/ExportedObjects/*.xml", ContentType = ContentType.ExportedObjects }
                ]);

            var summary = DataAuditor.PackageMetadataSummary(package);

            Assert.Equal("Cmf.Custom.Data", summary["PackageId"]);
            Assert.Equal("2.1.0", summary["PackageVersion"]);
            Assert.Equal("0", summary["Number of First Level Dependencies"]);
            Assert.Equal(ContentType.ExportedObjects.ToString(), summary["Data/ExportedObjects/*.xml"]);
        }

        #endregion

        #region AuditorExportedFiles

        [Fact]
        public void AuditorExportedFiles_GroupsByObjectTypeAndRecordsRows()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile("/pkg/Foo.xml", new MockFileData("""
                <Objects><Object type="TypeA, Assembly"><Name value="Foo" /></Object></Objects>
                """));
            fileSystem.AddFile("/pkg/Bar.xml", new MockFileData("""
                <Objects><Object type="TypeA, Assembly"><Name value="Bar" /></Object></Objects>
                """));
            fileSystem.AddFile("/pkg/Baz.xml", new MockFileData("""
                <Objects><Object type="TypeB, Assembly"><Name value="Baz" /></Object></Objects>
                """));

            var files = new[]
            {
                fileSystem.FileInfo.New("/pkg/Foo.xml"),
                fileSystem.FileInfo.New("/pkg/Bar.xml"),
                fileSystem.FileInfo.New("/pkg/Baz.xml"),
            };

            var package = TestSupport.CreateCmfPackage("Cmf.Custom.Data", PackageType.Data);
            var recordedRows = new List<(string type, string name)>();
            var reporters = new Reporters();
            reporters.RowAdder = (type, name, metadata) => recordedRows.Add((type, name));

            var result = DataAuditor.AuditorExportedFiles(files, package, reporters);

            Assert.Equal(2, result["TypeA"]);
            Assert.Equal(1, result["TypeB"]);
            Assert.Equal(3, recordedRows.Count);
            Assert.All(recordedRows, row => Assert.Equal("Exported Objects", row.type));
        }

        #endregion

        #region ExtractValuesFromJSONObject

        [Fact]
        public void ExtractValuesFromJSONObject_ExtractsOnlyRequestedNonEmptyProperties()
        {
            using var doc = System.Text.Json.JsonDocument.Parse("""{"Name": "Item1", "Action": "", "Extra": "ignored"}""");

            var values = DataAuditor.ExtractValuesFromJSONObject(doc.RootElement, ["Name", "Action", "Missing"]);

            Assert.Equal("Item1", values["Name"]);
            Assert.False(values.ContainsKey("Action"));
            Assert.False(values.ContainsKey("Missing"));
        }

        #endregion
    }
}
