using Cmf.CLI.Core.Enums;
using Cmf.CLI.Core.Objects;
using Cmf.CLI.Utilities;
using Cmf.Cli.Plugin.Audit.Commands.install;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace specs
{
    public class AuditTests
    {
        private static List<Func<IDirectoryInfo, string, Task>> Validators(List<string> calls) =>
            [(dir, packageId) => { calls.Add(packageId); return Task.CompletedTask; }];

        private static CmfPackage CreatePackage(IFileSystem fileSystem, string path, string packageId, PackageType packageType, DependencyCollection dependencies = null) =>
            TestSupport.CreateCmfPackage(packageId, packageType, dependencies: dependencies, fileSystem: fileSystem, path: path);

        [Fact]
        public void AddValidatorIfSelected_NoFilter_RegistersEveryPackageType()
        {
            var validatorsByType = new Dictionary<PackageType, List<Func<IDirectoryInfo, string, Task>>>();

            Review.AddValidatorIfSelected(validatorsByType, "", PackageType.Data, Validators([]));

            Assert.True(validatorsByType.ContainsKey(PackageType.Data));
        }

        [Fact]
        public void AddValidatorIfSelected_FilterExcludesType_DoesNotRegister()
        {
            var validatorsByType = new Dictionary<PackageType, List<Func<IDirectoryInfo, string, Task>>>();

            Review.AddValidatorIfSelected(validatorsByType, "IoT,Data", PackageType.Root, Validators([]));

            Assert.False(validatorsByType.ContainsKey(PackageType.Root));
        }

        [Fact]
        public void AddValidatorIfSelected_FilterIncludesType_Registers()
        {
            var validatorsByType = new Dictionary<PackageType, List<Func<IDirectoryInfo, string, Task>>>();

            Review.AddValidatorIfSelected(validatorsByType, "IoT,Data", PackageType.Data, Validators([]));

            Assert.True(validatorsByType.ContainsKey(PackageType.Data));
        }

        [Fact]
        public async Task BuildTaskTree_RunsRegisteredValidatorForMatchingDependency()
        {
            var fileSystem = new MockFileSystem();
            var leaf = CreatePackage(fileSystem, "/leaf/cmfpackage.json", "Leaf", PackageType.Data);
            var root = CreatePackage(fileSystem, "/root/cmfpackage.json", "Root", PackageType.Generic,
                dependencies: [new Dependency("Leaf", "1.0.0") { CmfPackage = leaf }]);

            var calledForPackageIds = new List<string>();
            var validatorsRegistered = new Dictionary<PackageType, List<Func<IDirectoryInfo, string, Task>>>
            {
                [PackageType.Data] = Validators(calledForPackageIds)
            };

            (List<Task> tasks, List<Dependency> unaudited) = Review.BuildTaskTree(root, validatorsRegistered);
            await Task.WhenAll(tasks);

            Assert.Single(tasks);
            Assert.Equal(["Leaf"], calledForPackageIds);
            Assert.Null(unaudited);
        }

        [Fact]
        public async Task BuildTaskTree_MissingDependency_IsReportedAsUnaudited()
        {
            var fileSystem = new MockFileSystem();
            var root = CreatePackage(fileSystem, "/root/cmfpackage.json", "Root", PackageType.Generic,
                dependencies: [new Dependency("Missing", "2.0.0")]);

            (List<Task> tasks, List<Dependency> unaudited) = Review.BuildTaskTree(root, []);
            await Task.WhenAll(tasks);

            Assert.Empty(tasks);
            Assert.Single(unaudited);
            Assert.Equal("Missing", unaudited[0].Id);
        }

        [Fact]
        public async Task BuildTaskTree_NoValidatorsRegisteredForPackageType_RunsNothing()
        {
            var fileSystem = new MockFileSystem();
            var root = CreatePackage(fileSystem, "/root/cmfpackage.json", "Root", PackageType.Root);

            var validatorsRegistered = new Dictionary<PackageType, List<Func<IDirectoryInfo, string, Task>>>
            {
                [PackageType.Data] = Validators([])
            };

            (List<Task> tasks, List<Dependency> unaudited) = Review.BuildTaskTree(root, validatorsRegistered);
            await Task.WhenAll(tasks);

            Assert.Empty(tasks);
            Assert.Null(unaudited);
        }

        [Fact]
        public async Task ExecuteAsync_NoCmfPackageJson_ThrowsWithReviewRemoteHint()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddDirectory("/workdir");
            var workingDir = fileSystem.DirectoryInfo.New("/workdir");
            var command = new Review(fileSystem);

            var ex = await Assert.ThrowsAsync<CliException>(() => command.ExecuteAsync(
                workingDir,
                teamProject: "proj",
                generateReport: false, reportOutputFolder: "/reports",
                postEvent: false, hostAddress: null, tenantName: null, clientId: null, securityAccessToken: null, useSSL: true,
                validatorsToRun: ""));

            Assert.Contains("review-remote-ado", ex.Message);
        }
    }
}
