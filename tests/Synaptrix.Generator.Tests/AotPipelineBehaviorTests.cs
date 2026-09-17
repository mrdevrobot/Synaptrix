using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Synaptrix.Generator.Tests;

/// <summary>
/// The closed pipeline-behavior registrations an ahead-of-time compiled host needs: a behaviour whose
/// response is a value type cannot be constructed by the container at runtime, because generic code is
/// not shared across value types and AOT only emits the instantiations it sees in the source.
/// </summary>
public class AotPipelineBehaviorTests
{
    private const string Source = @"
using Synaptrix;
using Synaptrix.Attributes;

[assembly: DiscoverSynaptrixHandlers]

namespace MyTestApp
{
    public readonly struct Result { public int Value { get; init; } }

    public class ValueTypeRequest : IRequest<Result> { }

    public class ValueTypeHandler : IRequestHandler<ValueTypeRequest, Result>
    {
        public System.Threading.Tasks.ValueTask<Result> Handle(ValueTypeRequest request, System.Threading.CancellationToken cancellationToken)
            => new System.Threading.Tasks.ValueTask<Result>(new Result());
    }

    public class ReferenceTypeRequest : IRequest<string> { }

    public class ReferenceTypeHandler : IRequestHandler<ReferenceTypeRequest, string>
    {
        public System.Threading.Tasks.ValueTask<string> Handle(ReferenceTypeRequest request, System.Threading.CancellationToken cancellationToken)
            => new System.Threading.Tasks.ValueTask<string>(""ok"");
    }

    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public System.Threading.Tasks.ValueTask<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            System.Threading.CancellationToken cancellationToken)
            => next(cancellationToken);
    }
}";

    private static Dictionary<string, string> Enabled => new()
    {
        ["build_property.SynaptrixAotPipelineBehaviors"] = "true",
    };

    private static string? AotSource(string[] sources)
        => sources.FirstOrDefault(s => s.Contains("SynaptrixAotPipelineBehaviors"));

    [Fact]
    public void ValueTypeResponse_GetsAClosedRegistration()
    {
        var (_, sources) = GeneratorTestHelper.RunGeneratorAndCompile(Source, Enabled);

        var aot = AotSource(sources);
        Assert.NotNull(aot);
        Assert.Contains(
            "services.AddTransient<global::Synaptrix.IPipelineBehavior<global::MyTestApp.ValueTypeRequest, global::MyTestApp.Result>, global::MyTestApp.LoggingBehavior<global::MyTestApp.ValueTypeRequest, global::MyTestApp.Result>>();",
            aot);
    }

    [Fact]
    public void ReferenceTypeResponse_GetsOneToo()
    {
        // Registering only the value-type pairs would leave the open-generic registration in place for
        // the others, and that registration is removed wholesale - the reference-type requests would
        // silently lose their behaviors.
        var (_, sources) = GeneratorTestHelper.RunGeneratorAndCompile(Source, Enabled);

        Assert.Contains(
            "services.AddTransient<global::Synaptrix.IPipelineBehavior<global::MyTestApp.ReferenceTypeRequest, string>, global::MyTestApp.LoggingBehavior<global::MyTestApp.ReferenceTypeRequest, string>>();",
            AotSource(sources));
    }

    [Fact]
    public void OpenGenericRegistrations_AreRemovedFirst()
    {
        var (_, sources) = GeneratorTestHelper.RunGeneratorAndCompile(Source, Enabled);

        Assert.Contains("typeof(global::Synaptrix.IPipelineBehavior<,>)", AotSource(sources));
        Assert.Contains("services.RemoveAt(i);", AotSource(sources));
    }

    [Fact]
    public void WithoutTheProperty_NothingIsEmitted()
    {
        var (_, sources) = GeneratorTestHelper.RunGeneratorAndCompile(Source);

        Assert.Null(AotSource(sources));
    }

    [Fact]
    public void GeneratedCode_Compiles()
    {
        var (diagnostics, _) = GeneratorTestHelper.RunGeneratorAndCompile(Source, Enabled);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }
}
