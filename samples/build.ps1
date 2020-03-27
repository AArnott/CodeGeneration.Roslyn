#!/usr/bin/env pwsh

param (
    [string[]]
    $Projects
)

Write-Host "Running in $PSScriptRoot" -ForegroundColor Cyan
Push-Location $PSScriptRoot
try {
    $env:Configuration ??= 'Debug'

    # clean up all restore and build artifacts:
    Remove-Item .nuget, bin, obj -Recurse -Force -ErrorAction Ignore
    
    # set env variable to use local CG.R packages
    $env:LocalNuGetVersion = dotnet nbgv get-version --variable NuGetPackageVersion --project ../src

    Write-Host "Using CG.R package version: $env:LocalNuGetVersion" -ForegroundColor Cyan
    
    # get generator project folders
    $generators = Get-ChildItem -Directory -Name | Where-Object { $_ -match 'Generator$' }
    
    # pack generators to make them available in folder feed
    $generators | Where-Object { $Projects -eq $null -or $Projects -contains $_} | ForEach-Object {
        Write-Host "dotnet pack $_" -ForegroundColor Green
        dotnet pack $_
    }
    
    # build all other projects/solutions
    Get-ChildItem -Directory -Name -Exclude $generators | Where-Object { $Projects -eq $null -or $Projects -contains $_} | ForEach-Object {
        if (Get-ChildItem $_/* -File -Include 'build.ps1') {
            Write-Host "$_/build.ps1" -ForegroundColor Green
            & "$_/build.ps1"
        }
        elseif (Get-ChildItem $_/* -File -Include *.csproj, *.sln) {
            Write-Host "dotnet build $_" -ForegroundColor Green
            dotnet build $_
        }
    }
}
finally {
    Pop-Location
}