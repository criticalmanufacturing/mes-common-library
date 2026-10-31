using audit.Objects;
using Cmf.Cli.Plugin.Audit.Commands.install;
using Newtonsoft.Json.Linq;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace specs
{
    /// <summary>
    /// End-to-end runs of "audit init" over dummy projects: a bare one (no pipelines yet) and one with
    /// dummy CD-Containers pipelines, checking everything that gets generated.
    /// </summary>
    public class InitDummyPipelineTests
    {
        private static string CreateBareProject()
        {
            var root = Directory.CreateTempSubdirectory("cmf-audit-dummy").FullName;
            File.WriteAllText(Path.Combine(root, ".project-config.json"), """{ "ProjectName": "Dummy", "MESVersion": "11.3.0" }""");
            File.WriteAllText(Path.Combine(root, "cmfpackage.json"), InitTests.RootCmfPackage);
            return root;
        }

        private static string CreateProjectWithPipelines()
        {
            var root = CreateBareProject();
            var builds = Directory.CreateDirectory(Path.Combine(root, "Builds")).FullName;
            Directory.CreateDirectory(Path.Combine(builds, ".vars"));
            Directory.CreateDirectory(Path.Combine(builds, ".tasks"));
            File.WriteAllText(Path.Combine(builds, "CD-Containers.yml"), InitTests.CdContainers);
            File.WriteAllText(Path.Combine(builds, ".vars", "global.yml"), "variables:\n  BuildRunner: 'image'\n");
            File.WriteAllText(Path.Combine(builds, ".vars", "Env1.yml"), "variables:\n  A: b\n");
            return root;
        }

        private static InitArguments Args(bool includeInCdContainers) => new()
        {
            AzureDevOps = true,
            IncludeInCDContainers = includeInCdContainers,
            CmfAuditVersion = "1.2.3",
            CmfAuditRegistry = new Uri("https://registry.example/npm/"),
        };

        private static void AssertValidYaml(string folder)
        {
            foreach (var file in Directory.GetFiles(folder, "*.yml", SearchOption.AllDirectories))
            {
                using var reader = new StreamReader(file);
                var exception = Record.Exception(() => new YamlStream().Load(reader));
                Assert.True(exception == null, $"{file} is not valid YAML: {exception?.Message}");
            }
        }

        private static Dictionary<string, string> Snapshot(string folder) =>
            Directory.GetFiles(folder, "*", SearchOption.AllDirectories).OrderBy(f => f).ToDictionary(f => f, File.ReadAllText);

        [Fact]
        public void AuditInit_OnProjectWithoutPipelines_GeneratesStandalonePipelineThatInstallsTheAuditPackage()
        {
            var root = CreateBareProject();
            try
            {
                InitTests.RunInit(root, Args(includeInCdContainers: true));

                // Cmf.Audit is copied for the project's MES version, and is NOT a dependency of the root package
                var auditPackage = JObject.Parse(File.ReadAllText(Path.Combine(root, "Cmf.Audit", "cmfpackage.json")));
                Assert.Equal("11.3.0", auditPackage["dependencies"]!.Single(d => d["id"]!.Value<string>() == "Cmf.Environment")["version"]!.Value<string>());
                Assert.Equal(InitTests.RootCmfPackage, File.ReadAllText(Path.Combine(root, "cmfpackage.json")));

                var builds = Path.Combine(root, "Builds");
                foreach (var file in Init.AuditBuildFiles.Concat(Init.SharedBuildFiles))
                {
                    Assert.True(File.Exists(Path.Combine(builds, file)), file);
                }
                AssertValidYaml(builds);

                // standalone pipeline: assemble, install the package, then audit
                var cdAuditReview = File.ReadAllText(Path.Combine(builds, "CD-AuditReview.yml"));
                Assert.DoesNotContain("<%=", cdAuditReview);
                Assert.Contains("- template: .vars/global.yml", cdAuditReview);
                var assemble = cdAuditReview.IndexOf("template: .tasks/assembling-audit-package.yml", StringComparison.Ordinal);
                var install = cdAuditReview.IndexOf("PackageToInstall: '$(CmfAuditPackageToInstall)'", StringComparison.Ordinal);
                var audit = cdAuditReview.IndexOf("template: .tasks/audit-review.yml", StringComparison.Ordinal);
                Assert.True(assemble > 0 && assemble < install && install < audit);

                var installAudit = File.ReadAllText(Path.Combine(builds, ".tasks", "install-cmf-audit.yml"));
                Assert.Contains("CmfAuditVersion: '1.2.3'", installAudit);
                Assert.Contains("CmfAuditRegistry: 'https://registry.example/npm/'", installAudit);

                // the pipeline references .vars/global.yml, so it is created with the package to install
                Assert.Contains("CmfAuditPackageToInstall: 'Cmf.Audit@1.0.0'", File.ReadAllText(Path.Combine(builds, ".vars", "global.yml")));
                Assert.False(File.Exists(Path.Combine(builds, "CD-Containers.yml")));

                // running it again changes nothing
                var snapshot = Snapshot(builds);
                InitTests.RunInit(root, Args(includeInCdContainers: true));
                Assert.Equal(snapshot, Snapshot(builds));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void AuditInit_OnProjectWithCdContainers_MergesTheAuditReviewStage()
        {
            var root = CreateProjectWithPipelines();
            try
            {
                var builds = Path.Combine(root, "Builds");

                InitTests.RunInit(root, Args(includeInCdContainers: true));

                var cdContainers = File.ReadAllText(Path.Combine(builds, "CD-Containers.yml"));
                Assert.True(PipelineYamlMerger.IsAuditMerged(cdContainers));
                var installation = cdContainers.IndexOf("- stage: Installation", StringComparison.Ordinal);
                var auditReview = cdContainers.IndexOf("- stage: AuditReview", StringComparison.Ordinal);
                var restorePoint = cdContainers.IndexOf("- stage: RestorePoint", StringComparison.Ordinal);
                Assert.True(installation < auditReview && auditReview < restorePoint);
                Assert.Contains("template: .tasks/assembling-audit-package.yml", cdContainers);
                Assert.Contains("PackageToInstall: '$(CmfAuditPackageToInstall)'", cdContainers);
                Assert.Contains("  dependsOn: AuditReview", cdContainers);
                AssertValidYaml(builds);

                // the project's own variables are kept, and the package to install is declared next to them
                var globalYml = File.ReadAllText(Path.Combine(builds, ".vars", "global.yml"));
                Assert.Contains("BuildRunner: 'image'", globalYml);
                Assert.Contains("CmfAuditPackageToInstall: 'Cmf.Audit@1.0.0'", globalYml);
                Assert.True(File.Exists(Path.Combine(builds, "CD-AuditReview.yml")));
                Assert.Equal(InitTests.RootCmfPackage, File.ReadAllText(Path.Combine(root, "cmfpackage.json")));

                // running it again changes nothing
                var snapshot = Snapshot(builds);
                InitTests.RunInit(root, Args(includeInCdContainers: true));
                Assert.Equal(snapshot, Snapshot(builds));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void AuditInit_WithoutCdContainersIntegration_KeepsCdContainersAndStillDeclaresThePackage()
        {
            var root = CreateProjectWithPipelines();
            try
            {
                var builds = Path.Combine(root, "Builds");

                InitTests.RunInit(root, Args(includeInCdContainers: false));

                Assert.Equal(InitTests.CdContainers, File.ReadAllText(Path.Combine(builds, "CD-Containers.yml")));
                Assert.Contains("CmfAuditPackageToInstall: 'Cmf.Audit@1.0.0'", File.ReadAllText(Path.Combine(builds, ".vars", "global.yml")));
                Assert.True(File.Exists(Path.Combine(builds, "CD-AuditReview.yml")));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
