#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$root = Join-Path $repo 'vendor/vc-runtime'
$manifest = Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json
$catalog = Get-Content -LiteralPath (Join-Path $repo 'licenses/components.json') -Raw | ConvertFrom-Json
$component = @($catalog.components | Where-Object id -EQ 'Microsoft.VisualC.Runtime.win-x64')
if ($component.Count -ne 1 -or $component[0].version -ne $manifest.version -or $manifest.architecture -ne 'win-x64') {
    throw 'Visual C++ runtime version/architecture differs from the license inventory'
}
$expected = @('msvcp140.dll','msvcp140_1.dll','vcruntime140.dll','vcruntime140_1.dll')
$actual = @(Get-ChildItem -LiteralPath (Join-Path $root 'win-x64') -File | Select-Object -ExpandProperty Name)
if (Compare-Object $expected $actual) { throw 'Unexpected or missing Visual C++ runtime files' }
if (Compare-Object $expected @($manifest.files.PSObject.Properties.Name)) { throw 'Invalid Visual C++ runtime manifest' }
foreach ($name in $expected) {
    $path = Join-Path $root ('win-x64/' + $name)
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $manifest.files.$name) {
        throw "Visual C++ runtime checksum mismatch: $name"
    }
    if ((Get-Item -LiteralPath $path).VersionInfo.FileVersion -ne $manifest.version) {
        throw "Visual C++ runtime file version mismatch: $name"
    }
    $bytes = [IO.File]::ReadAllBytes($path)
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
    if ([BitConverter]::ToUInt16($bytes, $peOffset + 4) -ne 0x8664) {
        throw "Visual C++ runtime must be x64: $name"
    }
}
Write-Output "Verified Visual C++ runtime $($manifest.version) (four x64 DLLs)"
