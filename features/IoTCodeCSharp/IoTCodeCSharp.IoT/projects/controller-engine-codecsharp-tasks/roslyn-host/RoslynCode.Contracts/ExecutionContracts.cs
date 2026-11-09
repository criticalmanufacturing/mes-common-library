using System.Text.Json.Nodes;
using Cmf.Foundation.BusinessOrchestration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace RoslynCode.Contracts;

/// <summary>
/// Entry point injected into every user "Code" class, exposing the controller capabilities
/// available to a C# Roslyn Code task (logging, the persisted data store, the message bus, the
/// MES system API, misc utilities, and the task's configured driver).
/// </summary>
public sealed class Framework
{
    /// <summary>Logs messages at Info/Warning/Error/Debug severity to the controller's logger.</summary>
    public Logger Logger { get; }

    /// <summary>Reads and writes values in the controller's persisted data store.</summary>
    public DataStore DataStore { get; }

    /// <summary>Sends requests and publishes notifications on the controller's message bus.</summary>
    public MessageBus MessageBus { get; }

    /// <summary>Calls the MES system API (business orchestration LBOs).</summary>
    public SystemApi System { get; }

    /// <summary>Miscellaneous helpers: delaying execution, serializing values, and type conversion.</summary>
    public Utils Utils { get; }

    /// <summary>Interacts with the task's configured controller driver, if any.</summary>
    public Driver Driver { get; }

    /// <summary>Creates the framework, wiring each capability to the given host bridge.</summary>
    /// <param name="bridge">Transport used to invoke controller-side operations from user code.</param>
    public Framework(IHostBridge bridge)
    {
        Logger = new Logger(bridge);
        DataStore = new DataStore(bridge);
        MessageBus = new MessageBus(bridge);
        System = new SystemApi(bridge);
        Utils = new Utils(bridge);
        Driver = new Driver(bridge);
    }
}

/// <summary>
/// Transport used by <see cref="Framework"/>'s capabilities to invoke an operation on the
/// controller host and get its result back, regardless of where the user code is actually running
/// (in-browser during compilation, or as a child process during real execution).
/// </summary>
public interface IHostBridge
{
    /// <summary>Invokes a named host-side operation and returns its result.</summary>
    /// <param name="method">Identifier of the host operation to invoke (e.g. "logInfo", "dataStoreGet").</param>
    /// <param name="arguments">Named arguments for the operation.</param>
    /// <returns>The operation's result, or null if it has none.</returns>
    JsonNode? Invoke(string method, JsonObject arguments);
}

/// <summary>Logs messages to the controller's logger at various severities.</summary>
public sealed class Logger(IHostBridge bridge)
{
    /// <summary>Logs an informational message.</summary>
    public void Info(string message) => bridge.Invoke("logInfo", new JsonObject { ["message"] = message });
    /// <summary>Logs a warning message.</summary>
    public void Warning(string message) => bridge.Invoke("logWarning", new JsonObject { ["message"] = message });
    /// <summary>Logs an error message.</summary>
    public void Error(string message) => bridge.Invoke("logError", new JsonObject { ["message"] = message });
    /// <summary>Logs a debug message.</summary>
    public void Debug(string message) => bridge.Invoke("logDebug", new JsonObject { ["message"] = message });
}

/// <summary>Reads and writes values in the controller's persisted data store.</summary>
public sealed class DataStore(IHostBridge bridge)
{
    /// <summary>Retrieves the value stored under <paramref name="key"/>, or null if there isn't one.</summary>
    public Task<JsonNode?> Get(string key) => Task.FromResult(bridge.Invoke("dataStoreGet", new JsonObject { ["key"] = key }));

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>.</summary>
    public Task Set(string key, JsonNode? value)
    {
        bridge.Invoke("dataStoreSet", new JsonObject { ["key"] = key, ["value"] = value });
        return Task.CompletedTask;
    }
}

/// <summary>Sends requests and publishes notifications on the controller's message bus.</summary>
public sealed class MessageBus(IHostBridge bridge)
{
    /// <summary>Sends <paramref name="message"/> on <paramref name="subject"/> and awaits its reply.</summary>
    /// <param name="subject">Subject to send the request on.</param>
    /// <param name="message">Message payload.</param>
    /// <param name="timeoutMs">Optional timeout, in milliseconds, to wait for a reply.</param>
    public Task<JsonNode?> SendRequest(string subject, JsonNode? message, long? timeoutMs = null) =>
        Task.FromResult(bridge.Invoke("messageBusSendRequest", new JsonObject { ["subject"] = subject, ["message"] = message, ["timeoutMs"] = timeoutMs }));

    /// <summary>Publishes <paramref name="message"/> on <paramref name="subject"/> without waiting for a reply.</summary>
    public void Publish(string subject, JsonNode? message) =>
        bridge.Invoke("messageBusPublish", new JsonObject { ["subject"] = subject, ["message"] = message });
}

