using Cmf.CLI.Core;
using Cmf.CLI.Core.Enums;
using Cmf.CLI.Core.Objects;
using audit.Services;
using Cmf.CLI.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Parsing;
using ExecutionContext = Cmf.CLI.Core.Objects.ExecutionContext;

try
{
    // telemetry is opt-in: set cmf_audit_enable_telemetry / cmf_audit_enable_extended_telemetry to enable it
    var registryAddress = Environment.GetEnvironmentVariable("cmf_audit_registry");

    var (rootCommand, parser) = await StartupModule.Configure(
        packageId: "@criticalmanufacturing/audit",
        envVarPrefix: "cmf_audit",
        description: "Plugin used to audit CM CLI based projects",
        args: args,
        npmClient: new VerdaccioService(new Uri(registryAddress ?? "https://dev.criticalmanufacturing.io/repository/npm-public")));

    using var activity = ExecutionContext.ServiceProvider.GetService<ITelemetryService>()!.StartActivity("Main");
    var result = await parser?.InvokeAsync(args);
    activity?.SetTag("execution.success", true);
    return result;
}
catch (CliException e)
{
    Log.Error(e.Message);
    Log.Debug(e.StackTrace);
    return (int)e.ErrorCode;
}
catch (Exception e)
{
    Log.Debug("Caught exception at program.");
    Log.Exception(e);
    ExecutionContext.ServiceProvider.GetService<ITelemetryService>()!.LogException(e);
    return (int)ErrorCode.Default;
}