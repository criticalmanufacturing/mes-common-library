using audit.Objects;
using Cmf.Cli.Plugin.Audit.Commands.install;
using Cmf.CLI.Core.Objects;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System.IO.Abstractions.TestingHelpers;
using Xunit;
using ExecutionContext = Cmf.CLI.Core.Objects.ExecutionContext;

namespace specs
{
    public class InitTests
    {
        internal const string CdContainers = """
            # CM DS Continuous Deployment Containers Pipeline
            pool:
              name: MyPool

            resources:
              containers:
              - container: build-runner
                image: $(BuildRunner)
                endpoint: my-connection
                options: --volume /mnt:/mnt

            variables:
            - template: .vars/global.yml
            - template: .vars/${{ parameters.Environment }}.yml
            - name: Foo
              value: bar
            - name: IsProdBranch
              value: false
            - name: EnvironmentCustomerPortalPAT
              value: x

            # Setted in runtime
            parameters:
            - name: Environment
              displayName: Environment name
              type: string
              default: Env2
              values:
                - Env1
                - Env2
            - name: executeAllStages
              type: boolean
              default: true
            - name: RunTests
              displayName: Run Tests
              type: boolean
              default: false
            # Documents the next parameter
            - name: ManualApproval
              type: boolean
              default: false

            stages:
            - stage: RunTests
              jobs:
              - job: RunTests
                steps:
                - checkout: none

            - stage: Installation
              jobs: []

            - stage: RestorePoint
              dependsOn: Installation
              condition: |
                and(
                  or(
                    and(eq('${{ parameters.ExecuteAllStages }}', true), eq(dependencies.Installation.result, 'Succeeded'))
                  ),
                  not(failed())
                )
              jobs: []

            - stage: ManualApproval
              dependsOn: RunTests
              jobs: []
            """;

        #region PipelineYamlMerger readers

        [Fact]
        public void Readers_ReadProjectSettingsFromCdContainers()
        {
            Assert.Equal("MyPool", PipelineYamlMerger.ReadPoolName(CdContainers));
            Assert.Equal("my-connection", PipelineYamlMerger.ReadBuildRunnerProperty(CdContainers, "endpoint"));
            Assert.Equal("--volume /mnt:/mnt", PipelineYamlMerger.ReadBuildRunnerProperty(CdContainers, "options"));
            Assert.True(PipelineYamlMerger.HasStage(CdContainers, "RunTests"));
            Assert.True(PipelineYamlMerger.HasParameter(CdContainers, "ExecuteAllStages"));
            Assert.False(PipelineYamlMerger.HasParameter(CdContainers, "Foo"));

            var (values, defaultValue) = PipelineYamlMerger.ReadEnvironmentParameter(CdContainers);
            Assert.Equal(["Env1", "Env2"], values);
            Assert.Equal("Env2", defaultValue);
        }

        [Theory]
        [InlineData("parameters:\n- name: Environment\n  type: string\nstages: []", "${{ parameters.Environment }}")]
        [InlineData("variables:\n- name: CustomerEnvironmentCodename\n  value: x\nstages: []", "$(CustomerEnvironmentCodename)")]
        [InlineData("stages:\n- stage: A\n  variables:\n    Environment: $(CustomerEnvironmentName)", "$(CustomerEnvironmentName)")]
        [InlineData("stages: []", null)]
        public void ResolveEnvironmentExpression_SupportsAllCdContainersFlavours(string pipeline, string? expected)
        {
            Assert.Equal(expected, PipelineYamlMerger.ResolveEnvironmentExpression(pipeline));
        }

        #endregion

        #region PipelineYamlMerger writers

