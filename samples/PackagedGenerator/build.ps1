#!/usr/bin/env pwsh

Write-Host "Running in $PSScriptRoot" -ForegroundColor Cyan
Push-Location $PSScriptRoot
try {
    Write-Host "dotnet build" -ForegroundColor Green
    dotnet build
}
finally {
    Pop-Location
}