# Builds the release assets for the version set in src\InstanceManager\InstanceManager.csproj:
#   InstanceManager-<version>.zip              the app: one file, "Instance Manager.exe"
#   InstanceManager-Source-Code-<version>.zip  the source code the app was built from
param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$ExcludedFolders = @('bin', 'obj', '.git', '.vs', '.vscode', '.idea', '.impeccable', 'release-assets', 'publish', 'artifacts', 'TestResults')

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

function Test-GitRoot {
    param([string]$Path)

    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        return $false
    }

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $top = git -C $Path rev-parse --show-toplevel 2>$null
    }
    finally {
        $ErrorActionPreference = $previous
    }

    return $LASTEXITCODE -eq 0 -and $top -and
        [IO.Path]::GetFullPath($top).TrimEnd('\') -eq [IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Get-SourceFiles {
    param([string]$Path)

    if (Test-GitRoot $Path) {
        # A git checkout ships what git tracks plus new files that aren't ignored.
        $files = @(git -C $Path -c core.quotepath=false ls-files --cached --others --exclude-standard)
        if ($LASTEXITCODE -ne 0) {
            throw 'git ls-files failed.'
        }
    }
    else {
        # An unpacked source archive ships everything except build output.
        $prefix = $Path.TrimEnd('\') + '\'
        $files = @(Get-ChildItem -LiteralPath $Path -Recurse -File -Force |
            ForEach-Object { $_.FullName.Substring($prefix.Length).Replace('\', '/') } |
            Where-Object {
                $folders = @($_.Split('/') | Select-Object -SkipLast 1)
                -not ($folders | Where-Object { $ExcludedFolders -contains $_ })
            })
    }

    return @($files |
        Where-Object { Test-Path -LiteralPath (Join-Path $Path $_) -PathType Leaf } |
        Sort-Object -Unique)
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
    $files = Get-SourceFiles $root
    if ($files.Count -eq 0) {
        throw "No source files found in $root."
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

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $appZip = Join-Path $OutputDirectory "InstanceManager-$version.zip"
    $sourceZip = Join-Path $OutputDirectory "InstanceManager-Source-Code-$version.zip"

    New-Zip $appZip @(@{ File = $published[0].FullName; Name = 'Instance Manager.exe' })
    New-Zip $sourceZip @($files | ForEach-Object { @{ File = (Join-Path $root $_); Name = $_ } })

    foreach ($zip in @($appZip, $sourceZip)) {
        $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        '{0}  {1:N0} bytes  sha256:{2}' -f (Split-Path -Leaf $zip), (Get-Item -LiteralPath $zip).Length, $hash
    }
}
finally {
    if (Test-Path -LiteralPath $work) {
        Remove-Item -LiteralPath $work -Recurse -Force
    }
}
