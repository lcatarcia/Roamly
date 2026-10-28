param(
    [Parameter(Mandatory)]
    [string]$ArgumentsJson
)

$ErrorActionPreference = 'Stop'
[string[]]$arguments = ConvertFrom-Json -InputObject $ArgumentsJson
& az @arguments
exit $LASTEXITCODE
