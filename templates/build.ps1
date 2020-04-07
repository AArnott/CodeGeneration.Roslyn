#!/usr/bin/env pwsh

param (
    [string[]]
    $Projects
)

function PrintAndInvoke {
    param ($expression)
    Write-Host "$expression" -ForegroundColor Green
    Invoke-Expression $expression
}

Write-Host "Running in $PSScriptRoot" -ForegroundColor Cyan
Push-Location $PSScriptRoot
try {
    $env:Configuration ??= 'Debug'

    # clean up all restore and build artifacts:
    Remove-Item .nuget, bin, obj -Recurse -Force -ErrorAction Ignore
    
    # set env variable to use local CG.R packages
    $env:LocalNuGetVersion = dotnet nbgv get-version --variable NuGetPackageVersion --project ../src

    Write-Host "Using CG.R package version: $env:LocalNuGetVersion" -ForegroundColor Cyan

    # check that CG.R packages for this version exist
    $cgrNupkg = Get-ChildItem ../bin/Packages/$env:Configuration/ -Filter *.nupkg
    | Where-Object BaseName -Match "$env:LocalNuGetVersion$"
    if (!$cgrNupkg) {
        Write-Host "CG.R packages for this version not found, packing..."
        PrintAndInvoke "dotnet pack ../src"
    }

    # make local nuget directories for both configurations, because
    # both Debug and Release are specified in nuget.config and have to exist (or nuget will fail)
    New-Item ../bin/Packages/Debug, ../bin/Packages/Release -ItemType Directory -ErrorAction:Ignore
    
    # build all projects/solutions
    Push-Location content
    try {
        Get-ChildItem -Directory -Name | Where-Object { $Projects -eq $null -or $Projects -contains $_ } | ForEach-Object {
            if (Get-ChildItem $_/* -File -Include 'build.ps1') {
                PrintAndInvoke "$_/build.ps1"
            }
            elseif (Get-ChildItem $_/* -File -Include *.csproj, *.sln) {
                PrintAndInvoke "dotnet build $_"
            }
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}