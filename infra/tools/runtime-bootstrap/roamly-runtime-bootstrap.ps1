$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$project = Join-Path $PSScriptRoot 'bridge\Roamly.RuntimeBootstrap.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    [Console]::Error.WriteLine('Il progetto bridge non esiste; non è stata avviata alcuna operazione Azure o SQL.')
    exit 2
}

& dotnet run --project $project --configuration Release --no-launch-profile
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
