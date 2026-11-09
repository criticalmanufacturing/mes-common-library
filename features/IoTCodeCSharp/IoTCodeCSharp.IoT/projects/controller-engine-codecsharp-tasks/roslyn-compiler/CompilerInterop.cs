using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.JSInterop;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;

namespace RoslynCode.Compiler;

public sealed class CompilationResult
{
    public string AssemblyBase64 { get; init; } = "";
    public string SourceHash { get; init; } = "";
    public string CatalogVersion { get; init; } = "1";
    public IReadOnlyList<CompilationDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class CompilationDiagnostic
{
    public string Severity { get; init; } = "info";
    public string Message { get; init; } = "";
    public int? Line { get; init; }
    public int? Column { get; init; }
}

public sealed class CompletionItemResult
{
    // DisplayText — the actual insert text (e.g. for a method it's just the method name, no
    // parens/params; parameter hints are a signature-help concern, not completion) — matching
    // what most browser-based Roslyn playgrounds do for a first pass.
    public string Label { get; init; } = "";
    // Roslyn's own tag names (e.g. "Method", "Property", "ExtensionMethod", "Keyword") — mapped to
    // monaco.languages.CompletionItemKind on the JS side, which is the only place that enum is available.
    public string Kind { get; init; } = "Text";
    public string SortText { get; init; } = "";
}

public static class CompilerInterop
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(CreateReferences);
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp12);
    // Every other .csproj in this repo has <Nullable>enable</Nullable> — user code compiled here
    // ad hoc via CSharpCompilation.Create wouldn't otherwise pick that up (it's a csproj/MSBuild
    // setting, not a language default), so e.g. "JsonObject?" in the template would warn with
    // CS8632 ("... should only be used in code within a '#nullable' annotations context").
    private static readonly CSharpCompilationOptions CompilationOptions =
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: false)
            .WithNullableContextOptions(NullableContextOptions.Enable);

    [JSInvokable]
    public static CompilationResult Compile(string source)
    {
        source ??= "";
        var sourceHash = SourceHash(source);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, ParseOptions);
        var compilation = CSharpCompilation.Create(
            $"RoslynUserCode_{sourceHash}",
            [syntaxTree],
            References.Value,
            CompilationOptions);

        using var output = new MemoryStream();
        var emit = compilation.Emit(output);
        return new CompilationResult
        {
            AssemblyBase64 = emit.Success ? Convert.ToBase64String(output.ToArray()) : "",
            SourceHash = sourceHash,
            Diagnostics = emit.Diagnostics.Select(ToDiagnostic).ToArray()
        };
    }

    [JSInvokable]
    public static async Task<IReadOnlyList<CompletionItemResult>> GetCompletions(string source, int position)
    {
        source ??= "";
        var document = CreateDocument(source);
        var completionService = CompletionService.GetService(document);
        if (completionService == null)
        {
            return [];
        }
        var completions = await completionService.GetCompletionsAsync(document, position);
        return completions.ItemsList.Select(item => new CompletionItemResult
        {
            Label = item.DisplayText,
            Kind = item.Tags.FirstOrDefault(tag => tag != WellKnownTags.Public && tag != WellKnownTags.Internal && tag != WellKnownTags.Private && tag != WellKnownTags.Protected) ?? "Text",
            SortText = item.SortText
        }).ToArray();
    }

    // Documentation is fetched lazily (only for the one item the user has highlighted in the
    // suggestion list) rather than eagerly for every item GetCompletions returns — same as real
    // Roslyn-backed editors, since resolving descriptions for the whole list on every keystroke
    // would be wasted work for the ~1 item anyone actually looks at.
    [JSInvokable]
    public static async Task<string?> GetCompletionDescription(string source, int position, string label, string sortText)
    {
        source ??= "";
        var document = CreateDocument(source);
        var completionService = CompletionService.GetService(document);
        if (completionService == null)
        {
            return null;
        }
        var completions = await completionService.GetCompletionsAsync(document, position);
        // DisplayText + SortText is a good-enough (if not mathematically unique) way to find the
        // same item back — there's no completion "id" that survives the JS interop round-trip.
        var item = completions.ItemsList.FirstOrDefault(candidate => candidate.DisplayText == label && candidate.SortText == sortText);
        if (item == null)
        {
            return null;
        }
        var description = await completionService.GetDescriptionAsync(document, item!);
        return description == null ? null : string.Concat(description.TaggedParts.Select(part => part.Text));
    }

    // A fresh AdhocWorkspace/Document per call, rather than an incrementally-updated one — simpler
    // and correct at the cost of some perf; revisit only if completion latency proves to be a problem.
    private static Document CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "RoslynUserCode",
            "RoslynUserCode",
            LanguageNames.CSharp,
            compilationOptions: CompilationOptions,
            parseOptions: ParseOptions,
            metadataReferences: References.Value);
        var project = workspace.AddProject(projectInfo);
        var documentInfo = DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            "Code.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())));
        return workspace.AddDocument(documentInfo);
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        var assembly = typeof(CompilerInterop).Assembly;
        var resourceNames = new HashSet<string>(assembly.GetManifestResourceNames(), StringComparer.Ordinal);
        return resourceNames
            .Where(name => name.StartsWith("References.", StringComparison.Ordinal) && name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(name =>
            {
                // Not every reference ships an XML doc file alongside it (Cmf.LightBusinessObjects.dll
                // doesn't) — when one exists (BCL references, RoslynCode.Contracts.dll), attach it so
                // completion/hover shows real descriptions instead of just signatures.
                var documentationResourceName = string.Concat(name.AsSpan(0, name.Length - ".dll".Length), ".xml");
                var documentation = resourceNames.Contains(documentationResourceName)
                    ? XmlDocumentationProvider.CreateFromBytes(ReadResourceBytes(assembly, documentationResourceName))
                    : null;
                return MetadataReference.CreateFromImage(ReadResourceBytes(assembly, name), documentation: documentation);
            })
            .ToArray();
    }

    private static byte[] ReadResourceBytes(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Reference '{name}' was not found.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static CompilationDiagnostic ToDiagnostic(Diagnostic diagnostic)
    {
        var position = diagnostic.Location.IsInSource ? diagnostic.Location.GetLineSpan().StartLinePosition : default;
        return new CompilationDiagnostic
        {
            Severity = diagnostic.Severity.ToString().ToLowerInvariant(),
            Message = diagnostic.GetMessage(),
            Line = diagnostic.Location.IsInSource ? position.Line + 1 : null,
            Column = diagnostic.Location.IsInSource ? position.Character + 1 : null
        };
    }

    private static string SourceHash(string source) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source ?? ""))).ToLowerInvariant();
}