#!/usr/bin/env pwsh

Push-Location $PSScriptRoot
try {
    # clean up all restore and build artifacts:
    Remove-Item .nuget, bin, obj -Recurse -Force
    
    # set env variable to use local CG.R packages
    $env:LocalNuGetVersion = nbgv get-version --variable NuGetPackageVersion --project ../src
    
    # get generator project folders
    $generators = Get-ChildItem -Directory -Name | Where-Object { $_ -match 'Generator$' }
    
    # pack generators to make them available in folder feed
    $generators | ForEach-Object {
        Write-Host "dotnet pack $_" -ForegroundColor Green
        dotnet pack $_
    }
    
    # build all other projects/solutions
    Get-ChildItem -Directory -Name -Exclude $generators | ForEach-Object {
        if (Get-ChildItem $_/* -File -Include *.csproj, *.sln) {
            Write-Host "dotnet build $_" -ForegroundColor Green
            dotnet build $_
        }
    }
}
finally {
    Pop-Location
}