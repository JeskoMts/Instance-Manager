param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-Zip {
    param([string]$Path, [array]$Entries)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force
    }

    $zip = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $Entries) {
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $entry.File, $entry.Name, [IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally {
        $zip.Dispose()
    }
}

$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $root 'release-assets'
}

$projectFile = Join-Path $root 'src\InstanceManager\InstanceManager.csproj'
$version = @(([xml](Get-Content -LiteralPath $projectFile -Raw)).Project.PropertyGroup.Version | Where-Object { $_ })[0]
if (-not $version) {
    throw 'No <Version> found in InstanceManager.csproj.'
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('im-release-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $work 'source'
$publish = Join-Path $work 'publish'

try {
    $files = @(git -C $root -c core.quotepath=false ls-files --cached --others --exclude-standard |
        Where-Object { Test-Path -LiteralPath (Join-Path $root $_) -PathType Leaf })
    if ($LASTEXITCODE -ne 0 -or $files.Count -eq 0) {
        throw 'git ls-files failed.'
    }

    foreach ($file in $files) {
        $target = Join-Path $source $file
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        Copy-Item -LiteralPath (Join-Path $root $file) -Destination $target
    }

    & dotnet publish (Join-Path $source 'src\InstanceManager\InstanceManager.csproj') -c Release -r win-x64 -o $publish --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet publish failed.'
    }

    $published = @(Get-ChildItem -LiteralPath $publish -File)
    if ($published.Count -ne 1 -or $published[0].Name -ne 'InstanceManager.exe') {
        throw "The publish output must be InstanceManager.exe alone, found: $($published.Name -join ', ')"
    }

    $exe = $published[0].FullName
    $assembly = Join-Path $source 'src\InstanceManager\bin\Release\net8.0-windows\win-x64\InstanceManager.dll'
    if (-not (Test-Path -LiteralPath $assembly)) {
        throw "Missing $assembly"
    }

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $download = Join-Path $OutputDirectory "Instance.Manager.$version.zip"
    $update = Join-Path $OutputDirectory "InstanceManager-$version.zip"
    $sourceZip = Join-Path $OutputDirectory "InstanceManager-Source-Code-$version.zip"

    New-Zip $download @(@{ File = $exe; Name = 'Instance Manager.exe' })
    New-Zip $update @(
        @{ File = $exe; Name = 'InstanceManager.exe' },
        @{ File = $assembly; Name = 'InstanceManager.dll' })
    New-Zip $sourceZip @($files | ForEach-Object { @{ File = (Join-Path $root $_); Name = $_ } })

    foreach ($zip in @($download, $update, $sourceZip)) {
        $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        '{0}  {1:N0} bytes  sha256:{2}' -f (Split-Path -Leaf $zip), (Get-Item -LiteralPath $zip).Length, $hash
    }
}
finally {
    if (Test-Path -LiteralPath $work) {
        Remove-Item -LiteralPath $work -Recurse -Force
    }
}