/// <summary>Calls the MES system API (business orchestration LBOs).</summary>
public sealed class SystemApi(IHostBridge bridge)
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        // LBO input/output properties are wire-compatible with cmf-lbos' generated classes (and the
        // real MES System API) only in their original PascalCase form (e.g. "Context", not
        // "context") - a camelCase contract resolver here would silently rename every property.
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        // The controller's System API identifies which LBO to invoke from a "$type" discriminator
        // on the serialized input (the same convention cmf-lbos sets on every generated input
        // class client-side). Without it the bridge call goes out unidentifiable and just hangs.
        TypeNameHandling = TypeNameHandling.Objects,
        // Input/output types are loaded here from the single merged "Cmf.LightBusinessObjects"
        // reference assembly the Roslyn host ships (so it doesn't need every individual
        // Cmf.*.BusinessOrchestration.dll), so the default binder's assembly-qualified name would
        // put "Cmf.LightBusinessObjects" in "$type" instead of the LBO's real domain assembly
        // (e.g. "Cmf.Foundation.BusinessOrchestration") the platform expects there.
        SerializationBinder = new LboSerializationBinder()
    };

    private sealed class LboSerializationBinder : ISerializationBinder
    {
        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            typeName = serializedType.FullName;
            assemblyName = DomainAssemblyName(serializedType);
        }

        public Type BindToType(string? assemblyName, string typeName) =>
            // The response names the LBO's real domain assembly, which isn't the one actually
            // loaded here, so resolve by type name alone against the merged assembly instead.
            typeof(BaseInput).Assembly.GetType(typeName)
                ?? throw new InvalidOperationException($"Could not resolve LBO type '{typeName}'.");

        // Every LBO domain assembly is named after the first three segments of its types'
        // namespace (e.g. "Cmf.Foundation.BusinessOrchestration.ApplicationSettingManagement.InputObjects"
        // lives in "Cmf.Foundation.BusinessOrchestration"; "Cmf.Navigo.BusinessOrchestration.*" in
        // "Cmf.Navigo.BusinessOrchestration").
        private static string DomainAssemblyName(Type type)
        {
            var segments = type.Namespace?.Split('.') ?? [];
            return segments.Length >= 3 ? string.Join('.', segments[..3]) : type.Assembly.GetName().Name ?? "";
        }
    }

    /// <summary>
    /// Calls the MES system API with <paramref name="input"/> and awaits its reply.
    /// Mirrors the platform's SystemAPI.call&lt;T extends BaseOutput&gt;(input: BaseInput): Promise&lt;T&gt;.
    /// </summary>
    /// <typeparam name="TOutput">Expected LBO output type.</typeparam>
    /// <param name="input">LBO input object to send.</param>
    /// <returns>The deserialized LBO output, or null if the call returned none.</returns>
    public async Task<TOutput?> Call<TOutput>(BaseInput input) where TOutput : BaseOutput
    {
        var requestNode = JsonNode.Parse(JsonConvert.SerializeObject(input, SerializerSettings));
        var responseNode = bridge.Invoke("systemCall", new JsonObject { ["input"] = requestNode });
        var result = responseNode is null
            ? null
            : JsonConvert.DeserializeObject<TOutput>(responseNode.ToJsonString(), SerializerSettings);
        return await Task.FromResult(result);
    }
}

/// <summary>Miscellaneous helpers: delaying execution, serializing values, and type conversion.</summary>
public sealed class Utils(IHostBridge bridge)
{
    /// <summary>Pauses execution for the given number of milliseconds.</summary>
    public Task Sleep(int milliseconds) => Task.Delay(milliseconds);

    /// <summary>Serializes <paramref name="value"/> to its JSON string representation.</summary>
    public string Stringify(JsonNode? value) => value?.ToJsonString() ?? "null";

    /// <summary>Converts <paramref name="value"/> to a controller value type (e.g. "String", "Integer", "Boolean").</summary>
    /// <param name="value">Value to convert.</param>
    /// <param name="toType">Name of the controller type to convert to.</param>
    /// <param name="defaultValue">Value to fall back to if the conversion fails and <paramref name="throwOnError"/> is false.</param>
    /// <param name="throwOnError">Whether to throw instead of returning <paramref name="defaultValue"/> on failure.</param>
    public Task<JsonNode?> ConvertValueToType(JsonNode? value, string toType, JsonNode? defaultValue = null, bool throwOnError = false) =>
        Task.FromResult(bridge.Invoke("utilsConvertValueToType", new JsonObject
        {
            ["value"] = value,
            ["toType"] = toType,
            ["defaultValue"] = defaultValue,
            ["throwOnError"] = throwOnError
        }));
}

/// <summary>Interacts with the task's configured controller driver, if any.</summary>
public sealed class Driver(IHostBridge bridge)
{
    /// <summary>Whether a controller driver is configured for this task.</summary>
    public bool Available => bridge.Invoke("driverAvailable", new JsonObject())?.GetValue<bool>() ?? false;

    /// <summary>Connects to the configured driver.</summary>
    public Task Connect() => InvokeVoid("driverConnect");

    /// <summary>Disconnects from the configured driver.</summary>
    public Task Disconnect() => InvokeVoid("driverDisconnect");

