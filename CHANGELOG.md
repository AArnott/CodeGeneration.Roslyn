# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.7.63] - 2020-04-08

> ⚠ There are major changes, please look at [v0.7 migration guide].

### Added
* Template pack for dotnet new: `dotnet new -i CodeGeneration.Roslyn.Templates`
* Various samples in `samples` folder
* GitHub Actions CI
* Support for plugin dependencies! 🎉 ([#156]).
* Plugins (generators) are now easier to build using `CodeGeneration.Roslyn.Plugin.Sdk` MSBuildSdk package ([#113]).
* Plugins are now easier to distribute using `CodeGeneration.Roslyn.PluginMetapackage.Sdk` MSBuildSdk package, see
[Readme section](https://github.com/AArnott/CodeGeneration.Roslyn#create-the-metapackage) ([#205]).

### Changed
* Readme demo is now simpler, and suggests usage of Templates package, and Sdks.
* .NET Core SDK version bumped to `3.1.100` ([#178]).
* `Attributes` package now targets `net20;net40` in addition to `netstandard1.0` ([#178]).
* Tool now has `RollForward=Major` policy to allow it to run on newer runtimes than 2.x,
  e.g. .NET Core SDK v3.x *only* should suffice for most usage scenarios ([#178]).
* MSBuild ItemGroup used for registration of plugin paths changed to `CodeGenerationRoslynPlugin`
  (was `GeneratorAssemblySearchPaths`). A warning for using old one is introduced (`CGR1002`).  ([#156])
  * ItemGroup now should contain full path to generator dll (previously it was a containing folder path)
  * Old behavior has a compat-plug for now and the paths are searched for any dll, and those found are added to new ItemGroup.
  * When using P2P generator (same solution), a consuming project needs to add an attribute `OutputItemType="CodeGenerationRoslynPlugin"` to the `ProjectReference` of the generator project. See [v0.7 migration guide].
* `dotnet-codegen` package is now `CodeGeneration.Roslyn.Tool` and is built very differently;
  also it includes build assets from `BuildTime` package ([#198]).

### Removed
* `CodeGeneration.Roslyn.BuildTime` package is now merged into `CodeGeneration.Roslyn.Tool`
  (which is now the only package required to be referenced by generator consumers, aside from generators themselves) ([#198]).

[#113]: https://github.com/AArnott/CodeGeneration.Roslyn/issues/113
[#156]: https://github.com/AArnott/CodeGeneration.Roslyn/pull/156
[#178]: https://github.com/AArnott/CodeGeneration.Roslyn/pull/178
[#198]: https://github.com/AArnott/CodeGeneration.Roslyn/pull/198
[#205]: https://github.com/AArnott/CodeGeneration.Roslyn/pull/205
[v0.7 migration guide]: https://github.com/AArnott/CodeGeneration.Roslyn/wiki/Migrations#v07


## [0.6.1] - 2019-06-16

See https://github.com/AArnott/CodeGeneration.Roslyn/releases/tag/v0.6.1

[Unreleased]: https://github.com/AArnott/CodeGeneration.Roslyn/compare/v0.7.63...HEAD
[0.7.63]: https://github.com/AArnott/CodeGeneration.Roslyn/compare/v0.6.1...v0.7.63
[0.6.1]: https://github.com/AArnott/CodeGeneration.Roslyn/releases/tag/v0.6.1
