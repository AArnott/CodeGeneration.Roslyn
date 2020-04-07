param (
    [Parameter()]
    [string[]] $InputFile,
    [Parameter()]
    [string[]] $OutputFile,
    [Parameter()]
    [string] $Version
)

while ($InputFile) {
    $input, $InputFile = $InputFile
    $output, $OutputFile = $OutputFile

    $json = Get-Content $input | ConvertFrom-Json
    $versionSymbol = $json.symbols.'cgr-version'
    if ($versionSymbol) {
        $versionSymbol.defaultValue = $Version
    }
    New-Item $output -Value (ConvertTo-Json $json) -Force
}