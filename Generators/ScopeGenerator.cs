using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Generators
{


    
    
    public static class ScopedCodeCodeBuilding
    {


        
        
        static  IncrementalValuesProvider<IMethodSymbol> FilterMethods(this IncrementalGeneratorInitializationContext context)
        {
            return context.SyntaxProvider.CreateSyntaxProvider(
                    predicate: static (node, _) =>
                        node is MethodDeclarationSyntax m &&
                        (
                            m.AttributeLists.Count > 0 ||
                            m.ParameterList.Parameters.Any(p => p.AttributeLists.Count > 0)
                        ),

                    transform: static (ctx, _) =>
                    {
                        var methodSyntax = (MethodDeclarationSyntax)ctx.Node;
                        var symbol = ctx.SemanticModel.GetDeclaredSymbol(methodSyntax);
                        return symbol as IMethodSymbol;
                    })
                .Where(s => s is not null)
                .Select((s, _) => s!)
                .Where(method =>
                    method.HasScopedAttribute() ||
                    method.Parameters.Any(p => p.HasScopedAttribute()));
        }
        
        
        public static void GenerateScopedMethods(this IncrementalGeneratorInitializationContext context)
        {
            var methods = context.FilterMethods();
            
            context.RegisterSourceOutput(methods, (ctx, method) =>
            {
            
                
                var code = method.ScopedMethod();
                
                var file = method.ScopedGetFileName();
                
                ctx.AddSource(file, code);
                
            });
        }



        
        
        
        static (IMethodSymbol method, string generatedCode) ScopedCallChild
            (this (IMethodSymbol method, string generatedCode) inp) => 
            (inp.method,
                ScopedCallChildMethodTemplate(
                        inp.method.ReturnsVoid,
                        inp.method.ScopedCallMethodName(), 
                        inp.method.ScopedMethodInlinedCallArgs())
                
                );
        
        
        static (IMethodSymbol method, string generatedCode) ScopedCreatingScope
            (this (IMethodSymbol method, string generatedCode) inp) => 
            (inp.method,
                
                inp.method.HasScopedAttribute() 
                    ? ScopedCreateScopeTemplate(inp.generatedCode, inp.method.ScopedGetScopeName())
                    : inp.generatedCode
                
            );
        
        

        
        static (IMethodSymbol method, string generatedCode) ScopedResolveValues
            (this (IMethodSymbol method, string generatedCode) inp) => 
            (inp.method,
                
                ScopedResolveValues(
                        inp.generatedCode, 
                        inp.method
                            .ScopedValuedParameters()
                            .Select(p => (p.Type.ToDisplayString(), p.Name)))
                
            );
        
        
        static (IMethodSymbol method, string generatedCode) ScopedResolveFrozenValues
            (this (IMethodSymbol method, string generatedCode) inp) => 
            (inp.method,
                
                ScopedResolveFrozenValues(
                        inp.generatedCode, 
                        inp.method
                            .ScopedFrozenParameters()
                            .Select(p => (p.Type.ToDisplayString(), p.Name)),
                        inp.method.Parameters.First().Name)
                
            );
        
        
        static (IMethodSymbol method, string generatedCode) ScopedProvideValues
            (this (IMethodSymbol method, string generatedCode) inp) => 
            (inp.method,
                
                inp.method.ReturnsVoid 
                    ? inp.generatedCode 
                    : ScopedProvideValue(inp.generatedCode)
                
            );
        
        
        
        static (IMethodSymbol method, string generatedCode) ScopedFreezeScope
            (this (IMethodSymbol method, string generatedCode) inp) => 
            (inp.method,
                
                inp.method.HasFrozenScopedAttribute() 
                    ? ScopedFreezeValue(inp.generatedCode, inp.method.ReturnsVoid) 
                    : inp.generatedCode
                
            );
        
        static (IMethodSymbol method, string generatedCode) ScopedCreateScopedMethod
            (this (IMethodSymbol method, string generatedCode) inp) => 
            (inp.method,
                ScopedMethodTemplate(
                        inp.generatedCode, 
                        inp.method.ReturnsVoid, 
                        inp.method.ScopedMethodReturnType(),
                        inp.method.ScopedGeneratedMethodName(), 
                        inp.method
                            .ScopedMethodInputFilteredArgs()
                            .ScopedMethodInlinedInputArgs())
                
            );
        
        
        static (IMethodSymbol method, string generatedCode) ScopedCreateScopedClass
            (this (IMethodSymbol method, string generatedCode) inp) => 
            (inp.method,
                
                ScopedClassTemplate(inp.generatedCode, inp.method.ScopedClassName())
                
            );

        
        static (IMethodSymbol method, string generatedCode) ScopedCreateScopedNamespace
            (this (IMethodSymbol method, string generatedCode) inp) => 
            (inp.method,
                
                ScopedNamespaceTemplate(inp.generatedCode, inp.method.ScopedNamespaceName())
                
            );

        static (IMethodSymbol method, string generatedCode) StartCodeGeneration(this IMethodSymbol method) => (method, "");
        
        static string EndCodeGeneration(this (IMethodSymbol method, string generatedCode) _) => _.generatedCode;


        static string ScopedMethod(this IMethodSymbol method) =>
            method
                .StartCodeGeneration()
                .ScopedCallChild()
                .ScopedCreatingScope()
                .ScopedResolveValues()
                .ScopedResolveFrozenValues()
                .ScopedProvideValues()
                .ScopedFreezeScope()
                .ScopedCreateScopedMethod()
                .ScopedCreateScopedClass()
                .ScopedCreateScopedNamespace()
                .EndCodeGeneration();
        
        static bool HasScopedAttribute(this IParameterSymbol symbol)
        {
            return symbol.ScopedAttributeOrNull() is not null;
        }
        
        static bool HasScopedAttribute(this IMethodSymbol symbol)
        {
            return symbol.ScopedAttributeOrNull() is not null;
        }
        
        static bool HasFrozenScopedAttribute(this IMethodSymbol symbol)
        {
            return symbol.ScopedAttributeOrNull()
                       ?.AttributeClass
                       ?.ToDisplayString()
                       .Contains("FrozenScope") 
                   ?? false;
        }
        
        
        static AttributeData? ScopedAttributeOrNull(this IMethodSymbol symbol)
        {
            return symbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString().Contains("Scope") ?? false);
        }
        
        static AttributeData? ScopedAttributeOrNull(this IParameterSymbol symbol)
        {
            return symbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString().Contains("Scope") ?? false);
        }
        
        
        static string ScopedMethodReturnType(this IMethodSymbol method) => 
            method.ReturnType.ToDisplayString();
        
        // ReSharper disable once MemberCanBePrivate.Global
        public static string ScopedClassName(this IMethodSymbol method) => 
            method.ContainingType.ToDisplayString().Replace($"{method.ScopedNamespaceName()}.", "");
        
        // ReSharper disable once MemberCanBePrivate.Global
        public static string ScopedNamespaceName(this IMethodSymbol method) => method
            .ContainingType
            .ContainingNamespace
            .ToDisplayString();


        static string ScopedGetFileName(this IMethodSymbol method)
        {
            return $"{method.ScopedNamespaceName()}.{method.ScopedClassName()}.{method.ScopedCallMethodName()}.g.cs";;
        }

       
        
        static string ScopedGetScopeName(this IMethodSymbol method)
        {
            return method.ScopedAttributeOrNull()?.AttributeClass?
                .ToDisplayString()
                .Replace("Attribute", "")
                .Replace("ScopyRuntime.", "") ?? "";
        }
        
        static string ScopedCallMethodName(this IMethodSymbol method)
        {
            var callMethodName = 
                $"{method.Name}";
            return callMethodName;
        }
       
        static string ScopedMethodInlinedCallArgs(this IMethodSymbol method) 
        => method.Parameters
            .Aggregate("",
                (accum, arg) => accum + $"{arg.Name},")
            .TrimEnd(',');

        static IEnumerable<IParameterSymbol> ScopedValuedParameters(this IMethodSymbol method)
        {
            return method.Parameters
                .Where(p => p.GetAttributes()
                    .Any(a => 
                        a.AttributeClass?.ToDisplayString() == "ScopyRuntime.ScopeAttribute"));
        }

        static IEnumerable<IParameterSymbol> ScopedFrozenParameters(this IMethodSymbol method)
        {
            return method.Parameters
                .Where(p => p.GetAttributes()
                    .Any(a => 
                        a.AttributeClass?.ToDisplayString() == "ScopyRuntime.FrozenScopeAttribute"));
        }
        
        
        
      
        static string ScopedGeneratedMethodName(this IMethodSymbol method) => 
            method.Name + "Scoped";



        static IEnumerable<IParameterSymbol> ScopedMethodInputFilteredArgs(this IMethodSymbol method)
            => method
                .Parameters
                .Where(p => !p.HasScopedAttribute()); 
        
        static string ScopedMethodInlinedInputArgs(this IEnumerable<IParameterSymbol> parameterSymbols)
            => parameterSymbols
                .Aggregate("", (accum, arg) => 
                    accum + $"{arg.ToDisplayString()}," )
                .TrimEnd(',');
        
        
        const string MethodCallResultVarName = "methodCallResult";
        
        
        static string ScopedCreateScopeTemplate(
            string scopeCode,
            string scopeName)
        {
            var source = 
                $$"""
                  CurrentScope.Push("{{scopeName}}");
                  {{scopeCode}}
                  CurrentScope.Pop();
                  """;
            return source;
        }

        static string ScopedProvideValue(string scopeCode)
        {
            var source = 
                $$"""
                  {{scopeCode}}
                  CurrentScope.Provide({{MethodCallResultVarName}});
                  """;
            return source;
        }
        
        static string ScopedFreezeValue(string scopeCode, bool isVoid)
        {
            var freezeVal = isVoid ?  "" : MethodCallResultVarName;
            
            var source = 
                $$"""
                  {{scopeCode}}
                  CurrentScope.Freeze({{freezeVal}});
                  """;
            return source;
        }
        static string ScopedResolveValues(string scopeCode, IEnumerable<(string type, string name)> values)
        {

            var valuesSolving = values
                .Select(v => $"var {v.name} = CurrentScope.Resolve<{v.type}>();")
                .Aggregate("", (accum, v) => accum + v + "\n");
            
            var source = 
                $$"""
                  {{valuesSolving}}
                  {{scopeCode}}
                  """;
            return source;
        }


        static string ScopedResolveFrozenValues(string scopedCode, IEnumerable<(string type, string name)> values, string objRef)
        {
            var valuesSolving = values
                .Select(v => $"var {v.name} = CurrentScope.ResolveFrozen<{v.type}>({objRef});")
                .Aggregate("", (accum, v) => accum + v + "\n");
            
            var source = 
                $$"""
                  {{valuesSolving}}
                  {{scopedCode}}
                  """;
            return source;
        }
        
        
        
        
       

        static string ScopedCallChildMethodTemplate(
            bool isVoid, 
            string callMethodName,
            string methodInlinedCallArgs
            )
        {
            var callMethodResultVar = isVoid ? "" : $"var {MethodCallResultVarName} = ";
            var source = 
                $$"""
                  {{callMethodResultVar}}{{callMethodName}}({{methodInlinedCallArgs}});
                  """;
            return source;
        }
        
        
        
        
        static string ScopedNamespaceTemplate(string nameSpaceCode, string nameSpaceName)
        {
            
            var source = $$"""
                            namespace {{nameSpaceName}}
                            {
                                using ScopyRuntime;
                                {{nameSpaceCode}}
                            }
                           """;
            return source;

        }



        static string ScopedClassTemplate(string classCode, string className)
        {
            var source = 
                $$"""
                  public static partial class {{className}}
                      {
                          {{classCode}}
                      }

                  """;
            return source;
        }
        
        
        
        
        
        
    

        static string ScopedMethodTemplate(
            string methodCode, 
            bool isVoid,
            string methodReturnType, 
            string generatedMethodName,
            string methodInlinedInputArgs)
        {
            
            var returnVal = isVoid ? "" : MethodCallResultVarName;
            methodReturnType = methodReturnType.Replace("Void", "void");
            var source = 
                $$"""
                   public static {{methodReturnType}} {{generatedMethodName}}
                      ({{methodInlinedInputArgs}})
                  {
                      {{methodCode}}
                      return {{returnVal}};
                  }

                  """;
            return source;
        }
        
        

    }
    
    


    
    
    


    
    
    
    
    
    public static class ScopedMethodInvokeAnalyzer
    {
        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        public class AnalyzerGenerator : DiagnosticAnalyzer
        {
            public override void Initialize(AnalysisContext context)
            {
            
                context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
                context.EnableConcurrentExecution();
                context.StartAnalysis();
            }

            public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ScopedMethodInvokeAnalyzer.SupportedDiagnostics;
        }

        public static void StartAnalysis(this AnalysisContext context)
        {
            
            
            context.RegisterCompilationStartAction(compilationContext =>
            {
                // Общий кеш для всей компиляции
                var methodSummaries = new ConcurrentDictionary<IMethodSymbol, ImmutableArray<(IInvocationOperation, IOperation)>>(SymbolEqualityComparer.Default);

                compilationContext.RegisterOperationBlockAction(ctx =>
                {
                    if (ctx.OwningSymbol is not IMethodSymbol currentMethod) return;
                    
                    

                    // Стек для текущего метода (здесь мы БУДЕМ кидать ошибки)
                    var provideStack = new Stack<(IInvocationOperation, IOperation)>();

                    foreach (var block in ctx.OperationBlocks)
                    {
                        foreach (var invocation in block.Descendants().OfType<IInvocationOperation>())
                        {
                            if (!invocation.IsScopyRuntimeClass())
                            {
                                // 1. Пытаемся получить сводку (рекурсивно, если нужно)
                                var nestedProvides 
                                    = GetOrAnalyzeSummary(invocation, ctx.Compilation, methodSummaries, new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));

                                // 2. Применяем эффекты вложенного метода к нашему текущему стеку
                                foreach (var (effectInvocation, currentInvocation) in nestedProvides)
                                {
                                    CheckAndPush(ctx, effectInvocation, provideStack,  invocation);
                                }
                            }
                            else
                            {
                                // 3. Если это прямой вызов Provide/Push/Dispose
                                CheckAndPush(ctx, invocation, provideStack, block);
                            }
                        }
                    }

                    // В конце сохраняем результат текущего метода в кеш для других
                    methodSummaries.TryAdd(currentMethod, [..provideStack]);
                });
            });
            static void CheckAndPush(
                OperationBlockAnalysisContext ctx, 
                IInvocationOperation effectInvocation, 
                Stack<(IInvocationOperation, IOperation)> stack, 
                IOperation currentInvocation)
            {


                if (currentInvocation.IsHiddenInLocalFlow() || effectInvocation.IsHiddenInLocalFlow())
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(DebugProvider, currentInvocation.Syntax.GetLocation(), 
                        $"IT'S FUCKING WORK"));
                }
                
                
                
                //
                // Если это Provide — проверяем, нет ли его уже в стеке
                if (effectInvocation.IsProvideValue() && effectInvocation.IsValueProvided(stack))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(ValueAlreadyProvidedRule, currentInvocation.Syntax.GetLocation()));
                }
    
                // Логика PushScopeData
                if (effectInvocation.IsPushScopeMethod() || effectInvocation.IsResolveValue() || effectInvocation.IsProvideValue())
                {
                    stack.Push((effectInvocation, currentInvocation));
                }
    
                // Тут же можно вызвать твой ClearDisposedScope(effect, stack), если прилетел Dispose
            }
            
            
            static void PushScopeData(
                Stack<(IInvocationOperation, IOperation)> provide, 
                IInvocationOperation operation,
                IOperation currentInvocation)
            {
                if (operation.IsPushScopeMethod() 
                    || operation.IsResolveValue() 
                    || operation.IsProvideValue())
                    provide.Push((operation, currentInvocation));
            }
            
            static ImmutableArray<(IInvocationOperation, IOperation)> GetOrAnalyzeSummary(
                IInvocationOperation methodInvocation, 
                Compilation compilation,
                ConcurrentDictionary<IMethodSymbol, ImmutableArray<(IInvocationOperation, IOperation)>> methodSummaries,
                HashSet<IMethodSymbol> visited) // Защита от циклов
            {
                if (methodSummaries.TryGetValue(methodInvocation.TargetMethod, out var summary)) 
                    return summary;

                // Чтобы не зациклиться
                if (!visited.Add(methodInvocation.TargetMethod)) return ImmutableArray<(IInvocationOperation, IOperation)>.Empty;

                var syntaxReference = methodInvocation.TargetMethod.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxReference == null) return ImmutableArray<(IInvocationOperation, IOperation)>.Empty;

                var model = compilation.GetSemanticModel(syntaxReference.SyntaxTree);
                // Получаем корень метода (блок или выражение)
                var syntax = syntaxReference.GetSyntax();
                var operation = model.GetOperation(syntax);

                if (operation is null) return ImmutableArray<(IInvocationOperation, IOperation)>.Empty;
    
                // Передаем visited дальше
                var result = AnalyzeMethodRecursive(operation, compilation, methodSummaries, visited); 

                methodSummaries.TryAdd(methodInvocation.TargetMethod, result);
                visited.Remove(methodInvocation.TargetMethod); // Очищаем после выхода из ветки
                return result;
            }

            static ImmutableArray<(IInvocationOperation, IOperation)> AnalyzeMethodRecursive(
                IOperation block,
                Compilation compilation,
                ConcurrentDictionary<IMethodSymbol, ImmutableArray<(IInvocationOperation, IOperation)>> methodSummaries,
                HashSet<IMethodSymbol> visited
                )
            {
                var localProvide = new Stack<(IInvocationOperation, IOperation)>();

                foreach (var currentInvocation in block.Descendants().OfType<IInvocationOperation>())
                {
                    var target = currentInvocation.TargetMethod;

                    if (!target.IsScopyRuntimeClass()) 
                    {
                        // РЕКУРСИВНО получаем сводку метода
                        var nestedProvides 
                            = GetOrAnalyzeSummary(currentInvocation, compilation, methodSummaries, visited);
            
                        foreach (var (typeInvocation, subCurrentInvocation) in nestedProvides)
                        {
                            
                            // Тут проверка на дубликаты (ValueAlreadyProvidedRule)
                            PushScopeData(localProvide, typeInvocation, currentInvocation);
                        }
                    }
                    else 
                    {
                        // Если это системный вызов (CurrentScope.Provide / Push)
                        PushScopeData(localProvide, currentInvocation, block);
                    }
                }
                return [..localProvide];
            }
        }


        // enum ScopeAction
        // {
        //     Push,
        //     Pop,
        //     Resolve,
        //     Provide
        // }

        // abstract record ScopeAction
        // {
        //     public record PushScopeAction(string Name) : ScopeAction;
        // }
        //


        // ReSharper disable once InconsistentNaming
        abstract record ScopeAction(bool IsHiddenFlow, ScopeAction.CallMethodWithScopeActions? PreviousCall = null)
        {
            public record Push(string ScopeName, bool IsHiddenFlow, CallMethodWithScopeActions PreviousCall) : ScopeAction(IsHiddenFlow, PreviousCall);

            public record Pop(bool IsHiddenFlow, CallMethodWithScopeActions PreviousCall, Push CurrentPush) : ScopeAction(IsHiddenFlow, PreviousCall);

            public record Resolve(string ValueName, bool IsHiddenFlow, CallMethodWithScopeActions PreviousCall, Push CurrentPush) : ScopeAction(IsHiddenFlow, PreviousCall);
        
            public record Provide(string ValueName, bool IsHiddenFlow, CallMethodWithScopeActions PreviousCall, Push CurrentPush ) : ScopeAction(IsHiddenFlow, PreviousCall);
            
            
            public static CallMethodWithScopeActions ProgramStart => new([], false);

            public static Push RootPush => new("Scope", false, ProgramStart);
            
            public record CallMethodWithScopeActions(IImmutableStack<ScopeAction> ScopeActions, bool IsHiddenFlow, CallMethodWithScopeActions? PreviousCall = null) : ScopeAction(IsHiddenFlow, PreviousCall);
        }

        static void Test()
        {
        }
        
        
        
        record InvocationMethodMetadata(ScopeAction Action);
        

        public static readonly DiagnosticDescriptor ValueAlreadyProvidedRule =
            new(
                "SCOPY_ALREADY_PROVIDED_VALUE",
                "Scope error",
                "Value in this scope already provided",
                "Scopy",
                DiagnosticSeverity.Error, 
                true);
        
        public static readonly DiagnosticDescriptor DebugProvider =
            new DiagnosticDescriptor(
                "SCOPY_DEBUG",
                "debug",
                "{0}",
                "Scopy",
                DiagnosticSeverity.Warning,
                true);


        public static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics = [ValueAlreadyProvidedRule, DebugProvider];


        static bool IsValueProvided(this IInvocationOperation method, Stack<(IInvocationOperation, IOperation)> provide)
        {
            var isProvided = provide.TakeWhile(p => !p.Item1.IsPushScopeMethod()).Any(ms => 
                ms.Item1.IsProvideValue() 
                && SymbolEqualityComparer.Default.Equals(ms.Item1.TargetMethod.GetProvideType(), method.TargetMethod.GetProvideType())
            );
            return isProvided;
        }

        static ITypeSymbol GetProvideType(this IMethodSymbol method)
        {
            return method.Parameters.First().Type;
        }
        
        static bool IsHiddenInLocalFlow(this IOperation op)
        {
            var current = op.Parent;
            while (current != null)
            {
                if (current is IConditionalOperation or ISwitchOperation or ILoopOperation or ITryOperation)
                    return true;
                current = current.Parent;
            }
            return false;
        }
        

        static bool IsName(this ISymbol symbol, string name) => symbol.Name == name;
        static bool IsScopyRuntimeNameSpace(this ISymbol symbol) => symbol.ContainingNamespace.IsName("ScopyRuntime");
        static bool IsScopyRuntimeClass(this ISymbol symbol) => symbol.ContainingType.IsName("CurrentScope");
        
        
        static bool IsName(this IInvocationOperation symbol, string name) => symbol.TargetMethod.Name == name;
        static bool IsScopyRuntimeNameSpace(this IInvocationOperation symbol) => symbol.TargetMethod.ContainingNamespace.IsName("ScopyRuntime");
        static bool IsScopyRuntimeClass(this IInvocationOperation symbol) => symbol.TargetMethod.ContainingType.IsName("CurrentScope");
        
        
        
        static bool IsPushScopeMethod(this IMethodSymbol symbol)
        {
            return symbol.IsScopyRuntimeNameSpace() && symbol.IsScopyRuntimeClass() && symbol.IsName("Push");
        }

        
        
        
        
        
        
        static bool IsDisposeScopeMethod(this IMethodSymbol symbol)
        {
            return symbol.IsScopyRuntimeNameSpace() && symbol.IsScopyRuntimeClass() && symbol.IsName("Pop");
        }
        
        static bool IsProvideValue(this IMethodSymbol symbol)
        {
            return symbol.IsScopyRuntimeNameSpace() && symbol.IsScopyRuntimeClass() && symbol.IsName("Provide");
        }
        
        static bool IsResolveValue(this IMethodSymbol symbol)
        {
            return symbol.IsScopyRuntimeNameSpace() && symbol.IsScopyRuntimeClass() && symbol.IsName("Resolve");
        }
        
        
        
        
        
        static bool IsPushScopeMethod(this IInvocationOperation operation)
        {
            var symbol = operation.TargetMethod;
            return symbol.IsScopyRuntimeNameSpace() && symbol.IsScopyRuntimeClass() && symbol.IsName("Push");
        }
        
        
        static bool IsDisposeScopeMethod(this IInvocationOperation operation)
            
        {
            var symbol = operation.TargetMethod;
            return symbol.IsScopyRuntimeNameSpace() && symbol.IsScopyRuntimeClass() && symbol.IsName("DisposeCurrent");
        }
        
        static bool IsProvideValue(this IInvocationOperation operation)
        {
            var symbol = operation.TargetMethod;
            return symbol.IsScopyRuntimeNameSpace() && symbol.IsScopyRuntimeClass() && symbol.IsName("Provide");
        }
        
        static bool IsResolveValue(this IInvocationOperation operation)
        {
            var symbol = operation.TargetMethod;
            return symbol.IsScopyRuntimeNameSpace() && symbol.IsScopyRuntimeClass() && symbol.IsName("Resolve");
        }

        
        
    }
    
    
    


    

}


namespace System.Runtime.CompilerServices
{
    // Этот класс нужен компилятору, чтобы разрешить работу 'init' свойств в старых версиях .NET
    internal static class IsExternalInit {}
}