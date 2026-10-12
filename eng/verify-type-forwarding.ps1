$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$legacyProject = Join-Path $repositoryRoot 'test\InfoCarrier.Core.BinaryCompatibility\LegacyMapperConsumer\LegacyMapperConsumer.csproj'
$hostProject = Join-Path $repositoryRoot 'test\InfoCarrier.Core.BinaryCompatibility\TypeForwardHost\TypeForwardHost.csproj'
$legacyAssembly = Join-Path $repositoryRoot 'test\InfoCarrier.Core.BinaryCompatibility\LegacyMapperConsumer\bin\Release\net10.0\LegacyMapperConsumer.dll'

dotnet build $legacyProject -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project $hostProject -c Release -- $legacyAssembly
exit $LASTEXITCODE
