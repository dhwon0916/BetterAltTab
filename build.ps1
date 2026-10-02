param([switch]$SelfContained, [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'BetterAltTab/BetterAltTab.csproj'
$tests = Join-Path $PSScriptRoot 'tests/BetterAltTab.Tests.csproj'
dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
dotnet run --project $tests -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
if ($SelfContained) {
    dotnet publish $project -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o (Join-Path $PSScriptRoot "artifacts/$Runtime")
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
}
