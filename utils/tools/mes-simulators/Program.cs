using Cmf.LightBusinessObjects.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MESSimulator.Line;
using MESSimulator.Mes;
using MESSimulator.Pipeline;
using System.Globalization;

namespace MESSimulator
{
    internal class Program
    {
        /// <summary>
        /// The MES client sends the current culture's name with its calls, and the MES refuses a call without one ("Missing
        /// value for mandatory property CultureName"). A container usually has no locale (no LANG), so the current culture is
        /// the invariant one, whose name is empty: use <paramref name="fallback"/> then.
        /// </summary>
        internal static void EnsureNamedCulture(string fallback = "en-US")
        {
            if (CultureInfo.CurrentCulture.Name.Length > 0 && CultureInfo.CurrentUICulture.Name.Length > 0)
            {
                return;
            }

            try
            {
                var culture = new CultureInfo(fallback);
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
            catch (CultureNotFoundException)
            {
                // Invariant globalization (no ICU): there is no named culture to switch to
            }
        }

        private static async Task<int> Main(string[] args)
        {
            EnsureNamedCulture();

            if (!TryParseArguments(args, out var overrides, out var lineFile, out var error))
            {
                Console.WriteLine(error);
                Console.WriteLine(Usage);
                return 1;
            }

            // The line file is required: there is no default line
            if (SimulatorConfiguration.ResolveLineFile(lineFile) == null)
            {
                Console.WriteLine(SimulatorConfiguration.MissingLineFileMessage(Directory.GetCurrentDirectory(), AppDomain.CurrentDomain.BaseDirectory));
                Console.WriteLine(Usage);
                return 1;
            }

            // Force content root to the executable directory so the json files are resolved from bin output.
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = [],
                ContentRootPath = AppDomain.CurrentDomain.BaseDirectory
            });

