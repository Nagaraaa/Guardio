[CmdletBinding()]
param(
    [string]$OutputDirectory = '',
    [ValidatePattern('^8\.0\.\d+$')]
    [string]$RuntimeVersion = '8.0.32'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/GardeAudio/GardeAudio.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a three-part project version.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

# Each run gets fresh staging folders; no recursive deletion or broad output glob.
$stagingRoot = Join-Path $OutputDirectory ('staging-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingRoot | Out-Null
$nugetRoot = $env:NUGET_PACKAGES
if (-not $nugetRoot) {
    $locations = & dotnet nuget locals global-packages --list
    if ($LASTEXITCODE -ne 0) { throw 'Could not locate the NuGet package cache.' }
    $nugetRoot = ($locations | Select-Object -Last 1) -replace '^global-packages:\s*', ''
}

$archives = @()
foreach ($kind in @('standalone', 'framework-dependent')) {
    $standalone = $kind -eq 'standalone'
    $publishDirectory = Join-Path $stagingRoot ($kind + '-publish')
    $packageDirectory = Join-Path $stagingRoot $kind
    New-Item -ItemType Directory -Path $packageDirectory | Out-Null
    $publishArguments = @(
        'publish', $project, '-c', 'Release', '-r', 'win-x64',
        '--self-contained', $standalone.ToString().ToLowerInvariant(),
        '-p:PublishSingleFile=true', '-p:DebugType=None', '-p:DebugSymbols=false',
        '-p:ContinuousIntegrationBuild=true', '-o', $publishDirectory, '--nologo'
    )
    if ($standalone) {
        $publishArguments += @('-p:IncludeNativeLibrariesForSelfExtract=true', "-p:RuntimeFrameworkVersion=$RuntimeVersion")
    }
    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $kind" }
    Copy-Item -LiteralPath (Join-Path $publishDirectory 'GardeAudio.exe') -Destination $packageDirectory
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $packageDirectory 'LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'START-HERE.txt') -Destination $packageDirectory

    if ($standalone) {
        $licenses = Join-Path $packageDirectory 'licenses'
        New-Item -ItemType Directory -Path $licenses | Out-Null
        $core = Join-Path $nugetRoot "microsoft.netcore.app.runtime.win-x64/$RuntimeVersion"
        $desktop = Join-Path $nugetRoot "microsoft.windowsdesktop.app.runtime.win-x64/$RuntimeVersion"
        Copy-Item -LiteralPath (Join-Path $core 'LICENSE.TXT') -Destination (Join-Path $licenses 'dotnet-runtime-LICENSE.txt')
        Copy-Item -LiteralPath (Join-Path $core 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $licenses 'dotnet-runtime-THIRD-PARTY-NOTICES.txt')
        Copy-Item -LiteralPath (Join-Path $desktop 'LICENSE') -Destination (Join-Path $licenses 'windowsdesktop-LICENSE.txt')
    }

    $suffix = if ($standalone) { '' } else { '-framework-dependent' }
    $archive = Join-Path $OutputDirectory "GardeAudio-v$version-windows-x64$suffix.zip"
    Compress-Archive -Path (Join-Path $packageDirectory '*') -DestinationPath $archive -CompressionLevel Optimal -Force
    $archives += $archive
}

$checksums = foreach ($archive in $archives) {
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($archive))"
}
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS.txt'), ($checksums -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
Get-Item -LiteralPath $archives | Select-Object Name, Length
Write-Host "Packages and SHA256SUMS.txt: $OutputDirectory"
