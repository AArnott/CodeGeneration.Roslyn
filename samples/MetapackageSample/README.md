# MetapackageSample

This sample demonstrates an approach to providing a nice UX for our generator consumers.

The `MetapackageSample.sln` solution builds and packs `Attributes` and `Generators` packages,
and then using those packages from local folder feed, `MetapackageSample` project
is packed and creates a new NuGet package that just references those packages,
as well as the `CodeGeneration.Roslyn.Tool` package.

This allows your consumers to simply
> `dotnet add package MetapackageSample`
and have the source generator working immediately.

Important aspects of the `Metapackage/Metapackage.csproj`:
* it's not built at the same time as Attributes and Generators because it needs
  their NuGets to be in the folder feed it uses.
* it's not producing any dll, and so should be only `pack`ed
* there are important comments for every element, please read them
* variables used (`$(PackageVersion)`, `$(LocalNuGetVersion)`) should be replaced
  with whatever you use in your setup. `LocalNuGetVersion` is the version of CG.R
  used across samples - you should use the same version as other CG.R packages you
  reference. `Directory.Build.props` is a good place to define that once.

`MetapackageConsumer` is an example consumer project that references just
our single `MetapackageSample` package, and successfully runs the generator.