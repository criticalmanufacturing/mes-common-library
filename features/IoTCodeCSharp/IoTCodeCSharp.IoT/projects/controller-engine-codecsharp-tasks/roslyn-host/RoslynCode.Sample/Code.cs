using System.Text.Json.Nodes;
using Cmf.Foundation.BusinessOrchestration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RoslynCode.Contracts;

public sealed class Code
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore
    };

    private readonly Framework _framework;

    public Code(Framework framework)
    {
        _framework = framework;
    }

    public async Task<JsonObject> Main(JsonObject inputs, Outputs outputs)
    {
        var value = inputs["value"]?.GetValue<int>() ?? 0;
        if (inputs["bridge"]?.GetValue<bool>() == true)
        {
            _framework.Logger.Debug("bridge log");
            await _framework.DataStore.Set("value", JsonValue.Create(value));
            var storedValue = await _framework.DataStore.Get("value");
            _framework.MessageBus.Publish("sample", storedValue?.DeepClone());
            var systemValue = await _framework.System.Call<BaseOutput>(new BaseInput());
            return new JsonObject { ["stored"] = storedValue, ["system"] = JsonNode.Parse(JsonConvert.SerializeObject(systemValue, SerializerSettings)) };
        }
        if (inputs["lbo"]?.GetValue<bool>() == true)
        {
            var container = new Cmf.Navigo.BusinessObjects.Container
            {
                CapacityForm = "Pallet",
                HoldCount = 3
            };
            var lboInput = new Cmf.Navigo.BusinessOrchestration.ContainerManagement.InputObjects.EmptyContainerInput
            {
                Container = container
            };
            var systemLboResult = await _framework.System.Call<Cmf.Navigo.BusinessOrchestration.ContainerManagement.OutputObjects.EmptyContainerOutput>(lboInput);
            return new JsonObject { ["systemLbo"] = JsonNode.Parse(JsonConvert.SerializeObject(systemLboResult, SerializerSettings)) };
        }
        if (inputs["driver"]?.GetValue<bool>() == true)
        {
            var converted = await _framework.Utils.ConvertValueToType(JsonValue.Create(value), "Integer");
            if (!_framework.Driver.Available) throw new InvalidOperationException("Driver should be available.");
            await _framework.Driver.Connect();
            var command = await _framework.Driver.ExecuteCommand(new JsonObject { ["name"] = "sample" }, new JsonObject { ["value"] = value });
            await _framework.Driver.GetProperties(new JsonArray("status"));
            await _framework.Driver.SetProperties(new JsonObject { ["status"] = "ready" });
            await _framework.Driver.SendRaw("text", JsonValue.Create("hello"));
            await _framework.Driver.NotifyRaw("text", JsonValue.Create("done"));
            await _framework.Driver.RegisterCustomDriverDefinitions(new JsonObject { ["name"] = "sample" });
            await _framework.Driver.Disconnect();
            return new JsonObject { ["converted"] = converted, ["command"] = command };
        }
        outputs.Emit("doubledFromEmit", JsonValue.Create(value * 2));
        return new JsonObject { ["doubledFromResult"] = value * 2 };
    }
}