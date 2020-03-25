# Samples

## Usage

If you want to use samples, they'll build against **latest stable** `CodeGeneration.Roslyn` packages from NuGet.

To build against locally built packages, specify `LocalNuGetVersion`. The easiest
way is to save the result of `CodeGeneration.Roslyn> nbgv get-version -p src` into
`LocalNuGetVersion` environment variable in the terminal from which you'll then
`dotnet run` or build specific samples.

From PowerShell, for example:
```powershell
pwsh> $env:LocalNuGetVersion = nbgv get-version -p src
pwsh> dotnet pack samples/PackagedGenerator
pwsh> dotnet run samples/PackageConsumer
```

After you've rebuilt any packages, simply delete the `samples/.nuget` folder
and new packages should restore.