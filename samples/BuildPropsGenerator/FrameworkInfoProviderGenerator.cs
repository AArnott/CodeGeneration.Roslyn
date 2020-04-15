using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BuildPropsGenerator
{
    public class FrameworkInfoProviderGenerator : ICodeGenerator
    {
        public FrameworkInfoProviderGenerator(AttributeData attributeData)
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
                    .AddMembers(CreateTargetFrameworkListProperty(), CreateCurrentTargetFrameworkProperty());
            }
            MemberDeclarationSyntax CreateTargetFrameworkListProperty()
            {
                var collectionType = "System.Collections.Generic.List<string>";
                var frameworks = context.BuildProperties["TargetFrameworks"];
                var quotedFrameworks = frameworks.Split(";").Select(framework => $"\"{framework}\"");
                var commaDelimitedFrameworks = string.Join(',', quotedFrameworks.ToArray());

                return SyntaxFactory.ParseMemberDeclaration($"public {collectionType} TargetFrameworks {{ get; }} = new {collectionType} {{ {commaDelimitedFrameworks} }};");
            }
            MemberDeclarationSyntax CreateCurrentTargetFrameworkProperty()
            {
                var framework = context.BuildProperties["TargetFramework"];
                return SyntaxFactory.ParseMemberDeclaration($"public string CurrentTargetFramework {{ get; }} = \"{framework}\";");
            }
        }
    }
}