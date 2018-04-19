using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeGeneration.Roslyn
{
    public class TransformationContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransformationContext" /> class.
        /// </summary>
        /// <param name="processingMember">The syntax node the generator attribute is found on.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="compilation">The overall compilation being generated for.</param>
        /// <param name="projectDirectory">The absolute path of the directory where the project file is located</param>
        public TransformationContext(CSharpSyntaxNode processingMember, SemanticModel semanticModel, CSharpCompilation compilation,
                                     string projectDirectory)
        {
            ProcessingMember = processingMember;
            SemanticModel = semanticModel;
            Compilation = compilation;
            ProjectDirectory = projectDirectory;
        }

        /// <summary>Gets the syntax node the generator attribute is found on.</summary>
        public CSharpSyntaxNode ProcessingMember { get; }

        /// <summary>Gets the semantic model for the <see cref="Compilation" />.</summary>
        public SemanticModel SemanticModel { get; }

        /// <summary>Gets the overall compilation being generated for.</summary>
        public CSharpCompilation Compilation { get; }

        /// <summary>
        /// Gets the absolute path of the directory where the project file is located
        /// </summary>
        public string ProjectDirectory { get; }
    }
}
