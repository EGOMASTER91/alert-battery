$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'Compilatorul .NET Framework inclus în Windows nu este disponibil.'
}
$output = Join-Path $projectRoot 'BatteryAlert.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /debug- /reference:System.Windows.Forms.dll "/win32icon:$projectRoot/src/BatteryAlert.App/Assets/app.ico" "/out:$output" (Join-Path $PSScriptRoot 'BatteryAlertLauncher.cs')
if ($LASTEXITCODE -ne 0) { throw 'Crearea fișierului de pornire a eșuat.' }
Write-Output $output
