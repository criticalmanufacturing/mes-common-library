using audit.Objects;
using Cmf.CLI.Core.Enums;
using Cmf.Cli.Plugin.Audit.Commands.install;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace specs
{
    public class IoTAuditorTests
    {
        #region ParseTaskClass

        [Fact]
        public void ParseTaskClass_ParsesClassNameInjectsInputsAndOutputs()
        {
            string[] lines =
            [
                "@Task.Task()",
                "export class SampleTask implements ITaskSettings {",
                "  @DI.Inject(SomeService)",
                "  private someService: SomeService;",
                "",
                "  @Task.InputProperty()",
                "  public inputA: string;",
                "",
                "  @Task.OutputProperty()",
                "  public outputB: number;",
                "}"
            ];

            var task = IoTAuditor.ParseTaskClass(lines);

            Assert.Equal("SampleTask", task.ClassName);
            Assert.Equal(["SomeService"], task.Injects);
            Assert.Equal(["inputA"], task.Inputs);
            Assert.Equal(["outputB"], task.Outputs);
        }

        [Fact]
        public void ParseTaskClass_ClassWithoutInterface_StillParsesName()
        {
            string[] lines =
            [
                "@Task.Task()",
                "export class PlainTask {",
                "}"
            ];

            var task = IoTAuditor.ParseTaskClass(lines);

            Assert.Equal("PlainTask", task.ClassName);
        }

        #endregion

        #region ParseConverterClass

        [Fact]
        public void ParseConverterClass_NoDesigner_ParsesClassNameAndInjects()
        {
            string[] lines =
            [
                "@Converter.Converter()",
                "export class SampleConverter implements IConverterSettings {",
                "  @DI.Inject(SomeService)",
                "  private someService: SomeService;",
                "}"
            ];

            var converter = IoTAuditor.ParseConverterClass(lines, designer: null);

            Assert.Equal("SampleConverter", converter.ClassName);
            Assert.Equal(["SomeService"], converter.Injects);
            Assert.Empty(converter.Parameters);
        }

        [Fact]
        public void ParseConverterClass_WithDesigner_ExtractsParameters()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile("/pkg/Sample-designer.ts", new MockFileData(
                "export const designer = {\n" +
                "    parameters: {\n" +
                "        param1: \"value1\",\n" +
                "        param2: \"value2\",\n" +
                "    }\n" +
                "};\n"));
            var designer = fileSystem.FileInfo.New("/pkg/Sample-designer.ts");

            string[] lines =
            [
                "@Converter.Converter()",
                "export class SampleConverter {",
                "}"
            ];

            var converter = IoTAuditor.ParseConverterClass(lines, designer);

            Assert.Equal("SampleConverter", converter.ClassName);
            Assert.Equal(["param1", "param2"], converter.Parameters);
        }

        #endregion

        #region ResolveCurrentWorkingDirForContent

        [Fact]
        public void ResolveCurrentWorkingDirForContent_PlainPath_DescendsDirectories()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory("/root/a/b");
            var workingDir = fileSystem.DirectoryInfo.New("/root");

            var resolved = IoTAuditor.ResolveCurrentWorkingDirForContent(workingDir, "a/b");

            Assert.Equal(fileSystem.Path.GetFullPath("/root/a/b"), resolved.FullName);
        }

        [Fact]
        public void ResolveCurrentWorkingDirForContent_WildcardSegment_IsSkipped()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory("/root/a/c");
            var workingDir = fileSystem.DirectoryInfo.New("/root");

            var resolved = IoTAuditor.ResolveCurrentWorkingDirForContent(workingDir, "a/*/c");

            Assert.Equal(fileSystem.Path.GetFullPath("/root/a/c"), resolved.FullName);
        }

        [Fact]
        public void ResolveCurrentWorkingDirForContent_DotDot_GoesToParent()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory("/root/a");
            var workingDir = fileSystem.DirectoryInfo.New("/root");

            var resolved = IoTAuditor.ResolveCurrentWorkingDirForContent(workingDir, "a/../a");

            Assert.Equal(fileSystem.Path.GetFullPath("/root/a"), resolved.FullName);
        }

        [Fact]
        public void ResolveCurrentWorkingDirForContent_MissingDirectory_ReturnsNull()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory("/root/a");
            var workingDir = fileSystem.DirectoryInfo.New("/root");

            var resolved = IoTAuditor.ResolveCurrentWorkingDirForContent(workingDir, "a/missing");

            Assert.Null(resolved);
        }

        #endregion

        #region PackageMetadataSummary

        [Fact]
        public void PackageMetadataSummary_IncludesPackageIdVersionAndContentToPack()
        {
            var package = TestSupport.CreateCmfPackage("Cmf.Custom.IoT.Packages", PackageType.IoT, version: "3.0.0");

            var summary = IoTAuditor.PackageMetadataSummary(package);

            Assert.Equal("Cmf.Custom.IoT.Packages", summary["PackageId"]);
            Assert.Equal("3.0.0", summary["PackageVersion"]);
            Assert.Equal("0", summary["Number of First Level Dependencies"]);
        }

        #endregion

        #region PackageJsonAuditor

        [Fact]
        public void PackageJsonAuditor_ExtractsNameVersionAndDependencies()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile("/pkg/package.json", new MockFileData("""
                {
                    "name": "@my-scope/my-package",
                    "version": "1.2.3",
                    "dependencies": { "lodash": "^4.0.0" },
                    "devDependencies": { "typescript": "^5.0.0" }
                }
                """));
            var files = new[] { fileSystem.FileInfo.New("/pkg/package.json") };

            var package = TestSupport.CreateCmfPackage("Cmf.Custom.IoT.Packages", PackageType.IoT);
            var recordedRows = new List<(string type, string name, Dictionary<string, string> metadata)>();
            var reporters = new Reporters();
            reporters.RowAdder = (type, name, metadata) => recordedRows.Add((type, name, metadata));

            IoTAuditor.PackageJsonAuditor(package, reporters, files);

            Assert.Single(reporters.PanelReports);
            Assert.Contains(recordedRows, r => r.type == "@my-scope/my-package@1.2.3" && r.name == "dependencies" && r.metadata["lodash"] == "^4.0.0");
            Assert.Contains(recordedRows, r => r.type == "@my-scope/my-package@1.2.3" && r.name == "devDependencies" && r.metadata["typescript"] == "^5.0.0");
        }

        #endregion

        #region TypescriptAuditor

        [Fact]
        public void TypescriptAuditor_BundledTask_IsDetectedAndReported()
        {
            var fileSystem = new MockFileSystem();
            var taskLines =
                "@Task.Task()\n" +
                "export class SampleTask implements ITaskSettings {\n" +
                "  @DI.Inject(SomeService)\n" +
                "  private someService: SomeService;\n" +
                "\n" +
                "  @Task.InputProperty()\n" +
                "  public inputA: string;\n" +
                "}\n";
            fileSystem.AddFile("/pkg/Tasks/SampleTask/SampleTask.ts", new MockFileData(taskLines));
            fileSystem.AddFile("/pkg/Tasks/SampleTask/SampleTask.html", new MockFileData("<div></div>"));
            var files = new[] { fileSystem.FileInfo.New("/pkg/Tasks/SampleTask/SampleTask.ts") };

            var package = TestSupport.CreateCmfPackage("Cmf.Custom.IoT.Packages", PackageType.IoT);
            var recordedRows = new List<(string type, string name)>();
            var reporters = new Reporters();
            reporters.RowAdder = (type, name, metadata) => recordedRows.Add((type, name));

            IoTAuditor.TypescriptAuditor(package, reporters, files);

            Assert.Contains(recordedRows, r => r.type == "Task" && r.name == "SampleTask");
            Assert.Contains(reporters.TableReports, r => true);
        }

        [Fact]
        public void TypescriptAuditor_AtlConverterWithoutDesigner_IsDetectedAsAtl()
        {
            var fileSystem = new MockFileSystem();
            var converterLines =
                "@Converter.Converter()\n" +
                "export class SampleConverter implements IConverterSettings {\n" +
                "  @DI.Inject(SomeService)\n" +
                "  private someService: SomeService;\n" +
                "}\n";
            fileSystem.AddFile("/pkg/Converters/SampleConverter/SampleConverter.ts", new MockFileData(converterLines));
            var files = new[] { fileSystem.FileInfo.New("/pkg/Converters/SampleConverter/SampleConverter.ts") };

            var package = TestSupport.CreateCmfPackage("Cmf.Custom.IoT.Packages", PackageType.IoT);
            var recordedRows = new List<(string type, string name)>();
            var reporters = new Reporters();
            reporters.RowAdder = (type, name, metadata) => recordedRows.Add((type, name));

            IoTAuditor.TypescriptAuditor(package, reporters, files);

            Assert.Contains(recordedRows, r => r.type == "Converter" && r.name == "SampleConverter");
        }

        #endregion
    }
}