        [Fact]
        public void MergeIntoCdContainers_AddsStageAfterInstallationAndChainsRestorePoint()
        {
            var merged = PipelineYamlMerger.MergeIntoCdContainers(CdContainers, "${{ parameters.Environment }}");
            var lines = merged.Split('\n').ToList();

            // stage between Installation and RestorePoint
            var audit = lines.IndexOf("- stage: AuditReview");
            Assert.True(audit > lines.IndexOf("- stage: Installation"));
            Assert.True(audit < lines.IndexOf("- stage: RestorePoint"));
            Assert.Contains("      eq(variables['IsProdBranch'], 'False'),", lines);
            Assert.Contains("        Environment: ${{ parameters.Environment }}", lines);
            Assert.Contains("    - template: .tasks/assembling-audit-package.yml", lines);
            Assert.Contains("        PackageToInstall: '$(CmfAuditPackageToInstall)'", lines);
            Assert.Contains("    - template: .tasks/audit-review.yml", lines);
            Assert.DoesNotContain(lines, line => line.Contains("timeoutInMinutes"));

            // RestorePoint waits for the audit instead of the installation
            var restoreLines = lines.Skip(lines.IndexOf("- stage: RestorePoint")).ToList();
            Assert.Contains("  dependsOn: AuditReview", restoreLines);
            Assert.Contains("        and(eq('${{ parameters.ExecuteAllStages }}', true), in(dependencies.AuditReview.result, 'Succeeded', 'SucceededWithIssues'))", restoreLines);
            Assert.True(PipelineYamlMerger.IsAuditMerged(merged));

            // idempotent
            Assert.Equal(merged, PipelineYamlMerger.MergeIntoCdContainers(merged, "${{ parameters.Environment }}"));
        }

        [Fact]
        public void MergeIntoCdContainers_WithoutGlobalYml_DeclaresAuditPackageVariable()
        {
            var merged = PipelineYamlMerger.MergeIntoCdContainers(CdContainers, "$(CustomerEnvironmentName)", "Cmf.Audit@1.0.0");

            Assert.Contains("- name: CmfAuditPackageToInstall\n  value: 'Cmf.Audit@1.0.0'", merged);
        }

        [Fact]
        public void MergeIntoCdContainers_PreservesCrLf()
        {
            var merged = PipelineYamlMerger.MergeIntoCdContainers(CdContainers.Replace("\n", "\r\n"), "$(CustomerEnvironmentName)");

            Assert.DoesNotContain("\n", merged.Replace("\r\n", ""));
        }

        [Theory]
        [InlineData("variables:\n  # comment\n  Foo: bar\n\n  Baz: 1\n", "variables:\n  # comment\n  Foo: bar\n\n  Baz: 1\n  # Toggle\n  RunAuditReview: false\n")]
        [InlineData("variables:\n- name: Foo\n  value: bar\n", "variables:\n- name: Foo\n  value: bar\n# Toggle\n- name: RunAuditReview\n  value: false\n")]
        [InlineData("variables:\n  RunAuditReview: true\n", "variables:\n  RunAuditReview: true\n")]
        public void AddVariable_AppendsOnlyWhenMissing(string variablesFile, string expected)
        {
            Assert.Equal(expected, PipelineYamlMerger.AddVariable(variablesFile, "RunAuditReview", "false", "Toggle"));
        }

        [Fact]
        public void ReadVariable_ReadsMappingVariables()
        {
            const string globalYml = "variables:\n  AgentPool: Linux # pool\n  CmfCliVersion: '5.x.x'\n  Empty: ''\n";

            Assert.Equal("Linux", PipelineYamlMerger.ReadVariable(globalYml, "AgentPool"));
            Assert.Equal("5.x.x", PipelineYamlMerger.ReadVariable(globalYml, "CmfCliVersion"));
            Assert.Null(PipelineYamlMerger.ReadVariable(globalYml, "Empty"));
            Assert.Null(PipelineYamlMerger.ReadVariable(globalYml, "Missing"));
        }

        #endregion

        #region Init command

        #region AlignMesDependencies

