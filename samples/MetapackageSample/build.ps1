#!/usr/bin/env pwsh

Write-Host "Running in $PSScriptRoot" -ForegroundColor Cyan
Push-Location $PSScriptRoot
try {
    $env:Configuration ??= 'Debug'

    # clean up all restore and build artifacts:
    Remove-Item .nuget, bin, obj -Recurse -Force -ErrorAction Ignore
    
    # set env variable to use local CG.R packages
    $env:LocalNuGetVersion = dotnet nbgv get-version --variable NuGetPackageVersion --project ../../src

    Write-Host "Using CG.R package version: $env:LocalNuGetVersion" -ForegroundColor Cyan
    
    # pack directory (solution), then pack Metapackage
    '.','MetapackageSample' | ForEach-Object {
        Write-Host "dotnet pack $_" -ForegroundColor Green
        dotnet pack $_
    }
    Write-Host "dotnet run MetapackageConsumer" -ForegroundColor Green
    dotnet run -p MetapackageConsumer
}
finally {
    Pop-Location
}