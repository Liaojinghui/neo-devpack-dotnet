using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Neo.SmartContract.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class Nep11ContractAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NC4025";
        private static readonly LocalizableString Title = "NEP-11 contract format and compliance";
        private static readonly LocalizableString MessageFormat = "{0}";
        private static readonly LocalizableString Description = "Verifies the correct format and compliance for NEP-11 token contracts.";
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;

            bool inheritsFromNep11Token = classDeclaration.BaseList?.Types
                .Any(t => t.Type.ToString() == "Neo.SmartContract.Framework.Nep11Token" ||
                          t.Type.ToString() == "Nep11Token") ?? false;

            bool hasNep11SupportedStandardsAttribute = HasSupportedStandardsAttribute(classDeclaration, "NepStandard.Nep11");

            if (inheritsFromNep11Token || hasNep11SupportedStandardsAttribute)
            {
                CheckNep11Compliance(context, classDeclaration);
            }
            else
            {
                ReportDiagnostic(context, classDeclaration, "Class should inherit from Nep11Token or have [SupportedStandards(NepStandard.Nep11)] attribute");
            }
        }

        private bool HasSupportedStandardsAttribute(ClassDeclarationSyntax classDeclaration, string standard)
        {
            return classDeclaration.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString() == "SupportedStandards" &&
                          a.ArgumentList?.Arguments.Any(arg => arg.ToString().Contains(standard)) == true);
        }

        private void CheckNep11Compliance(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
        {
            CheckMethod(context, classDeclaration, "Symbol", "string", true);
            CheckMethod(context, classDeclaration, "Decimals", "byte", true);
            CheckMethod(context, classDeclaration, "TotalSupply", "BigInteger", true);

            CheckOverloadedMethod(context, classDeclaration, "BalanceOf",
                [["UInt160"], ["UInt160", "ByteString"]], "BigInteger", true);

            CheckMethod(context, classDeclaration, "TokensOf", new[] { "UInt160" }, "InteropInterface", true);

            CheckOverloadedMethod(context, classDeclaration, "OwnerOf",
                [["ByteString"]], ["UInt160", "InteropInterface"], true);

            CheckOverloadedMethod(context, classDeclaration, "Transfer",
            [
                ["UInt160", "ByteString", "object"],
                ["UInt160", "UInt160", "BigInteger", "ByteString", "object"]
            ], "bool", false);

            CheckEvent(context, classDeclaration, "Transfer", "UInt160", "UInt160", "BigInteger", "ByteString");

            CheckNep11PayableCompliance(context, classDeclaration);
        }

        private void CheckNep11PayableCompliance(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
        {
            CheckMethod(context, classDeclaration, "OnNEP11Payment",
                ["UInt160", "BigInteger", "ByteString", "object"], "void", true);
        }

        private void CheckMethod(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration,
            string methodName, string returnType, bool shouldBeSafe)
        {
            CheckMethod(context, classDeclaration, methodName, new string[0], returnType, shouldBeSafe);
        }

        private void CheckMethod(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration,
            string methodName, string[] parameters, string returnType, bool shouldBeSafe)
        {
            var method = FindMethod(classDeclaration, methodName, parameters.Length);
            if (method == null)
            {
                ReportDiagnostic(context, classDeclaration, $"Missing method: {methodName}");
                return;
            }

            if (method.ReturnType.ToString() != returnType)
            {
                ReportDiagnostic(context, classDeclaration, $"Incorrect return type for {methodName}. Expected: {returnType}, Found: {method.ReturnType}");
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                if (method.ParameterList.Parameters[i].Type.ToString() != parameters[i])
                {
                    ReportDiagnostic(context, classDeclaration, $"Incorrect parameter type for {methodName}. Parameter {i + 1} expected: {parameters[i]}, Found: {method.ParameterList.Parameters[i].Type}");
                }
            }

            CheckSafeAttribute(context, classDeclaration, method, methodName, shouldBeSafe);
        }

        private void CheckOverloadedMethod(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration,
            string methodName, string[][] parameterSets, string returnType, bool shouldBeSafe)
        {
            CheckOverloadedMethod(context, classDeclaration, methodName, parameterSets, new[] { returnType }, shouldBeSafe);
        }

        private void CheckOverloadedMethod(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration,
            string methodName, string[][] parameterSets, string[] returnTypes, bool shouldBeSafe)
        {
            bool foundValidOverload = false;

            foreach (var parameters in parameterSets)
            {
                var method = FindMethod(classDeclaration, methodName, parameters.Length);
                if (method != null)
                {
                    bool isValidOverload = true;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (method.ParameterList.Parameters[i].Type.ToString() != parameters[i])
                        {
                            isValidOverload = false;
                            break;
                        }
                    }

                    if (isValidOverload && returnTypes.Contains(method.ReturnType.ToString()))
                    {
                        foundValidOverload = true;
                        CheckSafeAttribute(context, classDeclaration, method, methodName, shouldBeSafe);
                        break;
                    }
                }
            }

            if (!foundValidOverload)
            {
                ReportDiagnostic(context, classDeclaration, $"Missing valid overload for method: {methodName}");
            }
        }

        private void CheckEvent(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration,
            string eventName, params string[] parameterTypes)
        {
            var eventField = classDeclaration.Members
                .OfType<EventFieldDeclarationSyntax>()
                .FirstOrDefault(e => e.Declaration.Variables.Any(v => v.Identifier.Text == eventName));

            if (eventField == null)
            {
                ReportDiagnostic(context, classDeclaration, $"Missing event: {eventName}");
                return;
            }

            if (eventField.Declaration.Type is not FunctionPointerTypeSyntax fpType)
            {
                ReportDiagnostic(context, classDeclaration, $"Incorrect event type for {eventName}");
                return;
            }

            if (fpType.ParameterList.Parameters.Count != parameterTypes.Length)
            {
                ReportDiagnostic(context, classDeclaration, $"Incorrect number of parameters for event {eventName}");
                return;
            }

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                if (fpType.ParameterList.Parameters[i].Type.ToString() != parameterTypes[i])
                {
                    ReportDiagnostic(context, classDeclaration, $"Incorrect parameter type for event {eventName}. Parameter {i + 1} expected: {parameterTypes[i]}, Found: {fpType.ParameterList.Parameters[i].Type}");
                }
            }
        }

        private void CheckSafeAttribute(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration,
            MethodDeclarationSyntax method, string methodName, bool shouldBeSafe)
        {
            bool hasSafeAttribute = method.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString() == "Safe");

            if (shouldBeSafe && !hasSafeAttribute)
            {
                ReportDiagnostic(context, classDeclaration, $"Method {methodName} should have [Safe] attribute");
            }
            else if (!shouldBeSafe && hasSafeAttribute)
            {
                ReportDiagnostic(context, classDeclaration, $"Method {methodName} should not have [Safe] attribute");
            }
        }

        private static MethodDeclarationSyntax FindMethod(ClassDeclarationSyntax classDeclaration, string methodName, int parameterCount)
        {
            return classDeclaration.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == methodName && m.ParameterList.Parameters.Count == parameterCount)!;
        }

        private void ReportDiagnostic(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration, string message)
        {
            var diagnostic = Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation(), message);
            context.ReportDiagnostic(diagnostic);
        }
    }

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Nep11ContractCodeFixProvider)), Shared]
    public class Nep11ContractCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Nep11ContractAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Implement NEP-11 interface",
                    createChangedDocument: c => ImplementNep11InterfaceAsync(context.Document, declaration, c),
                    equivalenceKey: nameof(Nep11ContractCodeFixProvider)),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add [SupportedStandards] attribute",
                    createChangedDocument: c => AddSupportedStandardsAttributeAsync(context.Document, declaration, c),
                    equivalenceKey: nameof(Nep11ContractCodeFixProvider)),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add missing method",
                    createChangedDocument: c => AddMissingMethodAsync(context.Document, declaration, diagnostic.GetMessage(), c),
                    equivalenceKey: nameof(Nep11ContractCodeFixProvider)),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add [Safe] attribute",
                    createChangedDocument: c => AddSafeAttributeAsync(context.Document, declaration, diagnostic.GetMessage(), c),
                    equivalenceKey: nameof(Nep11ContractCodeFixProvider)),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove [Safe] attribute",
                    createChangedDocument: c => RemoveSafeAttributeAsync(context.Document, declaration, diagnostic.GetMessage(), c),
                    equivalenceKey: nameof(Nep11ContractCodeFixProvider)),
                diagnostic);
        }

        private static async Task<Document> ImplementNep11InterfaceAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            var newClassDecl = classDecl.AddBaseListTypes(
                SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("Neo.SmartContract.Framework.Nep11Token")));

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(classDecl, newClassDecl);
            return document.WithSyntaxRoot(newRoot);
        }

        private static async Task<Document> AddSupportedStandardsAttributeAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            var attribute = SyntaxFactory.Attribute(
                SyntaxFactory.ParseName("SupportedStandards"),
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.ParseExpression("NepStandard.Nep11")
                        )
                    })
                )
            );

            var newClassDecl = classDecl.AddAttributeLists(
                SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute)));

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(classDecl, newClassDecl);
            return document.WithSyntaxRoot(newRoot);
        }

        private static async Task<Document> AddMissingMethodAsync(Document document, ClassDeclarationSyntax classDecl, string diagnosticMessage, CancellationToken cancellationToken)
        {
            var methodName = diagnosticMessage.Split(':')[1].Trim();
            MethodDeclarationSyntax newMethod = null;

            switch (methodName)
            {
                case "Symbol":
                    newMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("string"), "Symbol")
                        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
                        .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement("return \"NEP11\";")))
                        .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Safe")))));
                    break;
                case "Decimals":
                    newMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("byte"), "Decimals")
                        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
                        .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement("return 0;")))
                        .AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Safe")))));
                    break;
                    // Add cases for other methods as needed
            }

            if (newMethod != null)
            {
                var newClassDecl = classDecl.AddMembers(newMethod);
                var root = await document.GetSyntaxRootAsync(cancellationToken);
                var newRoot = root.ReplaceNode(classDecl, newClassDecl);
                return document.WithSyntaxRoot(newRoot);
            }

            return document;
        }

        private static async Task<Document> AddSafeAttributeAsync(Document document, ClassDeclarationSyntax classDecl, string diagnosticMessage, CancellationToken cancellationToken)
        {
            var methodName = diagnosticMessage.Split(' ')[1];
            var method = classDecl.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.Text == methodName);

            if (method != null)
            {
                var safeAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Safe"));
                var newMethod = method.AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(safeAttribute)));
                var newClassDecl = classDecl.ReplaceNode(method, newMethod);

                var root = await document.GetSyntaxRootAsync(cancellationToken);
                var newRoot = root.ReplaceNode(classDecl, newClassDecl);
                return document.WithSyntaxRoot(newRoot);
            }

            return document;
        }

        private static async Task<Document> RemoveSafeAttributeAsync(Document document, ClassDeclarationSyntax classDecl, string diagnosticMessage, CancellationToken cancellationToken)
        {
            var methodName = diagnosticMessage.Split(' ')[1];
            var method = classDecl.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.Text == methodName);

            if (method != null)
            {
                var newMethod = method.RemoveNodes(
                    method.AttributeLists.Where(al => al.Attributes.Any(a => a.Name.ToString() == "Safe")),
                    SyntaxRemoveOptions.KeepNoTrivia);

                var newClassDecl = classDecl.ReplaceNode(method, newMethod);

                var root = await document.GetSyntaxRootAsync(cancellationToken);
                var newRoot = root.ReplaceNode(classDecl, newClassDecl);
                return document.WithSyntaxRoot(newRoot);
            }

            return document;
        }
    }
}
