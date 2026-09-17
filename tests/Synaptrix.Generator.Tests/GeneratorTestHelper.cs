using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Synaptrix.Generator;
using System.Reflection;
using System.Collections.Immutable;
using Synaptrix;

namespace Synaptrix.Generator.Tests;

public static class GeneratorTestHelper
{
    public static (ImmutableArray<Diagnostic> Diagnostics, string GeneratedSource) RunGenerator(string source, Dictionary<string, string>? globalOptions = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IRequestHandler<,>).Assembly.Location),
            // Add other necessary assemblies
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location)
        };

        var compilation = CSharpCompilation.Create(
            "Tests",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new SynaptrixGenerator();
        
        CSharpGeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Apply global options if provided
        if (globalOptions != null && globalOptions.Any())
        {
            var optionsProvider = new TestAnalyzerConfigOptionsProvider(globalOptions);
            driver = (CSharpGeneratorDriver)driver.WithUpdatedAnalyzerConfigOptions(optionsProvider);
        }

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var runResult = driver.GetRunResult();

        // We expect one generated source from our generator
        var generatedSource = string.Empty;
        if (runResult.Results.Length > 0)
        {
            var generatorResult = runResult.Results[0];
            if (generatorResult.GeneratedSources.Length > 0)
            {
                generatedSource = generatorResult.GeneratedSources[0].SourceText.ToString();
            }
        }

        return (diagnostics, generatedSource);
    }

    /// <summary>
    /// Runs the generator and returns ALL generated source files plus the diagnostics
    /// emitted when compiling the user source together with the generator output. Use this
    /// when the assertion needs to verify that the generated code itself compiles.
    /// </summary>
    public static (ImmutableArray<Diagnostic> CompilationDiagnostics, string[] GeneratedSources) RunGeneratorAndCompile(
        string source,
        Dictionary<string, string>? globalOptions = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IRequestHandler<,>).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.ComponentModel").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location),
            MetadataReference.CreateFromFile(Assembly.Load("Microsoft.Extensions.DependencyInjection.Abstractions").Location),
        };

        var compilation = CSharpCompilation.Create(
            "Tests",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new SynaptrixGenerator();
        CSharpGeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        if (globalOptions != null && globalOptions.Any())
        {
            driver = (CSharpGeneratorDriver)driver.WithUpdatedAnalyzerConfigOptions(new TestAnalyzerConfigOptionsProvider(globalOptions));
        }

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var runResult = driver.GetRunResult();
        var sources = runResult.Results.Length == 0
            ? Array.Empty<string>()
            : runResult.Results[0].GeneratedSources.Select(s => s.SourceText.ToString()).ToArray();

        var compileDiagnostics = outputCompilation.GetDiagnostics();
        return (compileDiagnostics, sources);
    }
}

public class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions _globalOptions;

    public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> globalOptions)
    {
        _globalOptions = new TestAnalyzerConfigOptions(globalOptions);
    }

    public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestAnalyzerConfigOptions(new Dictionary<string, string>());

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new TestAnalyzerConfigOptions(new Dictionary<string, string>());
}

public class TestAnalyzerConfigOptions : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> _options;

    public TestAnalyzerConfigOptions(Dictionary<string, string> options)
    {
        _options = options;
    }

    public override bool TryGetValue(string key, out string value)
    {
        return _options.TryGetValue(key, out value);
    }
}
