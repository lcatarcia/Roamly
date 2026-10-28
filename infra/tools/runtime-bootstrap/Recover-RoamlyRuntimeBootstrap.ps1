$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$project = Join-Path $PSScriptRoot 'bridge\Roamly.RuntimeBootstrap.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    [Console]::Error.WriteLine('Il progetto bridge non esiste; il ripristino non è stato avviato.')
    exit 2
}

& dotnet run --project $project --configuration Release --no-launch-profile -- --recover
exit $LASTEXITCODE
