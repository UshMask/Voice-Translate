#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$Version,
    [string]$SelfPublish = 'out/self',
    [string]$FxPublish = 'out/fx',
    [string]$OutputDirectory = 'out/assets'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& (Join-Path $PSScriptRoot 'verify-vc-runtime.ps1')
if (-not $Version) {
    $Version = ([xml](Get-Content -LiteralPath (Join-Path $repo 'src/Voica/Voica.csproj'))).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
}
if ($Version -notmatch '^\d+\.\d+\.\d+([.-][a-zA-Z0-9.-]+)?$') { throw 'Invalid package version' }
$licenseRoot = Join-Path $repo 'licenses'
$catalog = Get-Content -LiteralPath (Join-Path $licenseRoot 'components.json') -Raw | ConvertFrom-Json
$known = @{}
foreach ($component in $catalog.components) { $known[$component.id] = $component }
$assets = Get-Content -LiteralPath (Join-Path $repo 'src/Voica/obj/project.assets.json') -Raw | ConvertFrom-Json
foreach ($library in $assets.libraries.PSObject.Properties) {
    if ($library.Value.type -ne 'package') { continue }
    $id, $resolvedVersion = $library.Name -split '/', 2
    if ($id -eq 'Microsoft.NET.ILLink.Tasks') { continue } # compiler tooling, not shipped
    if (-not $known.ContainsKey($id) -or $known[$id].version -ne $resolvedVersion) {
        throw "Update licenses/components.json for resolved dependency $($library.Name)"
    }
}
foreach ($framework in $assets.project.frameworks.PSObject.Properties) {
    foreach ($dependency in $framework.Value.downloadDependencies) {
        if (-not $known.ContainsKey($dependency.name)) { continue }
        $resolvedVersion = (($dependency.version -replace '[\[\]\s]', '') -split ',')[0]
        if ($known[$dependency.name].version -ne $resolvedVersion) {
            throw "Update runtime license inventory for $($dependency.name) $resolvedVersion"
        }
    }
}
$expectedFiles = @('components.json') + @($catalog.files.PSObject.Properties.Name)
foreach ($entry in $catalog.files.PSObject.Properties) {
    $file = [IO.Path]::GetFullPath((Join-Path $licenseRoot $entry.Name))
    if (-not $file.StartsWith($licenseRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Invalid license path: $($entry.Name)"
    }
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Value.sha256) {
        throw "License checksum mismatch: $($entry.Name)"
    }
}
foreach ($file in Get-ChildItem -LiteralPath $licenseRoot -File -Recurse) {
    $relative = [IO.Path]::GetRelativePath($licenseRoot, $file.FullName).Replace('\','/')
    if ($relative -notin $expectedFiles) { throw "Unindexed license file: $relative" }
}
$output = [IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
if (-not $output.StartsWith($repo + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Package output must be inside the repository workspace'
}
New-Item -ItemType Directory -Force $output | Out-Null
foreach ($variant in @(@{folder=$SelfPublish;suffix='';runtime='Includes the .NET runtime / Среда .NET включена.'},
                        @{folder=$FxPublish;suffix='-fx';runtime='Requires .NET 8 Desktop Runtime 8.0.33+ x64 / Требуется .NET 8 Desktop Runtime 8.0.33+ x64.'})) {
    $source = [IO.Path]::GetFullPath((Join-Path $repo $variant.folder))
    $executable = Join-Path $source 'Voica.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Publish first: $executable" }
    $stage = Join-Path $output ('.portable-stage-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory $stage | Out-Null
    try {
        Copy-Item -LiteralPath $executable -Destination (Join-Path $stage 'VoiceTranslate.exe')
        foreach ($name in @('LICENSE','THIRD-PARTY-NOTICES.txt','DISTRIBUTION-TERMS.txt')) {
            Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $stage
        }
        Copy-Item -LiteralPath $licenseRoot -Destination (Join-Path $stage 'licenses') -Recurse
        $readme = @("Voice Translate $Version", $variant.runtime, '',
            'Run VoiceTranslate.exe. Visual C++ Runtime is included in the executable; no separate installation is needed. Keep LICENSE, DISTRIBUTION-TERMS.txt, THIRD-PARTY-NOTICES.txt and licenses/ when redistributing. Microsoft terms apply only to Microsoft components. GigaAM is downloaded separately.', '',
            'Запустите VoiceTranslate.exe. Visual C++ Runtime включён в EXE, отдельная установка не нужна. При передаче программы сохраняйте весь комплект лицензий. Условия Microsoft относятся к компонентам Microsoft; код форка остаётся под MIT. Модель GigaAM скачивается отдельно.') -join [Environment]::NewLine
        [IO.File]::WriteAllText((Join-Path $stage 'README.txt'),$readme,[Text.UTF8Encoding]::new($false))
        $archive = Join-Path $output "VoiceTranslate-$Version-win-x64$($variant.suffix).zip"
        $temporaryArchive = $stage + '.zip'
        [IO.Compression.ZipFile]::CreateFromDirectory($stage,$temporaryArchive,[IO.Compression.CompressionLevel]::Optimal,$false)
        Move-Item -LiteralPath $temporaryArchive -Destination $archive -Force
        ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($archive)) |
            Set-Content -LiteralPath ($archive + '.sha256') -Encoding ascii
        Write-Output "Created $archive"
    }
    finally {
        $resolvedStage = [IO.Path]::GetFullPath($stage)
        if (-not $resolvedStage.StartsWith($output + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
            throw 'Unsafe staging cleanup path'
        }
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
        if (Test-Path -LiteralPath ($stage + '.zip')) { Remove-Item -LiteralPath ($stage + '.zip') }
    }
}
