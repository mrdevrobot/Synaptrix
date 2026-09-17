using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Synaptrix.Generator;

/// <summary>
/// The Synaptrix generator class
/// </summary>
/// <seealso cref="IIncrementalGenerator"/>
[Generator]
// This class is a source generator that automatically registers Synaptrix handlers.
public class SynaptrixGenerator : IIncrementalGenerator
{
    private const string DiscoverAttributeName = "Synaptrix.Attributes.DiscoverSynaptrixHandlersAttribute";

    // Initializes the incremental generator.
    /// <summary>
    /// Initializes the context
    /// </summary>
    /// <param name="context">The context</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if DEBUG
        // Uncomment the following line to enable debugging during development.
        // System.Diagnostics.Debugger.Launch();
#endif
        // Check for the attribute
        var hasAttribute = context.CompilationProvider
            .Select(static (compilation, _) => compilation.Assembly.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == DiscoverAttributeName));

        // Retrieves analyzer config options.
        var compilationAndOptions = context.AnalyzerConfigOptionsProvider
            .Select((options, cancellationToken) => options);

        // Creates syntax provider to find handler classes.
        IncrementalValuesProvider<HandlerInfo?> handlerClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsHandlerCandidate(node),
                transform: static (ctx, ct) => GetHandlerInfo(ctx, ct)
            )
            .Where(static handlerInfo => handlerInfo is not null);

        // Collects all handler info.
        IncrementalValueProvider<ImmutableArray<HandlerInfo>> collectedHandlers = handlerClasses.Collect()
            .Select((handlers, _) => handlers.Where(h => h is not null).Select(h => h!).ToImmutableArray());

        // Combines collected handlers with compilation options and attribute check.
        var combinedProvider = collectedHandlers
            .Combine(compilationAndOptions)
            .Combine(context.CompilationProvider)
            .Combine(hasAttribute);

        // Registers the source output.
        context.RegisterSourceOutput(combinedProvider, (ctx, source) =>
        {
            var ((handlersAndOptions, compilation), shouldGenerate) = source;
            var (handlers, options) = handlersAndOptions;

            if (!shouldGenerate)
            {
                return;
            }

            // Default method name for registering handlers.
            var methodName = "AddSynaptrixHandlers";
            // Default namespace for generated code.
            var generatedNamespace = "SynaptrixGenerated";

            // Reads custom method name from build properties if specified.
            if (options.GlobalOptions.TryGetValue("build_property.Synaptrixgeneratedmethodname", out var customMethodName) && !string.IsNullOrWhiteSpace(customMethodName))
            {
                methodName = customMethodName;
            }

            // Reads root namespace from build properties, otherwise uses project name.
            if (options.GlobalOptions.TryGetValue("build_property.rootnamespace", out var projectRootNamespace) && !string.IsNullOrWhiteSpace(projectRootNamespace))
            {
                generatedNamespace = projectRootNamespace;
            }
            else if (options.GlobalOptions.TryGetValue("build_property.msbuildprojectname", out var projectName) && !string.IsNullOrWhiteSpace(projectName))
            {
                generatedNamespace = projectName;
            }

            // Generates the source code for registering handlers.
            var sourceCode = GenerateHandlersRegistrationCode(methodName, generatedNamespace, handlers, compilation);
            ctx.AddSource("SynaptrixGeneratedHandlersRegistrations.g.cs", SourceText.From(sourceCode, Encoding.UTF8));

            // Generate the concrete mediator class.
            if (!handlers.IsEmpty)
            {
                var mediatorCode = GenerateConcreteMediatorCode(generatedNamespace, handlers);
                ctx.AddSource("SynaptrixGeneratedMediator.g.cs", SourceText.From(mediatorCode, Encoding.UTF8));
            }

            // Opt-in, and only worth it in the composition root: the closed registrations cover every
            // request this assembly can see, referenced assemblies included, so emitting them anywhere
            // else would just repeat them.
            if (options.GlobalOptions.TryGetValue("build_property.SynaptrixAotPipelineBehaviors", out var aotBehaviors)
                && aotBehaviors.Equals("true", System.StringComparison.OrdinalIgnoreCase))
            {
                var aotCode = AotPipelineBehaviorEmitter.Generate(generatedNamespace, compilation);
                if (aotCode is not null)
                {
                    ctx.AddSource("SynaptrixAotPipelineBehaviors.g.cs", SourceText.From(aotCode, Encoding.UTF8));
                }
            }
        });
    }

    // Checks if a syntax node is a candidate for a handler.
    /// <summary>
    /// Ises the handler candidate using the specified node
    /// </summary>
    /// <param name="node">The node</param>
    /// <returns>The bool</returns>
    private static bool IsHandlerCandidate(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { BaseList: not null };
    }

    // Retrieves handler information from a syntax context.
    /// <summary>
    /// Gets the handler info using the specified context
    /// </summary>
    /// <param name="context">The context</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The handler info</returns>
    private static HandlerInfo? GetHandlerInfo(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        // Gets the declared symbol for the class.
        if (semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        if (classSymbol.IsAbstract)
        {
            return null;
        }

        var implementedInterfaces = new List<string>();
        var skippedNotes = new List<string>();

        // An open-generic class such as `Foo<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>`
        // cannot be registered with the generic `AddTransient<TInterface, TImpl>()` overload because
        // TRequest/TResponse are not in scope at the call site. We emit the unbound form
        // `typeof(Foo<,>)` and `typeof(IPipelineBehavior<,>)` instead - but only when that mapping is
        // actually valid; see RegisterOrSkipOpenGeneric below.
        var isOpenGeneric = classSymbol.IsGenericType && classSymbol.TypeParameters.Length > 0;

        // For an open-generic handler, checks whether `unboundInterfaceName` can be safely registered
        // via `services.AddTransient(typeof(IFoo<,>), typeof(Impl<,>))` and routes it to
        // implementedInterfaces (if so) or skippedNotes with a reason (if not). .NET's open-generic DI
        // resolution binds the two type-parameter lists purely positionally: closing IFoo<A,B> and
        // asking for it constructs Impl<A,B>, substituting Impl's own type parameters in declaration
        // order. That's only correct when each interface slot IS, directly and in the matching
        // position, one of Impl's own type parameters - not a constructed type wrapping it (e.g.
        // `IFoo<Command<T>, T>` on `Impl<T>`) and not a swapped order. Reject anything else instead of
        // emitting a registration that either throws ArgumentException at BuildServiceProvider() time
        // (arity mismatch) or silently fails to resolve at the call site (wrapped/swapped slots).
        void RegisterOrSkipOpenGeneric(string unboundInterfaceName, ImmutableArray<ITypeSymbol> interfaceTypeArguments)
        {
            var classTypeParameters = classSymbol.TypeParameters;

            if (interfaceTypeArguments.Length != classTypeParameters.Length)
            {
                skippedNotes.Add(
                    $"Skipped: {DescribeUnboundImplementation(classSymbol)} has {classTypeParameters.Length} type parameter(s) but " +
                    $"{unboundInterfaceName} needs {interfaceTypeArguments.Length} (arity mismatch) - .NET can't open-generic-map this shape.");
                return;
            }

            for (int i = 0; i < interfaceTypeArguments.Length; i++)
            {
                if (interfaceTypeArguments[i] is not ITypeParameterSymbol slotTypeParameter ||
                    !SymbolEqualityComparer.Default.Equals(slotTypeParameter, classTypeParameters[i]))
                {
                    skippedNotes.Add(
                        $"Skipped: {DescribeUnboundImplementation(classSymbol)}'s type parameters don't map directly, " +
                        $"position-for-position, onto {unboundInterfaceName}'s slots (slot {i} is wrapped in another " +
                        "constructed type, or the order differs) - .NET's open-generic DI mapping only supports the direct case.");
                    return;
                }
            }

            implementedInterfaces.Add(unboundInterfaceName);
        }

        // Iterates through all implemented interfaces.
        foreach (var @interface in classSymbol.AllInterfaces)
        {
            if (@interface.IsGenericType)
            {
                var genericDefinition = @interface.ConstructedFrom;
                var genericDefinitionFullName = genericDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                // Checks if the interface is a Synaptrix handler interface.
                if (genericDefinitionFullName == "global::Synaptrix.IRequestHandler<TRequest, TResponse>")
                {
                    if (isOpenGeneric)
                    {
                        RegisterOrSkipOpenGeneric("global::Synaptrix.IRequestHandler<,>", @interface.TypeArguments);
                    }
                    else
                    {
                        var requestType = @interface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        var responseType = @interface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        implementedInterfaces.Add($"global::Synaptrix.IRequestHandler<{requestType}, {responseType}>");
                    }
                }
                else if (genericDefinitionFullName == "global::Synaptrix.IRequestHandler<TRequest>")
                {
                    if (isOpenGeneric)
                    {
                        RegisterOrSkipOpenGeneric("global::Synaptrix.IRequestHandler<>", @interface.TypeArguments);
                    }
                    else
                    {
                        var requestType = @interface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        implementedInterfaces.Add($"global::Synaptrix.IRequestHandler<{requestType}>");
                    }
                }
                else if (genericDefinitionFullName == "global::Synaptrix.INotificationHandler<TNotification>")
                {
                    if (isOpenGeneric)
                    {
                        RegisterOrSkipOpenGeneric("global::Synaptrix.INotificationHandler<>", @interface.TypeArguments);
                    }
                    else
                    {
                        var notificationType = @interface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        implementedInterfaces.Add($"global::Synaptrix.INotificationHandler<{notificationType}>");
                    }
                }
                else if (genericDefinitionFullName == "global::Synaptrix.IPipelineBehavior<TRequest, TResponse>")
                {
                    if (isOpenGeneric)
                    {
                        RegisterOrSkipOpenGeneric("global::Synaptrix.IPipelineBehavior<,>", @interface.TypeArguments);
                    }
                    else
                    {
                        var requestType = @interface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        var responseType = @interface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        implementedInterfaces.Add($"global::Synaptrix.IPipelineBehavior<{requestType}, {responseType}>");
                    }
                }
                else if (genericDefinitionFullName == "global::Synaptrix.IStreamRequestHandler<TRequest, TResponse>")
                {
                    if (isOpenGeneric)
                    {
                        RegisterOrSkipOpenGeneric("global::Synaptrix.IStreamRequestHandler<,>", @interface.TypeArguments);
                    }
                    else
                    {
                        var requestType = @interface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        var responseType = @interface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        implementedInterfaces.Add($"global::Synaptrix.IStreamRequestHandler<{requestType}, {responseType}>");
                    }
                }
            }
        }

        // Creates a HandlerInfo if any supported interfaces are implemented, or if any were skipped
        // (so the skip reason still gets emitted as a diagnostic comment in the generated file).
        if (implementedInterfaces.Any() || skippedNotes.Any())
        {
            string implementationTypeName;
            if (isOpenGeneric)
            {
                // Unbound generic form, e.g. "global::MyNs.Behavior<,>".
                implementationTypeName = classSymbol.ConstructUnboundGenericType()
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
            else
            {
                implementationTypeName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            return new HandlerInfo(implementationTypeName, implementedInterfaces, isOpenGeneric, skippedNotes.Any() ? skippedNotes : null);
        }

        return null;
    }

    /// <summary>
    /// Unbound generic display form of <paramref name="classSymbol"/>, e.g. "global::MyNs.Behavior&lt;,&gt;",
    /// for use in diagnostic skip-reason messages.
    /// </summary>
    private static string DescribeUnboundImplementation(INamedTypeSymbol classSymbol) =>
        classSymbol.ConstructUnboundGenericType().ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    // Generates the handlers registration code.
    /// <summary>
    /// Generates the handlers registration code using the specified method name
    /// </summary>
    /// <param name="methodName">The method name</param>
    /// <param name="generatedNamespace">The generated namespace</param>
    /// <param name="handlers">The handlers</param>
    /// <param name="compilation">The compilation to check references</param>
    /// <returns>The string</returns>
    private static string GenerateHandlersRegistrationCode(string methodName, string generatedNamespace, ImmutableArray<HandlerInfo> handlers, Compilation compilation)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// This file is automatically generated by Synaptrix.Generator.");
        sb.AppendLine("// Do not modify this file manually.");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Synaptrix;");
        sb.AppendLine();
        sb.AppendLine($"namespace {generatedNamespace}.Generated");
        sb.AppendLine("{");
        sb.AppendLine($"    public static class SynaptrixGeneratedRegistrations");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Automatically registers Synaptrix handlers.");
        sb.AppendLine("        /// This method is generated at compile time by the Source Generator.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"services\">The service collection to add to.</param>");
        sb.AppendLine("        /// <param name=\"mediatorLifetime\">");
        sb.AppendLine("        /// Lifetime for IMediator / ISender / GeneratedMediator.");
        sb.AppendLine("        /// <list type=\"bullet\">");
        sb.AppendLine("        /// <item><term>Scoped (default)</term><description>Per-scope instance. Correct for ASP.NET Core (per-request) and safe for desktop apps with a single root scope.</description></item>");
        sb.AppendLine("        /// <item><term>Singleton</term><description>One instance per container. Use only when the app never creates child scopes that contain scoped services.</description></item>");
        sb.AppendLine("        /// <item><term>Transient</term><description>New instance on every resolution. Avoids any scope-capture issue at the cost of a small allocation per dispatch.</description></item>");
        sb.AppendLine("        /// </list>");
        sb.AppendLine("        /// </param>");
        sb.AppendLine("        /// <returns>The modified service collection.</returns>");
        sb.AppendLine($"        public static IServiceCollection {methodName}(this IServiceCollection services, ServiceLifetime mediatorLifetime = ServiceLifetime.Scoped)");
        sb.AppendLine("        {");

        // Registers each handler with its implemented interfaces.
        // Guards prevent duplicate registrations when this method is called multiple times
        // through the recursive assembly chain (each assembly in the dependency graph that
        // references this one will call AddSynaptrixHandlers, which in turn calls this method).
        foreach (var handler in handlers)
        {
            // By the time a handler reaches this point, GetHandlerInfo has already rejected any
            // open-generic interface mapping .NET's DI can't actually construct (arity mismatch, or
            // a slot that isn't a direct positional type-parameter reference) - see
            // RegisterOrSkipOpenGeneric. Every entry left in ImplementedInterfaceTypeNames is safe
            // to register as-is; SkippedRegistrationNotes carries the diagnostic for anything that
            // was rejected, printed below instead of a registration.
            foreach (var implementedInterface in handler.ImplementedInterfaceTypeNames)
            {
                if (handler.IsOpenGeneric)
                {
                    // Open-generic registration: AddTransient(typeof(IFoo<,>), typeof(MyFoo<,>)).
                    // Guard: prevents duplicate behavior registrations (e.g. IPipelineBehavior<,>)
                    // which would cause the behavior to execute N times per request.
                    sb.AppendLine($"            if (!services.Any(d => d.ServiceType == typeof({implementedInterface}) && d.ImplementationType == typeof({handler.ImplementationTypeName})))");
                    sb.AppendLine($"                services.AddTransient(typeof({implementedInterface}), typeof({handler.ImplementationTypeName}));");
                }
                else
                {
                    sb.AppendLine($"            if (!services.Any(d => d.ServiceType == typeof({implementedInterface}) && d.ImplementationType == typeof({handler.ImplementationTypeName})))");
                    sb.AppendLine($"                services.AddTransient<{implementedInterface}, {handler.ImplementationTypeName}>();");
                }
            }

            if (handler.SkippedRegistrationNotes is { Count: > 0 })
            {
                foreach (var note in handler.SkippedRegistrationNotes)
                {
                    sb.AppendLine($"            // {note}");
                }
            }
        }

        // Recursive application functionality
        sb.AppendLine();
        sb.AppendLine("            // Register handlers from referenced assemblies");
        foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            // Check if the referenced assembly has the attribute
            bool shouldScan = referencedAssembly.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == DiscoverAttributeName);

            if (shouldScan)
            {
                // We assume default namespace conventions or we need to find the specific class. 
                // Since we can't easily know the namespace of the referenced assembly's generated code without scanning it, 
                // we'll try to guess based on assembly name or build properties if accessible (not easily accessible here).
                
                var refNamespace = referencedAssembly.Name; // Default default
                // Checking if the class exists (with .Generated suffix)
                var candidateType = compilation.GetTypeByMetadataName($"{refNamespace}.Generated.SynaptrixGeneratedRegistrations");

                // If not found, maybe it used "SynaptrixGenerated" default?
                if (candidateType == null)
                {
                   candidateType = compilation.GetTypeByMetadataName("SynaptrixGenerated.Generated.SynaptrixGeneratedRegistrations");
                }

                if (candidateType != null)
                {
                     // Fix: Dynamic discovery of the registration method
                     // We look for a public static method that returns IServiceCollection and takes IServiceCollection as parameter
                     // Prefer the 2-param overload (new signature with mediatorLifetime).
                     // Fall back to the 1-param overload for assemblies compiled with an older
                     // version of the generator (backward compatibility).
                     var registrationMethod2 = candidateType.GetMembers()
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault(m =>
                            m.IsStatic &&
                            m.DeclaredAccessibility == Accessibility.Public &&
                            m.ReturnType.Name == "IServiceCollection" &&
                            m.Parameters.Length == 2 &&
                            m.Parameters[0].Type.Name == "IServiceCollection" &&
                            m.Parameters[1].Type.Name == "ServiceLifetime");

                     var registrationMethod1 = registrationMethod2 == null
                         ? candidateType.GetMembers()
                               .OfType<IMethodSymbol>()
                               .FirstOrDefault(m =>
                                   m.IsStatic &&
                                   m.DeclaredAccessibility == Accessibility.Public &&
                                   m.ReturnType.Name == "IServiceCollection" &&
                                   m.Parameters.Length == 1 &&
                                   m.Parameters[0].Type.Name == "IServiceCollection")
                         : null;

                     if (registrationMethod2 != null)
                     {
                         sb.AppendLine($"            {candidateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{registrationMethod2.Name}(services, mediatorLifetime);");
                     }
                     else if (registrationMethod1 != null)
                     {
                         sb.AppendLine($"            {candidateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{registrationMethod1.Name}(services);");
                     }
                     else
                     {
                         // Fallback: assume new signature (safest default for unknown assemblies)
                         sb.AppendLine($"            {candidateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.AddSynaptrixHandlers(services, mediatorLifetime);");
                     }
                }
            }
        }

        sb.AppendLine();

        // Collect unique concrete handler types.
        // Open-generic implementations (e.g. pipeline behaviors) are registered above
        // via `AddTransient(typeof(...), typeof(...))` against their interfaces; they
        // cannot be registered standalone with the generic `AddTransient<T>()` overload
        // because their type arguments are unbound.
        var uniqueHandlers = handlers
            .Where(h => !h.IsOpenGeneric)
            .Select(h => h.ImplementationTypeName)
            .Distinct()
            .ToList();

        // Register each concrete handler type as Transient.
        // Handlers are resolved lazily by GeneratedMediator via IServiceProvider,
        // breaking circular dependencies (e.g. Handler → Datasource → IMediator → Handler).
        // Guard: prevents duplicate concrete registrations across the assembly chain.
        sb.AppendLine("            // Transient registrations for handler types.");
        foreach (var handlerType in uniqueHandlers)
        {
            sb.AppendLine($"            if (!services.Any(d => d.ServiceType == typeof({handlerType}) && d.ImplementationType == typeof({handlerType})))");
            sb.AppendLine($"                services.AddTransient<{handlerType}>();");
        }

        sb.AppendLine();
        sb.AppendLine("            // Register INotificationPublisher for use with the non-generated Mediator class.");
        sb.AppendLine("            if (!services.Any(d => d.ServiceType == typeof(global::Synaptrix.INotificationPublisher)))");
        sb.AppendLine("            {");
        sb.AppendLine("                services.AddSingleton<global::Synaptrix.INotificationPublisher, global::Synaptrix.ForeachAwaitPublisher>();");
        sb.AppendLine("            }");

        sb.AppendLine();
        if (handlers.IsEmpty)
        {
            // No local handlers: GeneratedMediator is not produced for this project.
            // Fall back to the reflection-based Mediator so IMediator/ISender are still resolvable.
            sb.AppendLine("            // No local handlers found: register the standard Mediator as fallback.");
            sb.AppendLine("            if (!services.Any(d => d.ServiceType == typeof(global::Synaptrix.IMediator)))");
            sb.AppendLine("            {");
            sb.AppendLine("                services.AddTransient<global::Synaptrix.IMediator, global::Synaptrix.Mediator>();");
            sb.AppendLine("                services.AddTransient<global::Synaptrix.ISender, global::Synaptrix.Mediator>();");
            sb.AppendLine("            }");
        }
        else
        {
            // Register GeneratedMediator with the lifetime chosen by the caller.
            // Default is Scoped: correct for ASP.NET Core (per-request) and behaves like
            // Singleton in desktop apps that use a single root scope (e.g. Avalonia + Lifter).
            // Singleton is still available for apps that never create child scopes with
            // scoped services. Transient avoids any scope-capture issue at a negligible cost.
            sb.AppendLine("            // Register the source-generated mediator with the requested lifetime.");
            sb.AppendLine($"            switch (mediatorLifetime)");
            sb.AppendLine("            {");
            sb.AppendLine("                case global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton:");
            sb.AppendLine($"                    services.AddSingleton<global::{generatedNamespace}.Generated.GeneratedMediator>();");
            sb.AppendLine($"                    services.AddSingleton<global::Synaptrix.IMediator>(static sp => sp.GetRequiredService<global::{generatedNamespace}.Generated.GeneratedMediator>());");
            sb.AppendLine($"                    services.AddSingleton<global::Synaptrix.ISender>(static sp => sp.GetRequiredService<global::{generatedNamespace}.Generated.GeneratedMediator>());");
            sb.AppendLine("                    break;");
            sb.AppendLine("                case global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient:");
            sb.AppendLine($"                    services.AddTransient<global::{generatedNamespace}.Generated.GeneratedMediator>();");
            sb.AppendLine($"                    services.AddTransient<global::Synaptrix.IMediator>(static sp => sp.GetRequiredService<global::{generatedNamespace}.Generated.GeneratedMediator>());");
            sb.AppendLine($"                    services.AddTransient<global::Synaptrix.ISender>(static sp => sp.GetRequiredService<global::{generatedNamespace}.Generated.GeneratedMediator>());");
            sb.AppendLine("                    break;");
            sb.AppendLine("                default: // Scoped");
            sb.AppendLine($"                    services.AddScoped<global::{generatedNamespace}.Generated.GeneratedMediator>();");
            sb.AppendLine($"                    services.AddScoped<global::Synaptrix.IMediator>(static sp => sp.GetRequiredService<global::{generatedNamespace}.Generated.GeneratedMediator>());");
            sb.AppendLine($"                    services.AddScoped<global::Synaptrix.ISender>(static sp => sp.GetRequiredService<global::{generatedNamespace}.Generated.GeneratedMediator>());");
            sb.AppendLine("                    break;");
            sb.AppendLine("            }");
        }

        sb.AppendLine();
        sb.AppendLine("            return services;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ── Concrete mediator generation ───────────────────────────────────────────────

    private static string GenerateConcreteMediatorCode(string generatedNamespace, ImmutableArray<HandlerInfo> handlers)
    {
        // ── Build request/notification mappings ───────────────────────────────
        var responseHandlers     = new List<(string ReqType, string RespType, string ImplType)>();
        var voidHandlers         = new List<(string ReqType, string ImplType)>();
        var notifHandlersByType  = new Dictionary<string, List<string>>();  // notifType → implTypes
        var streamHandlers       = new List<(string ReqType, string RespType, string ImplType)>();

        foreach (var h in handlers)
        {
            // Open-generic implementations (e.g. pipeline behaviors) have no concrete
            // dispatch entry — they participate at runtime through DI resolution.
            if (h.IsOpenGeneric) continue;

            foreach (var iface in h.ImplementedInterfaceTypeNames)
            {
                if (iface.StartsWith("global::Synaptrix.IRequestHandler<"))
                {
                    var inner    = iface.Substring("global::Synaptrix.IRequestHandler<".Length, iface.Length - "global::Synaptrix.IRequestHandler<".Length - 1);
                    var commaIdx = FindTopLevelComma(inner);
                    if (commaIdx >= 0)
                        responseHandlers.Add((inner.Substring(0, commaIdx).Trim(), inner.Substring(commaIdx + 1).Trim(), h.ImplementationTypeName));
                    else
                        voidHandlers.Add((inner.Trim(), h.ImplementationTypeName));
                }
                else if (iface.StartsWith("global::Synaptrix.INotificationHandler<"))
                {
                    var notifType = iface.Substring("global::Synaptrix.INotificationHandler<".Length, iface.Length - "global::Synaptrix.INotificationHandler<".Length - 1).Trim();
                    if (!notifHandlersByType.ContainsKey(notifType))
                        notifHandlersByType[notifType] = new List<string>();
                    notifHandlersByType[notifType].Add(h.ImplementationTypeName);
                }
                else if (iface.StartsWith("global::Synaptrix.IStreamRequestHandler<"))
                {
                    var inner    = iface.Substring("global::Synaptrix.IStreamRequestHandler<".Length, iface.Length - "global::Synaptrix.IStreamRequestHandler<".Length - 1);
                    var commaIdx = FindTopLevelComma(inner);
                    if (commaIdx >= 0)
                        streamHandlers.Add((inner.Substring(0, commaIdx).Trim(), inner.Substring(commaIdx + 1).Trim(), h.ImplementationTypeName));
                }
            }
        }

        // ── Emit source ────────────────────────────────────────────────────────
        var sb = new StringBuilder();
        sb.AppendLine("// This file is automatically generated by Synaptrix.Generator.");
        sb.AppendLine("// Do not modify this file manually.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {generatedNamespace}.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Source-generated mediator with direct handler dispatch.");
        sb.AppendLine("    /// Handlers are resolved lazily from IServiceProvider to break circular dependency chains");
        sb.AppendLine("    /// (e.g. Handler → Datasource → IMediator → Handler).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public sealed partial class GeneratedMediator : global::Synaptrix.IMediator, global::Synaptrix.ISender");
        sb.AppendLine("    {");

        // ── Field ──────────────────────────────────────────────────────────────
        sb.AppendLine("        private readonly global::System.IServiceProvider _sp;");
        sb.AppendLine("        private global::Synaptrix.Mediator? _fallback;");
        sb.AppendLine();

        // ── Constructor ────────────────────────────────────────────────────────
        sb.AppendLine("        public GeneratedMediator(global::System.IServiceProvider sp)");
        sb.AppendLine("        {");
        sb.AppendLine("            _sp = sp;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // ── Fallback ───────────────────────────────────────────────────────────
        // A request/notification type not in this project's own switch below is not
        // necessarily unregistered - it may be handled by a handler discovered from a
        // different referenced assembly (composed additively into the same IServiceCollection),
        // or by an open-generic handler that can only be resolved by its closed type at
        // runtime. Either way it's still reachable through the DI container, just not through
        // this assembly's own compile-time-known switch, so fall back to the reflection-based
        // mediator instead of failing outright.
        sb.AppendLine("        private global::Synaptrix.Mediator __Fallback => _fallback ??= new global::Synaptrix.Mediator(_sp);");
        sb.AppendLine();

        // ── Pipeline helpers ───────────────────────────────────────────────────
        // Run an IRequest<TResponse> handler with the registered pipeline behaviors
        // composed around it (matches Synaptrix.Core runtime Mediator semantics).
        sb.AppendLine("        private async global::System.Threading.Tasks.ValueTask<TR> __DispatchResponseAsync<TQ, TR, TH>(TQ request, global::System.Threading.CancellationToken cancellationToken)");
        sb.AppendLine("            where TQ : global::Synaptrix.IRequest<TR>");
        sb.AppendLine("            where TH : global::Synaptrix.IRequestHandler<TQ, TR>");
        sb.AppendLine("        {");
        sb.AppendLine("            var handler = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<TH>(_sp);");
        sb.AppendLine("            var behaviors = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetServices<global::Synaptrix.IPipelineBehavior<TQ, TR>>(_sp);");
        sb.AppendLine("            if (behaviors is global::Synaptrix.IPipelineBehavior<TQ, TR>[] arr && arr.Length == 0)");
        sb.AppendLine("                return await handler.Handle(request, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("            global::Synaptrix.RequestHandlerDelegate<TR> pipeline = ct => handler.Handle(request, ct);");
        sb.AppendLine("            var list = behaviors as global::System.Collections.Generic.IList<global::Synaptrix.IPipelineBehavior<TQ, TR>>");
        sb.AppendLine("                       ?? new global::System.Collections.Generic.List<global::Synaptrix.IPipelineBehavior<TQ, TR>>(behaviors);");
        sb.AppendLine("            for (int i = list.Count - 1; i >= 0; i--)");
        sb.AppendLine("            {");
        sb.AppendLine("                var b = list[i];");
        sb.AppendLine("                var prev = pipeline;");
        sb.AppendLine("                pipeline = ct => b.Handle(request, prev, ct);");
        sb.AppendLine("            }");
        sb.AppendLine("            return await pipeline(cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // ── Send<TResponse>(IRequest<TResponse>) ──────────────────────────────
        sb.AppendLine("        public async global::System.Threading.Tasks.ValueTask<TResponse> Send<TResponse>(global::Synaptrix.IRequest<TResponse> request, global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        for (int i = 0; i < responseHandlers.Count; i++)
        {
            var (reqType, respType, implType) = responseHandlers[i];
            sb.AppendLine($"            if (request is {reqType} r{i})");
            sb.AppendLine($"                return (TResponse)(object)await __DispatchResponseAsync<{reqType}, {respType}, {implType}>(r{i}, cancellationToken).ConfigureAwait(false);");
        }
        sb.AppendLine("            return await __Fallback.Send(request, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // ── Send(IRequest) ─────────────────────────────────────────────────────
        sb.AppendLine("        public async global::System.Threading.Tasks.ValueTask Send(global::Synaptrix.IRequest request, global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        for (int i = 0; i < voidHandlers.Count; i++)
        {
            var (reqType, implType) = voidHandlers[i];
            sb.AppendLine($"            if (request is {reqType} v{i}) {{ await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{implType}>(_sp).Handle(v{i}, cancellationToken).ConfigureAwait(false); return; }}");
        }
        for (int i = 0; i < responseHandlers.Count; i++)
        {
            var (reqType, respType, implType) = responseHandlers[i];
            sb.AppendLine($"            if (request is {reqType} rv{i}) {{ await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{implType}>(_sp).Handle(rv{i}, cancellationToken).ConfigureAwait(false); return; }}");
        }
        sb.AppendLine("            await __Fallback.Send(request, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // ── Send(object) ───────────────────────────────────────────────────────
        sb.AppendLine("        public async global::System.Threading.Tasks.ValueTask<object?> Send(object request, global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        for (int i = 0; i < responseHandlers.Count; i++)
        {
            var (reqType, respType, implType) = responseHandlers[i];
            sb.AppendLine($"            if (request is {reqType} o{i})");
            sb.AppendLine($"                return (object?)await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{implType}>(_sp).Handle(o{i}, cancellationToken).ConfigureAwait(false);");
        }
        for (int i = 0; i < voidHandlers.Count; i++)
        {
            var (reqType, implType) = voidHandlers[i];
            sb.AppendLine($"            if (request is {reqType} ov{i}) {{ await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{implType}>(_sp).Handle(ov{i}, cancellationToken).ConfigureAwait(false); return null; }}");
        }
        sb.AppendLine("            return await __Fallback.Send(request, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // ── Publish(INotification) — lazy resolve, sequential dispatch with sync fast-path ──
        sb.AppendLine("        public global::System.Threading.Tasks.ValueTask Publish(global::Synaptrix.INotification notification, global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        foreach (var kvp in notifHandlersByType)
        {
            var safe  = GetSafeIdentifier(kvp.Key);
            var hList = kvp.Value;
            sb.AppendLine($"            if (notification is {kvp.Key} _n_{safe})");
            sb.AppendLine("            {");
            for (int i = 0; i < hList.Count; i++)
            {
                sb.AppendLine($"                var _t{i}_{safe} = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{hList[i]}>(_sp).Handle(_n_{safe}, cancellationToken);");
                if (i < hList.Count - 1)
                {
                    sb.AppendLine($"                if (!_t{i}_{safe}.IsCompletedSuccessfully) return _FinishPublish_{safe}_From{i}(_n_{safe}, _t{i}_{safe}, cancellationToken);");
                }
                else
                {
                    sb.AppendLine($"                if (_t{i}_{safe}.IsCompletedSuccessfully) return default;");
                    sb.AppendLine($"                return _t{i}_{safe};");
                }
            }
            if (hList.Count == 0)
                sb.AppendLine("                return default;");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            return __Fallback.Publish(notification, cancellationToken);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // ── Async slow-path helpers for Publish ────────────────────────────────
        foreach (var kvp in notifHandlersByType)
        {
            var safe  = GetSafeIdentifier(kvp.Key);
            var hList = kvp.Value;
            for (int fromIdx = 0; fromIdx < hList.Count - 1; fromIdx++)
            {
                sb.AppendLine($"        private async global::System.Threading.Tasks.ValueTask _FinishPublish_{safe}_From{fromIdx}({kvp.Key} notification, global::System.Threading.Tasks.ValueTask pending, global::System.Threading.CancellationToken cancellationToken)");
                sb.AppendLine("        {");
                sb.AppendLine("            await pending.ConfigureAwait(false);");
                for (int j = fromIdx + 1; j < hList.Count; j++)
                    sb.AppendLine($"            await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{hList[j]}>(_sp).Handle(notification, cancellationToken).ConfigureAwait(false);");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
        }

        // ── CreateStream<TResponse>(IStreamRequest<TResponse>) ─────────────────
        sb.AppendLine("        public global::System.Collections.Generic.IAsyncEnumerable<TResponse> CreateStream<TResponse>(global::Synaptrix.IStreamRequest<TResponse> request, global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("        {");
        for (int i = 0; i < streamHandlers.Count; i++)
        {
            var (reqType, respType, implType) = streamHandlers[i];
            sb.AppendLine($"            if (request is {reqType} s{i})");
            sb.AppendLine($"                return (global::System.Collections.Generic.IAsyncEnumerable<TResponse>)global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{implType}>(_sp).Handle(s{i}, cancellationToken);");
        }
        sb.AppendLine("            return __Fallback.CreateStream(request, cancellationToken);");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────

    /// <summary>Returns the simple (unqualified) type name from a fully qualified name.</summary>
    private static string GetSimpleName(string fqn)
    {
        var lastDot = fqn.LastIndexOf('.');
        return lastDot >= 0 ? fqn.Substring(lastDot + 1) : fqn;
    }

    /// <summary>Lower-cases the first character of <paramref name="s"/>.</summary>
    private static string Uncapitalize(string s)
        => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

    /// <summary>
    /// Finds the index of the first top-level comma (depth 0 relative to angle brackets)
    /// in <paramref name="s"/>. Returns -1 if not found.
    /// </summary>
    private static int FindTopLevelComma(string s)
    {
        int depth = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if      (s[i] == '<') depth++;
            else if (s[i] == '>') depth--;
            else if (s[i] == ',' && depth == 0) return i;
        }
        return -1;
    }

    /// <summary>
    /// Converts <paramref name="typeName"/> into a valid C# identifier by replacing
    /// non-alphanumeric characters with underscores and collapsing consecutive underscores.
    /// </summary>
    private static string GetSafeIdentifier(string typeName)
    {
        var sb = new StringBuilder(typeName.Length);
        foreach (var c in typeName)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        var result = sb.ToString();
        // Collapse runs of underscores.
        while (result.Contains("__"))
            result = result.Replace("__", "_");
        return result.Trim('_');
    }
}