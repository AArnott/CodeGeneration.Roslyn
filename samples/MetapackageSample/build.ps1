#!/usr/bin/env pwsh

Write-Host "Running in $PSScriptRoot" -ForegroundColor Cyan
Push-Location $PSScriptRoot
try {
    # pack directory (solution), then pack Metapackage
    '.', 'MetapackageSample' | ForEach-Object {
        Write-Host "dotnet pack $_" -ForegroundColor Green
        dotnet pack $_
    }
    Write-Host "dotnet run -p MetapackageConsumer" -ForegroundColor Green
    dotnet run -p MetapackageConsumer
}
finally {
    Pop-Location
}