        [Fact]
        public void AlignMesDependencies_AlignsOnlyDependenciesOnTheMesMinorVersion()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                ["/pkg/cmfpackage.json"] = """
                    { "packageId": "Cmf.Audit", "version": "1.0.0", "dependencies": [
                      { "id": "Cmf.Environment", "version": "11.3.5", "mandatory": false },
                      { "id": "Cmf.Audit.Grafana", "version": "1.0.0" },
                      { "id": "Other", "version": "11.2.1" } ] }
                    """,
                ["/pkg/Sub/cmfpackage.json"] = """{ "packageId": "Sub", "version": "1.0.0", "contentToPack": [] }""",
            });

            Init.AlignMesDependencies(fileSystem, fileSystem.DirectoryInfo.New("/pkg"), new Version(11, 3, 7));

            var dependencies = JObject.Parse(fileSystem.File.ReadAllText("/pkg/cmfpackage.json"))["dependencies"]!
                .ToDictionary(dependency => dependency["id"]!.Value<string>()!, dependency => dependency["version"]!.Value<string>());
            Assert.Equal("11.3.7", dependencies["Cmf.Environment"]);
            Assert.Equal("1.0.0", dependencies["Cmf.Audit.Grafana"]);
            Assert.Equal("11.2.1", dependencies["Other"]);
            Assert.Equal("""{ "packageId": "Sub", "version": "1.0.0", "contentToPack": [] }""", fileSystem.File.ReadAllText("/pkg/Sub/cmfpackage.json"));
        }

        #endregion

        internal const string RootCmfPackage = """
            {
              "packageId": "Cmf.Custom.Package",
              "version": "1.0.0",
              "packageType": "Root",
              "dependencies": [
                { "id": "Cmf.Custom.Business", "version": "1.0.0" }
              ]
            }
            """;

        private static string CreateProject(bool withBuilds, string mesVersion = "11.3.7")
        {
            var root = Directory.CreateTempSubdirectory("cmf-audit-init-tests").FullName;
            File.WriteAllText(Path.Combine(root, ".project-config.json"), $$"""{ "ProjectName": "Test", "MESVersion": "{{mesVersion}}" }""");
            File.WriteAllText(Path.Combine(root, "cmfpackage.json"), RootCmfPackage);

            if (withBuilds)
            {
                Directory.CreateDirectory(Path.Combine(root, "Builds", ".vars"));
                Directory.CreateDirectory(Path.Combine(root, "Builds", ".tasks"));
                File.WriteAllText(Path.Combine(root, "Builds", "CD-Containers.yml"), CdContainers);
                File.WriteAllText(Path.Combine(root, "Builds", ".vars", "global.yml"), "variables:\n  BuildRunner: 'image'\n");
                File.WriteAllText(Path.Combine(root, "Builds", ".vars", "Env1.yml"), "variables:\n  A: b\n");
                File.WriteAllText(Path.Combine(root, "Builds", ".tasks", "use-node-version.yml"), "# project version\n");
            }

            return root;
        }

        internal static void RunInit(string root, InitArguments args)
        {
            var cwd = Directory.GetCurrentDirectory();
            // Init refuses to run on GitHub-hosted projects (including inside GitHub Actions), so neutralise it for the tests.
            var githubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
            try
            {
                Environment.SetEnvironmentVariable("GITHUB_ACTIONS", null);
                Directory.SetCurrentDirectory(root);
                ExecutionContext.Initialize(new System.IO.Abstractions.FileSystem());
                ExecutionContext.ServiceProvider = new ServiceCollection()
                    .AddSingleton<IVersionService>(new VersionService("@criticalmanufacturing/audit"))
                    .AddSingleton<IProjectConfigService>(new ProjectConfigService())
                    .BuildServiceProvider();
                TestSupport.GetLogStringWriter();
                new Init().Execute(args);
            }
            finally
            {
                Directory.SetCurrentDirectory(cwd);
                Environment.SetEnvironmentVariable("GITHUB_ACTIONS", githubActions);
            }
        }

        [Fact]
        public void Init_AzureDevOps_GeneratesPipelinesAndMergesCdContainers()
        {
            var root = CreateProject(withBuilds: true);
            try
            {
                RunInit(root, new InitArguments
                {
                    AzureDevOps = true,
                    IncludeInCDContainers = true,
                    CmfAuditVersion = "1.2.3",
                    CmfAuditRegistry = new Uri("https://cm-collaborationhub.io/api/npm/feed/"),
                });

                var builds = Path.Combine(root, "Builds");
                Assert.True(File.Exists(Path.Combine(root, Init.AuditPackageName, "cmfpackage.json")));
                foreach (var file in Init.AuditBuildFiles.Concat(Init.SharedBuildFiles))
                {
                    Assert.True(File.Exists(Path.Combine(builds, file)), file);
                }

                // shared tasks the project already had are kept
                Assert.Equal("# project version\n", File.ReadAllText(Path.Combine(builds, ".tasks", "use-node-version.yml")));

                // values resolved from the project's own pipelines, no template tokens left behind
                var auditReview = File.ReadAllText(Path.Combine(builds, "CD-AuditReview.yml"));
                Assert.DoesNotContain("<%=", auditReview);
                Assert.Contains("  name: MyPool", auditReview);
                Assert.Contains("    endpoint: my-connection", auditReview);
                Assert.Contains("    options: '--volume /mnt:/mnt'", auditReview);
                Assert.Contains("  default: Env2\n  values:\n    - Env1\n    - Env2\n", auditReview);
                Assert.Contains("    - template: .tasks/assembling-audit-package.yml", auditReview);
                Assert.Contains("PackageToInstall: '$(CmfAuditPackageToInstall)'", auditReview);

                var installAudit = File.ReadAllText(Path.Combine(builds, ".tasks", "install-cmf-audit.yml"));
                Assert.Contains("CmfAuditVersion: '1.2.3'", installAudit);
                Assert.Contains("CmfAuditRegistry: 'https://cm-collaborationhub.io/api/npm/feed/'", installAudit);
                Assert.DoesNotContain("login --token", installAudit);

                Assert.True(PipelineYamlMerger.IsAuditMerged(File.ReadAllText(Path.Combine(builds, "CD-Containers.yml"))));
                Assert.Contains("CmfAuditPackageToInstall: 'Cmf.Audit@1.0.0'", File.ReadAllText(Path.Combine(builds, ".vars", "global.yml")));

                // the audit package has its own lifecycle: not a dependency of the root package
                Assert.Equal(RootCmfPackage, File.ReadAllText(Path.Combine(root, "cmfpackage.json")));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void Init_WithoutCdContainersIntegration_LeavesCdContainersUntouched()
        {
            var root = CreateProject(withBuilds: true);
            try
            {
                RunInit(root, new InitArguments { AzureDevOps = true, IncludeInCDContainers = false, CmfAuditRegistry = new Uri("https://registry.npmjs.com/") });

                var builds = Path.Combine(root, "Builds");
                Assert.True(File.Exists(Path.Combine(builds, "CD-AuditReview.yml")));
                Assert.Equal(CdContainers, File.ReadAllText(Path.Combine(builds, "CD-Containers.yml")));
                Assert.Contains("CmfAuditPackageToInstall: 'Cmf.Audit@1.0.0'", File.ReadAllText(Path.Combine(builds, ".vars", "global.yml")));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void Init_NotAzureDevOps_OnlyCopiesAuditPackage()
        {
            var root = CreateProject(withBuilds: false);
            try
            {
                RunInit(root, new InitArguments { AzureDevOps = false });

                Assert.False(Directory.Exists(Path.Combine(root, "Builds")));

                // the package matching the MES minor version, with its MES dependencies aligned with the project's patch
                var auditPackage = JObject.Parse(File.ReadAllText(Path.Combine(root, Init.AuditPackageName, "cmfpackage.json")));
                Assert.Equal("11.3.7", auditPackage["dependencies"]!.Single(dependency => dependency["id"]!.Value<string>() == "Cmf.Environment")["version"]!.Value<string>());

                // not registered in the root package
                Assert.Equal(RootCmfPackage, File.ReadAllText(Path.Combine(root, "cmfpackage.json")));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void Init_UnsupportedMesVersion_Throws()
        {
            var root = CreateProject(withBuilds: false, mesVersion: "9.0.1");
            try
            {
                var exception = Assert.Throws<Cmf.CLI.Utilities.CliException>(() => RunInit(root, new InitArguments { AzureDevOps = false }));

                Assert.Contains("not available for MES 9.0. Supported MES versions: 11.3", exception.Message);
                Assert.False(Directory.Exists(Path.Combine(root, Init.AuditPackageName)));
                Assert.Equal(RootCmfPackage, File.ReadAllText(Path.Combine(root, "cmfpackage.json")));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        #endregion
    }
}