    /// <summary>Executes a command on the driver and awaits its reply.</summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="parameters">Command parameters.</param>
    /// <param name="timeoutMs">Optional timeout, in milliseconds, to wait for a reply.</param>
    public Task<JsonNode?> ExecuteCommand(JsonNode? command, JsonNode? parameters, long? timeoutMs = null) =>
        Task.FromResult(bridge.Invoke("driverExecuteCommand", new JsonObject { ["command"] = command, ["parameters"] = parameters, ["timeoutMs"] = timeoutMs }));

    /// <summary>Reads the given driver properties.</summary>
    public Task<JsonNode?> GetProperties(JsonNode? properties) =>
        Task.FromResult(bridge.Invoke("driverGetProperties", new JsonObject { ["properties"] = properties }));

    /// <summary>Writes the given driver properties.</summary>
    public Task<JsonNode?> SetProperties(JsonNode? propertiesValues) =>
        Task.FromResult(bridge.Invoke("driverSetProperties", new JsonObject { ["propertiesValues"] = propertiesValues }));

    /// <summary>Sends a raw message directly to the driver and awaits its reply.</summary>
    /// <param name="type">Type identifier of the raw message.</param>
    /// <param name="content">Message content.</param>
    /// <param name="timeoutMs">Optional timeout, in milliseconds, to wait for a reply.</param>
    public Task<JsonNode?> SendRaw(string type, JsonNode? content, long? timeoutMs = null) =>
        Task.FromResult(bridge.Invoke("driverSendRaw", new JsonObject { ["type"] = type, ["content"] = content, ["timeoutMs"] = timeoutMs }));

    /// <summary>Sends a raw notification directly to the driver without waiting for a reply.</summary>
    public Task NotifyRaw(string type, JsonNode? content) => InvokeVoid("driverNotifyRaw", new JsonObject { ["type"] = type, ["content"] = content });

    /// <summary>Registers custom driver definitions with the controller driver.</summary>
    public Task RegisterCustomDriverDefinitions(JsonNode? custom) => InvokeVoid("driverRegisterCustomDriverDefinitions", new JsonObject { ["custom"] = custom });

    private Task InvokeVoid(string method, JsonObject? arguments = null)
    {
        bridge.Invoke(method, arguments ?? new JsonObject());
        return Task.CompletedTask;
    }
}

/// <summary>Collects the named output values a task execution emits, in addition to its return value.</summary>
public sealed class Outputs
{
    private readonly List<OutputEmission> _emissions = [];

    /// <summary>Every output emitted so far during this execution, in emission order.</summary>
    public IReadOnlyList<OutputEmission> Emissions => _emissions;

    /// <summary>Emits a named output with the given value.</summary>
    public void Emit(string name, JsonNode? value) => _emissions.Add(new OutputEmission(name, value));
}

/// <summary>A single named output value emitted via <see cref="Outputs.Emit"/>.</summary>
/// <param name="Name">Output name.</param>
/// <param name="Value">Output value.</param>
public sealed record OutputEmission(string Name, JsonNode? Value);

/// <summary>Request to execute a compiled Code assembly, sent from the controller to the execution host.</summary>
public sealed class ExecutionRequest
{
    /// <summary>Path to the compiled Code assembly to load and run for this request.</summary>
    public string AssemblyPath { get; init; } = "";

    /// <summary>Named input values for the task's inputs, keyed by input name.</summary>
    public JsonObject Inputs { get; init; } = new();
}

/// <summary>Result of executing a compiled Code assembly, returned from the execution host to the controller.</summary>
public sealed class ExecutionResponse
{
    /// <summary>Value returned by the user code's Main method, if any.</summary>
    public JsonNode? Result { get; init; }

    /// <summary>Every output the execution emitted via <see cref="Outputs.Emit"/>.</summary>
    public IReadOnlyList<OutputEmission> Outputs { get; init; } = [];

    /// <summary>Error message if the execution failed, otherwise null.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// A framework operation request sent from the execution host back to the controller over the
/// same process boundary an <see cref="ExecutionRequest"/>/<see cref="ExecutionResponse"/> pair
/// crosses (e.g. a Logger.Info call needs to reach the controller's real logger).
/// </summary>
public sealed class HostRequest
{
    /// <summary>Correlation id used to match this request to its <see cref="HostResponse"/>.</summary>
    public string Id { get; init; } = "";

    /// <summary>Identifier of the host operation to invoke (e.g. "logInfo", "dataStoreGet").</summary>
    public string Method { get; init; } = "";

    /// <summary>Named arguments for the operation.</summary>
    public JsonObject Arguments { get; init; } = new();
}

/// <summary>The controller's reply to a <see cref="HostRequest"/>.</summary>
public sealed class HostResponse
{
    /// <summary>Correlation id matching the originating <see cref="HostRequest"/>.</summary>
    public string Id { get; init; } = "";

    /// <summary>The operation's result, if any.</summary>
    public JsonNode? Result { get; init; }

    /// <summary>Error message if the operation failed, otherwise null.</summary>
    public string? Error { get; init; }
}
