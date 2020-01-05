# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
* GeneratorInCustomerSolution sample in `samples` folder
* GitHub Actions CI
* Support for plugin dependencies! 🎉 ([#156]).

### Changed
* .NET Core SDK version bumped to `3.1.100` ([#178]).
* `Attributes` package now targets `net20;net40` in addition to `netstandard1.0` ([#178]).
* `dotnet-codegen` now has `RollForward=Major` policy to allow it to run on newer runtimes than 2.x,
  e.g. .NET Core SDK v3.x *only* should suffice for most usage scenarios ([#178]).
* MSBuild ItemGroup used for registration of plugin paths changed to `CodeGenerationRoslynPlugin`
  (was `GeneratorAssemblySearchPaths`). A warning for using old one is introduced (`CGR1002`).  ([#156])
  * ItemGroup now should contain full path to generator dll (previously it was a containing folder path)
  * Old behavior has a compat-plug and the paths are searched for any dll, and those found are added to new ItemGroup.
  * When using P2P generator (same solution), a consuming project needs to add an attribute `OutputItemType="CodeGenerationRoslynPlugin"` to the `ProjectReference` of the generator project. See [v0.7 migration guide].

[#156]: https://github.com/AArnott/CodeGeneration.Roslyn/pull/156
[#178]: https://github.com/AArnott/CodeGeneration.Roslyn/pull/178
[v0.7 migration guide]: https://github.com/AArnott/CodeGeneration.Roslyn/wiki/Migrations#v07


## [0.6.1] - 2019-06-16

See https://github.com/AArnott/CodeGeneration.Roslyn/releases/tag/v0.6.1

[Unreleased]: https://github.com/amis92/RecordGenerator/compare/v0.6.1...HEAD
[0.6.1]: #061---2019-06-16
