# CGR Templates

Our templates provide a nice starting point for creating your CG.R plugins.

To install the template pack, run:
> `dotnet new -i CodeGeneration.Roslyn.Templates`

*To uninstall template pack, run:* `dotnet new -u CodeGeneration.Roslyn.Templates`

Currently these templates are available:
- plugin Generator class (`dotnet new cgrgen`)
- plugin Attribute class (`dotnet new cgratt`)
- plugin Generators project (`dotnet new cgrplugingens`)
- plugin Attributes project (`dotnet new cgrpluginatts`)
- plugin project set (`dotnet new cgrplugin`) - this creates 3 projects:
  - Attributes,
  - Generators,
  - and a metapackage building project.
