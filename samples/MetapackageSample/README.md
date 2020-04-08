# MetapackageSample

This sample demonstrates an approach to providing a nice UX for our generator consumers.

The `MetapackageSample.sln` solution builds and packs `Attributes` and `Generators` packages,
and the `MetapackageSample` project which builds a metapackage that contains all package
references that your consumers will need:
* your `Attributes` package
* your `Generators` package (via custom `NupkgAdditionalDependency` Item)
* `CodeGeneration.Roslyn.Tool` package (implicitly added in the PluginMetapackage.Sdk).

Using that metapackage, your generator's users only need one PackageReferece to add:
> `dotnet add package MetapackageSample`
and have the source generator working immediately.

`MetapackageConsumer` is an example consumer project that references just
our single `MetapackageSample` package, and successfully runs the generator.

Important aspects of the `Metapackage/Metapackage.csproj`:
* it's built (packed) at the same time as Attributes and Generators;
* it's not producing any dll, and so should be only `pack`ed (and is packed on build)
* there are important comments for every element, please read them
* variables used (`$(PackageVersion)`, `$(LocalNuGetVersion)`) should be replaced
  with whatever you use in your setup. `LocalNuGetVersion` is the version of CG.R
  used across samples - you should use the same version as other CG.R packages you
  reference. `Directory.Build.props` is a good place to define that once.
  

> ⚠ Please note that Metapackage project doesn't change how P2P (`ProjectReference`)
> setup works - it **only** works as a NuGet package!
