using Cmf.CLI.Core;
using Cmf.CLI.Core.Enums;
using Cmf.CLI.Core.Objects;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO;
using System.IO.Abstractions;

namespace specs
{
    internal static class TestSupport
    {
        /// <summary>
        /// Creates a test console and returns its string writer, which will contain all the console content.
        /// </summary>
        public static StringWriter GetLogStringWriter()
        {
            var writer = new StringWriter();
            Environment.SetEnvironmentVariable("cmf_cli_loglevel", null);

            LoggerHelpers.LogLevelOption.Parse(""); // reset to default log level
            Log.AnsiConsole = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes,
                // we should probably add some color testing but it's low priority, to avoid a lot of trimming we disable the colours here
                ColorSystem = (ColorSystemSupport)ColorSystem.NoColors,
                Out = new AnsiConsoleOutput(writer),
                Interactive = InteractionSupport.No,
                Enrichment = new ProfileEnrichment
                {
                    UseDefaultEnrichers = false,
                },
            });

            return writer;
        }

        public static CmfPackage CreateCmfPackage(
            string packageId,
            PackageType packageType = PackageType.Generic,
            string version = "1.0.0",
            DependencyCollection dependencies = null,
            List<ContentToPack> contentToPack = null,
            IFileSystem fileSystem = null,
            string path = null)
        {
            var package = new CmfPackage(
                name: packageId,
                packageId: packageId,
                version: version,
                description: null,
                packageType: packageType,
                targetDirectory: null,
                targetLayer: null,
                isInstallable: null,
                isUniqueInstall: null,
                isToForceInstall: null,
                keywords: null,
                isToSetDefaultSteps: null,
                dependencies: dependencies,
                steps: null,
                contentToPack: contentToPack,
                xmlInjection: null,
                waitForIntegrationEntries: null,
                baseLocalizationFiles: null,
                testPackages: null);

            if (fileSystem != null && path != null)
            {
                package.SetFileInfo(fileSystem.FileInfo.New(path));
            }

            return package;
        }
    }
}
