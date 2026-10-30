using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;
using RoslynCode.Contracts;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true
};

// RoslynCode.Contracts (loaded in the Default context) now references LBO types directly, so its dependencies
// must also be resolvable here, not just in the per-execution "roslyn-user-code" context below.
AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
{
    var dependencyPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName.Name}.dll");
    return File.Exists(dependencyPath) ? context.LoadFromAssemblyPath(dependencyPath) : null;
};

// Serves one execution request per stdin line for as long as the caller keeps stdin open, so a
// caller that wants a warm/reusable process can send many requests, while a caller that wants the
// original one-shot behavior just sends one line and closes stdin right after (EOF below then ends
// this loop and the process exits, exactly like the old single-request Main used to).
string? requestJson;
while ((requestJson = await Console.In.ReadLineAsync()) is not null)
{
    if (string.IsNullOrWhiteSpace(requestJson))
    {
        continue;
    }

    // A per-request failure (bad assembly path, malformed request, user code throwing) must only
    // fail that one request, not this process — a pooled/warm caller relies on the process surviving
    // to serve its next request.
    try
    {
        var request = JsonSerializer.Deserialize<ExecutionRequest>(requestJson, jsonOptions)
            ?? throw new InvalidOperationException("Execution request could not be parsed.");
        var response = await Execute(request.AssemblyPath, request, new HostBridge(jsonOptions));
        await WriteResponse(response, jsonOptions);
    }
    catch (Exception exception)
    {
        await WriteResponse(new ExecutionResponse { Error = exception.ToString() }, jsonOptions);
    }
}

return 0;

static async Task<ExecutionResponse> Execute(string assemblyPath, ExecutionRequest request, IHostBridge bridge)
{
    if (!File.Exists(assemblyPath))
    {
        throw new FileNotFoundException("Compiled C# assembly was not found.", assemblyPath);
    }

    var loadContext = new AssemblyLoadContext("roslyn-user-code", isCollectible: true);
    try
    {
        loadContext.Resolving += (context, assemblyName) =>
        {
            if (assemblyName.Name == typeof(Framework).Assembly.GetName().Name) { return typeof(Framework).Assembly; }

            var dependencyPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName.Name}.dll");
            return File.Exists(dependencyPath) ? context.LoadFromAssemblyPath(dependencyPath) : null;
        };

        // Deliberately NOT LoadFromAssemblyPath: that memory-maps the file and keeps it locked for
        // as long as this (collectible) context lives, and `Unload()` below only *requests* a
        // teardown that the GC performs later — so the caller's `unlink` right after this request
        // completes would race the mapping and fail with EPERM/sharing violation on Windows.
        // Reading the bytes up front and loading from memory leaves no handle behind, so the
        // caller is free to delete its per-execution DLL the moment it gets the response.
        var assemblyBytes = File.ReadAllBytes(Path.GetFullPath(assemblyPath));
        using var assemblyStream = new MemoryStream(assemblyBytes, writable: false);
        var assembly = loadContext.LoadFromStream(assemblyStream);
        var codeType = assembly.GetType("Code", throwOnError: false)
            ?? throw new InvalidOperationException("Compiled assembly must define a public 'Code' class.");
        var instance = CreateCodeInstance(codeType, bridge);
        var outputs = new Outputs();
        var main = codeType.GetMethod("Main", BindingFlags.Public | BindingFlags.Instance, [typeof(JsonObject), typeof(Outputs)])
            ?? throw new InvalidOperationException("Code must define public Main(JsonObject inputs, Outputs outputs).");

        if (main.Invoke(instance, [request.Inputs, outputs]) is not Task task)
        {
            throw new InvalidOperationException("Code.Main must return Task or Task<JsonNode?>.");
        }

        await task;
        var result = ReadTaskResult(task);
        return new ExecutionResponse { Result = result, Outputs = outputs.Emissions };
    }
    finally
    {
        loadContext.Unload();
    }
}

static object CreateCodeInstance(Type codeType, IHostBridge bridge)
{
    var frameworkConstructor = codeType.GetConstructor([typeof(Framework)]);
    if (frameworkConstructor is not null)
    {
        return frameworkConstructor.Invoke([new Framework(bridge)]);
    }

    return Activator.CreateInstance(codeType)
        ?? throw new InvalidOperationException("Code must have a public parameterless or Framework constructor.");
}

static JsonNode? ReadTaskResult(Task task)
{
    var resultProperty = task.GetType().GetProperty("Result");
    if (resultProperty is null)
    {
        return null;
    }

    return resultProperty.GetValue(task) switch
    {
        null => null,
        JsonNode node => node,
        _ => throw new InvalidOperationException("Code.Main must return Task, Task<JsonNode?>, or Task<JsonObject>.")
    };
}

static Task WriteResponse(ExecutionResponse response, JsonSerializerOptions jsonOptions) =>
    Console.Out.WriteLineAsync(JsonSerializer.Serialize(response, jsonOptions));

sealed class HostBridge(JsonSerializerOptions jsonOptions) : IHostBridge
{
    public JsonNode? Invoke(string method, JsonObject arguments)
    {
        var id = Guid.NewGuid().ToString("N");
        Console.Out.WriteLine(JsonSerializer.Serialize(new HostRequest { Id = id, Method = method, Arguments = arguments }, jsonOptions));
        var responseJson = Console.In.ReadLine() ?? throw new InvalidOperationException("Controller closed the framework bridge.");
        var response = JsonSerializer.Deserialize<HostResponse>(responseJson, jsonOptions)
            ?? throw new InvalidOperationException("Framework bridge response could not be parsed.");
        if (response.Id != id) { throw new InvalidOperationException("Framework bridge response id did not match its request."); }
        if (response.Error is not null) { throw new InvalidOperationException(response.Error); }
        return response.Result;
    }
}