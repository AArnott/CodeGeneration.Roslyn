using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace JsonGenerator
{
    public class JsonGenerator : ICodeGenerator
    {
        public JsonGenerator(AttributeData attributeData)
        {
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            var partialType = CreatePartialType();
            return Task.FromResult(SyntaxFactory.List(partialType));

            IEnumerable<MemberDeclarationSyntax> CreatePartialType()
            {
                var newPartialType =
                    context.ProcessingNode is ClassDeclarationSyntax classDeclaration
                        ? SyntaxFactory.ClassDeclaration(classDeclaration.Identifier.ValueText)
                        : context.ProcessingNode is StructDeclarationSyntax structDeclaration
                            ? SyntaxFactory.StructDeclaration(structDeclaration.Identifier.ValueText)
                            : default(TypeDeclarationSyntax);
                if (newPartialType is null)
                    yield break;
                yield return newPartialType
                    ?.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                    .AddMembers(CreateIdProperty());
            }
            MemberDeclarationSyntax CreateIdProperty()
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(
                    new { GeneratorAssembly = typeof(JsonGenerator).Assembly.FullName }
                );
                return
                    PropertyDeclaration(
                        PredefinedType(
                            Token(SyntaxKind.StringKeyword)),
                        Identifier("Json"))
                    .WithModifiers(
                        TokenList(
                            Token(SyntaxKind.StaticKeyword)))
                    .WithExpressionBody(
                        ArrowExpressionClause(
                            LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                Literal(json))))
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken));
            }
        }
    }
}
