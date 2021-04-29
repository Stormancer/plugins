// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Stormancer.Server.Plugins.Api.S2SProxyGenerator
{
    /// <summary>
    /// Generates proxy classes for scene to scene actions decorated with <see cref="S2SApiAttribute"/> in Stormancer.
    /// </summary>
    /// <remarks>
    /// Controllers must be decorated with <see cref="ServiceAttribute"/> to specify service locator informations.
    /// If the client code needs to send or receive raw data using the pipe interface, the action shall have a, IS2SRequestContext parameter
    /// decorated with <see cref="S2SContextUsageAttribute"/>. If at least one such parameter exist, the generator will expose the underlaying pipes.
    /// </remarks>
    [Generator]
    public class S2SProxySourceGenerator : ISourceGenerator
    {
        internal enum ReturnType
        {
            TaskT,
            TaskVoid,
            AsyncEnumerable,
            Operation
        }

        private INamedTypeSymbol? GetSymbol(GeneratorExecutionContext context, string assemblyName, string typeName)
        {
            var asm = context.Compilation.References.Select(metadata => context.Compilation.GetAssemblyOrModuleSymbol(metadata)).OfType<IAssemblySymbol>().FirstOrDefault(asm => asm.Identity.Name == assemblyName);

            return asm?.GetTypeByMetadataName(typeName);
        }

        private INamedTypeSymbol? GetSymbol<T>(GeneratorExecutionContext context)
        {
            var apiAsm = context.Compilation.References.Select(metadata => context.Compilation.GetAssemblyOrModuleSymbol(metadata)).OfType<IAssemblySymbol>().FirstOrDefault(asm => asm.Identity.Name == typeof(T).Assembly.GetName().Name);
            return apiAsm?.GetTypeByMetadataName(typeof(T).FullName!);

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        public void Execute(GeneratorExecutionContext context)
        {

            var s2sApiAttribute = GetSymbol<S2SApiAttribute>(context);
            var serviceAttributeSymbol = GetSymbol<ServiceAttribute>(context);
            var contextUsageSymbol = GetSymbol<S2SContextUsageAttribute>(context);
            var iAsyncEnumerableSymbol = GetSymbol(context, "System.Runtime", "System.Collections.Generic.IAsyncEnumerable`1")?.ConstructUnboundGenericType();
            var genericTaskSymbol = GetSymbol(context, "System.Runtime", "System.Threading.Tasks.Task`1")?.ConstructUnboundGenericType();
            var taskSymbol = GetSymbol(context, "System.Runtime", "System.Threading.Tasks.Task");
            var s2sRequestContextSymbol = GetSymbol(context, "Stormancer.Abstractions.Server", "Stormancer.Core.IS2SRequestContext");

            Debug.Assert(taskSymbol != null);

            if (s2sApiAttribute is null) return;
            if (serviceAttributeSymbol is null) return;

            if (!(context.SyntaxReceiver is SyntaxReceiver receiver)) return;

            var buffer = new StringBuilder();

            var classes = new List<string>();
            foreach (var r in receiver.ClassCandidates)
            {
                var model = context.Compilation.GetSemanticModel(r.SyntaxTree);
                if (model.GetDeclaredSymbol(r) is not { } s) continue;

                var serviceAttribute = s.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(serviceAttributeSymbol, a.AttributeClass));

                if (serviceAttribute != null)
                {
                    var generatedSource = Generate(s, serviceAttribute, out var className);
                    var filename = GetFilename(s);
                    context.AddSource(filename, SourceText.From(generatedSource, Encoding.UTF8));
                    classes.Add(className);
                }
            }

            buffer.Clear();
            buffer.Append(@"using Stormancer.Plugins;

namespace Stormancer.Server.Codegen
{
    public class S2SProxyApp
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new S2SProxyPlugin());
        }

        private class S2SProxyPlugin : IHostPlugin
        {
            public void Build(HostPluginBuildContext ctx)
            {
                ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
                {
");
            foreach (var className in classes)
            {
                buffer.Append(@$"builder.Register<{className}>().InstancePerRequest();
");
            }
            buffer.Append(@"                };
            }
        }
    }
}");
            context.AddSource("app.cs", SourceText.From(buffer.ToString(), Encoding.UTF8));

            //var test = @"class Test{}";
            //context.AddSource("test.cs", SourceText.From(test, Encoding.UTF8));
            string GetFilename(INamedTypeSymbol type)
            {
                buffer.Clear();

                //foreach (var part in type.ContainingNamespace.ToDisplayParts())
                //{
                //    if (part.Symbol is { Name: var name } && !string.IsNullOrEmpty(name))
                //    {
                //        buffer.Append(name);
                //        buffer.Append('_');
                //    }
                //}
                buffer.Append(type.Name);
                buffer.Append("_proxy.cs");

                return buffer.ToString();
            }

            string Generate(INamedTypeSymbol type, AttributeData serviceAttribute, out string fullClassName)
            {
                buffer.Clear();

                buffer.Append(@"//AUTOGENERATED FILE: DO NOT CHANGE
using Stormancer.Core;
using Stormancer;
using Stormancer.Server.Plugins.ServiceLocator;
");
                if (!string.IsNullOrEmpty(type.ContainingNamespace.Name))
                {
                    buffer.Append(@"namespace ");
                    buffer.Append(type.ContainingNamespace.ToDisplayString());
                    buffer.Append(@"
{
");
                }

                buffer.Append("    public class ");

                var name = GetControllerName(type);
                var className = name + "Proxy";
                fullClassName = $"{type.ContainingNamespace}.{className}";
                buffer.Append(className);
                buffer.Append($@"
    {{
        private readonly ISceneHost scene;
        private readonly ISerializer serializer;
        private readonly IServiceLocator locator;

        public {className}(
            ISceneHost scene, 
            ISerializer serializer,
            IServiceLocator locator)
        {{
            this.scene = scene;
            this.serializer = serializer;
            this.locator = locator;
        }}
");

                //Debugger.Launch();
                foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(m => m.DeclaredAccessibility == Accessibility.Public))
                {

                    var s2sAttribute = method.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(s2sApiAttribute, a.AttributeClass));

                    if (s2sAttribute != null)
                    {

                        GetServiceAttributeInfos(serviceAttribute, out var serviceType, out var requireServiceInstanceId);

                        serviceType = serviceType ?? name;
                        var route = GetRoute(method);



                        var requestUsageContext = GetContextUsage(method);

                        var returnsOperation = requestUsageContext != 0;

                        if (requestUsageContext.HasFlag(S2SRequestContextUsage.Write) && !method.ReturnsVoid && !SymbolEqualityComparer.Default.Equals(method.ReturnType, taskSymbol))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                new DiagnosticDescriptor("STRM001", "A scene to scene action writing to the requestContext must return void or Task.", "", "Stormancer.Codegen", DiagnosticSeverity.Warning, true)
                                , method.Locations.First()));
                            throw new InvalidOperationException($"Cannot create proxy for {method} : An action writing to the context must return void or Task.");
                        }

                        var operationType = BuildOperationType(method);

                        string returnType = BuildReturnType(method, returnsOperation, operationType, out var resultType);



                        buffer.Append($@"
        public {(!returnsOperation ? "async " : "")}{returnType} {method.Name}(");

                        if (requireServiceInstanceId)
                        {
                            buffer.Append("string serviceInstanceId, ");
                        }
                        foreach (var parameter in method.Parameters)
                        {

                            if (!SymbolEqualityComparer.Default.Equals(parameter.Type, s2sRequestContextSymbol))
                            {
                                buffer.Append($"{parameter.Type} {parameter.Name}, ");
                            }
                        }

                        buffer.Append(@"System.Threading.CancellationToken cancellationToken)
        {
");

                        buffer.Append($@"            var rqTask = locator.StartS2SRequestAsync(""{serviceType}"", {(requireServiceInstanceId ? "serviceInstanceId" : @"""""")}, ""{route}"", cancellationToken);
");

                        buffer.Append($@"            {(!returnsOperation ? "await using " : "")}var result = new {operationType}(rqTask,serializer,async writer=>
            {{
");
                        foreach (var parameter in method.Parameters)
                        {
                            if (!SymbolEqualityComparer.Default.Equals(parameter.Type, s2sRequestContextSymbol))
                            {
                                buffer.Append($@"                await writer.WriteObject({parameter.Name}, serializer, cancellationToken);
");
                            }
                        }

                        buffer.Append(@"
            }, cancellationToken);
");

                        switch (resultType)
                        {
                            case ReturnType.AsyncEnumerable:
                                buffer.Append(@"            await foreach(var value from result.GetResultsAsync(cancellationToken))
            {
                yield return value;
            }");
                                break;
                            case ReturnType.TaskT:
                                buffer.Append(@"            return await result.GetResultAsync(cancellationToken);");
                                break;
                            case ReturnType.TaskVoid:

                                break;
                            case ReturnType.Operation:
                                buffer.Append("            return result;");
                                break;
                        }
                        buffer.Append(@"
        }
");

                    }
                }

                //Close class
                buffer.Append(@"    }
");
                //Close namespace
                if (!string.IsNullOrEmpty(type.ContainingNamespace.Name))
                {
                    buffer.Append(@"}
");
                }

                return buffer.ToString();

            }

            void GetServiceAttributeInfos(AttributeData serviceAttribute, out string? serviceType, out bool requireServiceInstanceId)
            {
                requireServiceInstanceId = ((bool?)serviceAttribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Named").Value.Value) ?? false;
                serviceType = ((string?)serviceAttribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "ServiceType").Value.Value);
            }


            S2SRequestContextUsage GetContextUsage(IMethodSymbol method)
            {
                return method.Parameters
                    .SelectMany(p => p.GetAttributes())
                    .Where(a => SymbolEqualityComparer.Default.Equals(contextUsageSymbol, a.AttributeClass))
                    .Select(a => (S2SRequestContextUsage)a.ConstructorArguments.First().Value!)
                    .Aggregate(S2SRequestContextUsage.None, (agg, v) => agg | v);
            }

            string BuildReturnType(IMethodSymbol method, bool returnsOperation, string operationType, out ReturnType returnType)
            {
                if (returnsOperation)
                {
                    returnType = ReturnType.Operation;


                    return operationType;
                }
                else
                {
                    if (method.ReturnsVoid)
                    {
                        returnType = ReturnType.TaskVoid;
                        return taskSymbol!.ToString();
                    }
                    else
                    {
                        var type = method.ReturnType;
                        if (type.Kind == SymbolKind.NamedType)
                        {
                            var namedType = (INamedTypeSymbol)type;
                            if (namedType.IsGenericType)
                            {
                                switch (namedType.ConstructUnboundGenericType())
                                {
                                    case var t when SymbolEqualityComparer.Default.Equals(t, genericTaskSymbol):
                                        returnType = ReturnType.TaskT;
                                        return namedType.ToString();
                                    case var t when SymbolEqualityComparer.Default.Equals(t, iAsyncEnumerableSymbol):
                                        returnType = ReturnType.AsyncEnumerable;
                                        return namedType.ToString();
                                    default:
                                        break;
                                }

                            }
                            else if (SymbolEqualityComparer.Default.Equals(namedType, taskSymbol))
                            {
                                returnType = ReturnType.TaskVoid;
                                return namedType.ToString();
                            }

                        }
                        returnType = ReturnType.TaskT;
                        return $"System.Threading.Tasks.Task<{type}>";
                    }


                }
            }
            string BuildOperationType(IMethodSymbol method)
            {
                return method switch
                {
                    var m when m.ReturnsVoid => "S2SOperation",
                    var m when !m.ReturnsVoid => m.ReturnType switch
                    {
                        INamedTypeSymbol type when SymbolEqualityComparer.Default.Equals(type, taskSymbol) => "S2SOperation",
                        INamedTypeSymbol type when type.IsGenericType => type.ConstructUnboundGenericType() switch
                        {
                            var t when SymbolEqualityComparer.Default.Equals(t, iAsyncEnumerableSymbol) => $"S2SOperationResults<{type.TypeArguments.First()}>",
                            var t when SymbolEqualityComparer.Default.Equals(t, genericTaskSymbol) => $"S2SOperationResult<{type.TypeArguments.First()}>",
                            { } => $"S2SOperationResult<{type}>"
                        },
                        ITypeSymbol type => $"S2SOperationResult<{type}>"
                    },
                    _ => throw new NotSupportedException($"S2S operation type not found for action {method}.")

                };
            }


            string GetControllerName(INamedTypeSymbol type)
            {
                return type.Name.EndsWith("Controller") ? type.Name.Substring(0, type.Name.Length - "Controller".Length) : type.Name;
            }
            string GetRoute(IMethodSymbol method)
            {
                var s2sAttribute = method.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(s2sApiAttribute, a.AttributeClass));

                if (s2sAttribute != null)
                {
                    var routeValue = s2sAttribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Route");

                    var route = (string?)routeValue.Value.Value;

                    if (!string.IsNullOrEmpty(route))
                    {
                        return route!;
                    }

                }
                return GetControllerName((INamedTypeSymbol)method.ContainingSymbol) + "." + method.Name;

            }
        }






        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        class SyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> ClassCandidates { get; } = new List<ClassDeclarationSyntax>();
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax && classDeclarationSyntax.AttributeLists.Count > 0)
                {
                    ClassCandidates.Add(classDeclarationSyntax);
                }
            }
        }

    }

}