            // The host's default sources (appsettings.json, then unprefixed environment variables) are replaced by one
            // explicit order: files < MESSIM_ environment variables < command line. Re-adding the files after the
            // defaults would let appsettings.json override the environment variables.
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddSimulatorSources(AppDomain.CurrentDomain.BaseDirectory, lineFile, overrides);

            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });

            builder.Services.AddMESSimulator(builder.Configuration);
            builder.Services.AddHostedService<SimulationHost>();

            // On Ctrl+C / SIGTERM lots in progress finish their current run (e.g. waiting for a stepper)
            builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromMinutes(10));

            using var host = builder.Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var simulation = host.Services.GetRequiredService<IOptions<SimulationOptions>>().Value;
            var diagnostics = host.Services.GetRequiredService<IOptions<DiagnosticsOptions>>().Value;

            logger.LogInformation("Starting Scenario Run (speed={Speed}, MES call timings {Timings}); press Ctrl+C to stop",
                simulation.Speed.ToString(CultureInfo.InvariantCulture), diagnostics.LogMesCallDurations ? "on" : "off");

            // RunAsync disposes the host (and its services) when it stops
            var statistics = host.Services.GetRequiredService<MesCallStatistics>();
            var outcome = host.Services.GetRequiredService<RunOutcome>();
            try
            {
                await host.RunAsync();
            }
            catch (OptionsValidationException ex)
            {
                logger.LogCritical("Invalid configuration:{NewLine}{Errors}", Environment.NewLine, string.Join(Environment.NewLine, ex.Failures));
                return 2;
            }

            if (diagnostics.SummaryOnExit)
            {
                logger.LogInformation("MES call summary:{NewLine}{Summary}",
                    Environment.NewLine, statistics.FormatSummary());
            }

            if (outcome.Failed)
            {
                logger.LogCritical("Scenario Run failed: {Message}", outcome.Failure!.Message);
                return 3;
            }

            logger.LogInformation("Finished Scenario Run");
            return 0;
        }

        internal const string Usage =
            "Usage: MESSimulator --line|-l <file> [--speed|-s <decimal>] [--timings|-t] [--terminateonstart [true|false]] [--yes|-y] [--dry-run]";

        /// <summary>
        /// Maps the command line onto configuration keys: --speed/-s N (Simulation:Speed),
        /// --timings/-t (Diagnostics:LogMesCallDurations), --terminateonstart [true|false] (Line:Startup:TerminatePreviousRuns),
        /// --yes/-y (Line:Startup:ConfirmTerminate=false), --dry-run (Line:Startup:DryRun),
        /// --line/-l FILE (the line file; returned in <paramref name="lineFile"/>). On a bad argument <paramref name="error"/> says why.
        /// </summary>
        internal static bool TryParseArguments(string[] args, out Dictionary<string, string?> overrides, out string? lineFile, out string? error)
        {
            overrides = [];
            lineFile = null;
            error = null;
            const string startup = $"{LineOptions.SectionName}:{nameof(LineOptions.Startup)}";
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--speed":
                    case "-s":
                        if (i + 1 >= args.Length
                            || !decimal.TryParse(args[i + 1], NumberStyles.Number, CultureInfo.InvariantCulture, out var speed)
                            || speed <= 0)
                        {
                            error = "Error: --speed requires a positive decimal (e.g., --speed 1.25).";
                            return false;
                        }
                        overrides[$"{SimulationOptions.SectionName}:{nameof(SimulationOptions.Speed)}"] = speed.ToString(CultureInfo.InvariantCulture);
                        i++;
                        break;
                    case "--timings":
                    case "-t":
                        overrides[$"{DiagnosticsOptions.SectionName}:{nameof(DiagnosticsOptions.LogMesCallDurations)}"] = "true";
                        break;
                    case "--terminateonstart":
                        // An optional true/false after the flag (the flag alone means true)
                        string terminate = "true";
                        if (i + 1 < args.Length && bool.TryParse(args[i + 1], out var value))
                        {
                            terminate = value ? "true" : "false";
                            i++;
                        }
                        overrides[$"{startup}:{nameof(StartupOptions.TerminatePreviousRuns)}"] = terminate;
                        break;
                    case "--yes":
                    case "-y":
                        overrides[$"{startup}:{nameof(StartupOptions.ConfirmTerminate)}"] = "false";
                        break;
                    case "--dry-run":
                        overrides[$"{startup}:{nameof(StartupOptions.DryRun)}"] = "true";
                        break;
                    case "--line":
                    case "-l":
                        if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                        {
                            error = "Error: --line requires the path of a line file (e.g., --line simulationConfiguration.semi.json).";
                            return false;
                        }
                        lineFile = args[i + 1];
                        i++;
                        break;
                    default:
                        error = $"Unknown argument: {args[i]}";
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Points the LightBusinessObjects client at the MES from the "ClientConfiguration" section.
        /// </summary>
        internal static void ConfigureClient(IConfiguration configuration)
        {
            // Bind configuration for ClientConfiguration (supports env-specific overrides and reload)
            var clientConfigSection = configuration.GetSection("ClientConfiguration");

            // Cmf.LightBusinessObjects.Infrastructure.ClientConfiguration.Connection/.Authentication are
            // abstract (ConnectionConfiguration/AuthenticationConfiguration) so they can hold one of several
            // concrete shapes (ExplicitConnection, DiscoveryConnection, PersonalAccessTokenAuthentication, ...).
            // Microsoft.Extensions.Configuration.Binder can't pick a concrete type for an abstract-typed
            // property, so clientConfigSection.Get<ClientConfiguration>() throws
            // "Cannot create instance of type ... because it is either abstract or an interface."
            // Read the raw values instead and build the config via the library's own factory method.
            ClientConfigurationProvider.ConfigurationFactory = () =>
            {
                // Rebind each time to pick up changes from appsettings.{Environment}.json or env vars
                var environmentAddress = clientConfigSection["Connection:EnvironmentAddress"];
                var clientId = clientConfigSection["Authentication:ClientId"];
                var securityAccessToken = clientConfigSection["Authentication:SecurityAccessToken"];

                if (string.IsNullOrWhiteSpace(environmentAddress))
                    throw new InvalidOperationException("ClientConfiguration: Connection:EnvironmentAddress is required.");
                if (string.IsNullOrWhiteSpace(clientId))
                    throw new InvalidOperationException("ClientConfiguration: Authentication:ClientId is required.");
                if (string.IsNullOrWhiteSpace(securityAccessToken))
                    throw new InvalidOperationException("ClientConfiguration: Authentication:SecurityAccessToken is required.");

                return ClientConfiguration.ForPersonalAccessToken(environmentAddress, clientId, securityAccessToken);
            };
        }
    }
}
