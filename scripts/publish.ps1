param([string]$Configuration = 'Release', [switch]$Local)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'dist'))
$variant = if ($Local) { 'local' } else { 'portable' }
$outputDirectory = Join-Path $distRoot ('build-' + $variant)
$packageDirectory = Join-Path $distRoot ('package-' + $variant)
# Validate every recursive cleanup target before deleting previous build output.
foreach ($directory in @($outputDirectory, $packageDirectory)) {
    $resolved = [IO.Path]::GetFullPath($directory)
    if (-not $resolved.StartsWith($distRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe publish path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
$helper = Join-Path $projectRoot 'vendor\headsetcontrol\headsetcontrol.exe'
$source = Join-Path $projectRoot 'vendor\headsetcontrol\headsetcontrol-4.1.0.tar.gz'
if ((Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash -ne '047F0AE26A06D86F6EC5A543166DF20CCD47EE07148C915221AA08B97BC190DD') { throw 'HeadsetControl checksum mismatch.' }
if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne '8084EFE74C96DB051CDB31B1CF7FF4E3D441C97DAA6C85A35209278EC7448B8E') { throw 'HeadsetControl source checksum mismatch.' }
$selfContained = if ($Local) { 'false' } else { 'true' }
dotnet publish (Join-Path $projectRoot 'src\BatteryAlert.App\BatteryAlert.App.csproj') -c $Configuration -r win-x64 --self-contained $selfContained -o $outputDirectory -p:TargetLatestRuntimePatch=true -p:DebugType=None -p:DebugSymbols=false "-p:PathMap=$projectRoot=/_/BatteryAlert"
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
& (Join-Path $PSScriptRoot 'create-launcher.ps1')
$runtimeDirectory = Join-Path $packageDirectory 'dist\BatteryAlert'
New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $outputDirectory -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $runtimeDirectory -Recurse -Force }
Copy-Item -LiteralPath (Join-Path $projectRoot 'BatteryAlert.exe') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'SECURITY.md') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\DESIGN-ASSETS.md') -Destination $runtimeDirectory
$documentationDirectory = Join-Path $packageDirectory 'docs'
New-Item -ItemType Directory -Path $documentationDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\COMPATIBILITY.md'), (Join-Path $projectRoot 'docs\VALIDATION.md'), (Join-Path $projectRoot 'docs\SECURITY-AUDIT.md'), (Join-Path $projectRoot 'docs\DESIGN-ASSETS.md') -Destination $documentationDirectory
$forbidden = @(Get-ChildItem -LiteralPath $packageDirectory -Recurse -Force -File | Where-Object { $_.Extension -in @('.pdb', '.log', '.trx') -or $_.Name -like 'state.json*' })
if ($forbidden.Count) { throw 'Unexpected diagnostic/state files in application package.' }
$version = ([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$zipPath = Join-Path $distRoot ("BatteryAlert-v$version-win-x64-$variant.zip")
Compress-Archive -Path (Join-Path $packageDirectory '*') -DestinationPath $zipPath -Force
Write-Output $zipPath

