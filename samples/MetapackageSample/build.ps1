#!/usr/bin/env pwsh

function PrintAndInvoke {
    param ($expression)
    Write-Host "$expression" -ForegroundColor Green
    Invoke-Expression $expression
}

Write-Host "Running in $PSScriptRoot" -ForegroundColor Cyan
Push-Location $PSScriptRoot
try {
    # pack directory (solution)
    PrintAndInvoke "dotnet pack"
    # run consumer
    PrintAndInvoke "dotnet run -p MetapackageConsumer"
}
finally {
    Pop-Location
}