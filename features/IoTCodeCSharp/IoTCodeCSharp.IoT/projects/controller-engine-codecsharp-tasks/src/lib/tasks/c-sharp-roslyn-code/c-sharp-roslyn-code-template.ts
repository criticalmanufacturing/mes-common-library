/** C# source shown for a new Roslyn Code task. */
export const CSHARP_ROSLYN_TEMPLATE_CONTENT = `using System.Text.Json.Nodes;
using System.Threading.Tasks;
using RoslynCode.Contracts;

public sealed class Code
{
    private readonly Framework _framework;

    public Code(Framework framework)
    {
        _framework = framework;
    }

    public async Task<JsonObject?> Main(JsonObject inputs, Outputs outputs)
    {
        // Add code here;

        // Emit output during execution: outputs.Emit("output1", JsonValue.Create(value));
        // return new JsonObject { ["doubled"] = value * 2 };
        return null; 
    }
}
`;