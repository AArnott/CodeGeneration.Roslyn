#!/usr/bin/env pwsh

function PrintAndInvoke {
    param ($expression)
    Write-Host "$expression" -ForegroundColor Green
    Invoke-Expression $expression
}

Write-Host "Running in $PSScriptRoot" -ForegroundColor Cyan
Push-Location $PSScriptRoot
try {
    $env:Configuration ??= 'Debug'
    
    # set variable to local CG.R version
    $version = dotnet nbgv get-version --variable NuGetPackageVersion --project ../src
    Write-Host "Using CG.R package version: $version" -ForegroundColor Cyan

    # make local nuget directories for both configurations, because
    # both Debug and Release are specified in nuget.config and have to exist (or nuget will fail)
    New-Item ../bin/Packages/Debug, ../bin/Packages/Release -ItemType Directory -ErrorAction:Ignore

    # install template pack
    PrintAndInvoke "dotnet new --install CodeGeneration.Roslyn.Templates::$version --nuget-source $(Get-Location)/../bin/Packages/$env:Configuration/"

    # move to test dir, empty it
    $testRoot = "bin/template-tests"
    New-Item $testRoot -ItemType Directory -ErrorAction:Ignore
    Remove-Item $testRoot/* -Recurse -Force
    Push-Location $testRoot
    try {
        # whole solution
        PrintAndInvoke "dotnet new cgrplugin --sln -o TestPlugin && dotnet build TestPlugin"
        # generators project
        PrintAndInvoke "dotnet new cgrplugingens -o TestGens && dotnet build TestGens"
        # add generator to existing project
        PrintAndInvoke "dotnet new cgrgen -o TestGens -n NewGen && dotnet build TestGens"
        # attributes project
        PrintAndInvoke "dotnet new cgrpluginatts -o TestAtts && dotnet build TestAtts"
        # add attribute to existing project
        PrintAndInvoke "dotnet new cgratt -o TestAtts -n NewGen && dotnet build TestAtts"
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}