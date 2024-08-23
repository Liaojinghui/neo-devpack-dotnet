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
    public class Nep17ContractAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NC4024";
        private static readonly LocalizableString Title = "NEP-17 contract format and compliance";
        private static readonly LocalizableString MessageFormat = "{0}";
        private static readonly LocalizableString Description = "Verifies the correct format and compliance for NEP-17 token contracts.";
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

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;

            bool inheritsFromNep17Token = classDeclaration.BaseList?.Types
                .Any(t => t.Type.ToString() == "Neo.SmartContract.Framework.Nep17Token" ||
                          t.Type.ToString() == "Nep17Token") ?? false;

            bool hasSupportedStandardsAttribute = classDeclaration.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString() == "SupportedStandards" &&
                          a.ArgumentList?.Arguments.Any(arg => arg.ToString().Contains("NepStandard.Nep17")) == true);

            if (!inheritsFromNep17Token && !hasSupportedStandardsAttribute)
            {
                var diagnostic = Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation(),
                    "NEP-17 contract should inherit from Nep17Token or have [SupportedStandards(NepStandard.Nep17)] attribute");
                context.ReportDiagnostic(diagnostic);
            }

            // Check NEP-17 compliance
            CheckNep17Compliance(context, classDeclaration);

            // Check NEP-17 Payable compliance
            CheckNep17PayableCompliance(context, classDeclaration);
        }

        private static void CheckNep17Compliance(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
        {
            var symbolProperty = FindProperty(classDeclaration, "Symbol");
            var decimalsProperty = FindProperty(classDeclaration, "Decimals");
            var totalSupplyMethod = FindMethod(classDeclaration, "TotalSupply", 0);
            var balanceOfMethod = FindMethod(classDeclaration, "BalanceOf", 1);
            var transferMethod = FindMethod(classDeclaration, "Transfer", 4);

            CheckProperty(context, classDeclaration, symbolProperty, "Symbol", "string", true);
            CheckProperty(context, classDeclaration, decimalsProperty, "Decimals", "byte", true);
            CheckMethod(context, classDeclaration, totalSupplyMethod, "TotalSupply", "BigInteger", true);
            CheckMethod(context, classDeclaration, balanceOfMethod, "BalanceOf", "BigInteger", true, "UInt160");
            CheckMethod(context, classDeclaration, transferMethod, "Transfer", "bool", false, "UInt160", "UInt160", "BigInteger", "object");

            var transferEvent = FindEvent(classDeclaration, "Transfer", "UInt160", "UInt160", "BigInteger");
            if (transferEvent == null) ReportDiagnostic(context, classDeclaration, "Incomplete NEP-17 implementation: Transfer event is missing");
        }

        private static void CheckNep17PayableCompliance(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
        {
            var onNEP17PaymentMethod = FindMethod(classDeclaration, "OnNEP17Payment", 3);
            CheckMethod(context, classDeclaration, onNEP17PaymentMethod, "OnNEP17Payment", "void", true, "UInt160", "BigInteger", "object");
        }

        private static PropertyDeclarationSyntax FindProperty(ClassDeclarationSyntax classDeclaration, string propertyName)
        {
            return classDeclaration.Members
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(p => p.Identifier.Text == propertyName)!;
        }

        private static MethodDeclarationSyntax FindMethod(ClassDeclarationSyntax classDeclaration, string methodName, int parameterCount)
        {
            return classDeclaration.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == methodName && m.ParameterList.Parameters.Count == parameterCount)!;
        }

        private static EventFieldDeclarationSyntax FindEvent(ClassDeclarationSyntax classDeclaration, string eventName, params string[] parameterTypes)
        {
            return classDeclaration.Members
                .OfType<EventFieldDeclarationSyntax>()
                .FirstOrDefault(e => e.Declaration.Variables.Any(v => v.Identifier.Text == eventName) &&
                                     e.Declaration.Type is FunctionPointerTypeSyntax fpType &&
                                     fpType.ParameterList.Parameters.Count == parameterTypes.Length &&
                                     fpType.ParameterList.Parameters.Select(p => p.Type.ToString()).SequenceEqual(parameterTypes))!;
        }

        private static void CheckProperty(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration,
            PropertyDeclarationSyntax property, string propertyName, string expectedType, bool shouldBeSafe)
        {
            if (property == null)
            {
                ReportDiagnostic(context, classDeclaration, $"Incomplete NEP-17 implementation: {propertyName} property is missing");
                return;
            }

            if (property.Type.ToString() != expectedType)
            {
                ReportDiagnostic(context, classDeclaration, $"Incorrect type for {propertyName} property. Expected: {expectedType}, Found: {property.Type}");
            }

            var getter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
            if (getter == null)
            {
                ReportDiagnostic(context, classDeclaration, $"{propertyName} property must have a getter");
                return;
            }

            bool hasSafeAttribute = HasSafeAttribute(getter);
            if (shouldBeSafe && !hasSafeAttribute)
            {
                ReportDiagnostic(context, classDeclaration, $"{propertyName} property getter must have [Safe] attribute");
            }
            else if (!shouldBeSafe && hasSafeAttribute)
            {
                ReportDiagnostic(context, classDeclaration, $"{propertyName} property getter should not have [Safe] attribute");
            }
        }

        private static void CheckMethod(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration,
            MethodDeclarationSyntax method, string methodName, string expectedReturnType, bool shouldBeSafe, params string[] expectedParameterTypes)
        {
            if (method == null)
            {
                ReportDiagnostic(context, classDeclaration, $"Incomplete NEP-17 implementation: {methodName} method is missing");
                return;
            }

            if (method.ReturnType.ToString() != expectedReturnType)
            {
                ReportDiagnostic(context, classDeclaration, $"Incorrect return type for {methodName} method. Expected: {expectedReturnType}, Found: {method.ReturnType}");
            }

            if (method.ParameterList.Parameters.Count != expectedParameterTypes.Length)
            {
                ReportDiagnostic(context, classDeclaration, $"Incorrect number of parameters for {methodName} method. Expected: {expectedParameterTypes.Length}, Found: {method.ParameterList.Parameters.Count}");
            }
            else
            {
                for (int i = 0; i < expectedParameterTypes.Length; i++)
                {
                    if (method.ParameterList.Parameters[i].Type.ToString() != expectedParameterTypes[i])
                    {
                        ReportDiagnostic(context, classDeclaration, $"Incorrect type for parameter {i + 1} in {methodName} method. Expected: {expectedParameterTypes[i]}, Found: {method.ParameterList.Parameters[i].Type}");
                    }
                }
            }

            bool hasSafeAttribute = HasSafeAttribute(method);
            if (shouldBeSafe && !hasSafeAttribute)
            {
                ReportDiagnostic(context, classDeclaration, $"{methodName} method must have [Safe] attribute");
            }
            else if (!shouldBeSafe && hasSafeAttribute)
            {
                ReportDiagnostic(context, classDeclaration, $"{methodName} method should not have [Safe] attribute. [Safe] forbids writing to storage and emitting events.");
            }
        }

        private static bool HasSafeAttribute(SyntaxNode node)
        {
            return node switch
            {
                MemberDeclarationSyntax member => member.AttributeLists.SelectMany(al => al.Attributes).Any(a => a.Name.ToString() == "Safe"),
                AccessorDeclarationSyntax accessor => accessor.AttributeLists.SelectMany(al => al.Attributes).Any(a => a.Name.ToString() == "Safe"),
                _ => false
            };
        }

        private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration, string message)
        {
            var diagnostic = Diagnostic.Create(Rule, classDeclaration.Identifier.GetLocation(), message);
            context.ReportDiagnostic(diagnostic);
        }
    }

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Nep17ContractCodeFixProvider)), Shared]
    public class Nep17ContractCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Nep17ContractAnalyzer.DiagnosticId);

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
                    title: "Implement NEP-17 interface",
                    createChangedDocument: c => ImplementNep17InterfaceAsync(context.Document, declaration, c),
                    equivalenceKey: nameof(Nep17ContractCodeFixProvider)),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add [SupportedStandards] attribute",
                    createChangedDocument: c => AddSupportedStandardsAttributeAsync(context.Document, declaration, c),
                    equivalenceKey: nameof(Nep17ContractCodeFixProvider)),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add [Safe] attribute to property",
                    createChangedDocument: c => AddSafeAttributeToPropertyAsync(context.Document, declaration, c),
                    equivalenceKey: nameof(Nep17ContractCodeFixProvider)),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove [Safe] attribute from Transfer method",
                    createChangedDocument: c => RemoveSafeAttributeFromTransferMethodAsync(context.Document, declaration, c),
                    equivalenceKey: nameof(Nep17ContractCodeFixProvider)),
                diagnostic);
        }

        private static async Task<Document> ImplementNep17InterfaceAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            var newClassDecl = classDecl.AddBaseListTypes(
                SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("Neo.SmartContract.Framework.Nep17Token")));

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
                            SyntaxFactory.ParseExpression("NepStandard.Nep17")
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

        private static async Task<Document> AddSafeAttributeToPropertyAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            var properties = classDecl.Members.OfType<PropertyDeclarationSyntax>();
            var updatedProperties = properties.Select(p =>
            {
                if (p.Identifier.Text is "Symbol" or "Decimals")
                {
                    var getter = p.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
                    if (getter != null && !HasSafeAttribute(getter))
                    {
                        var safeAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Safe"));
                        var newGetter = getter.AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(safeAttribute)));
                        return p.ReplaceNode(getter, newGetter);
                    }
                }
                return p;
            });

            var newClassDecl = classDecl.ReplaceNodes(
                properties,
                (oldNode, newNode) => updatedProperties.FirstOrDefault(p => p.SpanStart == oldNode.SpanStart) ?? newNode
            );

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(classDecl, newClassDecl);
            return document.WithSyntaxRoot(newRoot);
        }

        private static async Task<Document> RemoveSafeAttributeFromTransferMethodAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
        {
            var transferMethod = classDecl.Members.OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == "Transfer" && m.ParameterList.Parameters.Count == 4);

            if (transferMethod == null) return document;
            var newTransferMethod = transferMethod.RemoveNodes(
                transferMethod.AttributeLists.Where(al => al.Attributes.Any(a => a.Name.ToString() == "Safe")),
                SyntaxRemoveOptions.KeepNoTrivia);

            var newClassDecl = classDecl.ReplaceNode(transferMethod, newTransferMethod);

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = root.ReplaceNode(classDecl, newClassDecl);
            return document.WithSyntaxRoot(newRoot);
        }

        private static bool HasSafeAttribute(SyntaxNode node)
        {
            return node switch
            {
                MemberDeclarationSyntax member => member.AttributeLists.SelectMany(al => al.Attributes).Any(a => a.Name.ToString() == "Safe"),
                AccessorDeclarationSyntax accessor => accessor.AttributeLists.SelectMany(al => al.Attributes).Any(a => a.Name.ToString() == "Safe"),
                _ => false
            };
        }
    }
}
