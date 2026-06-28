$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$excluded = [regex]'[\\/](?:\.git|bin|obj|\.test-output|TestResults)[\\/]'
$textExtensions = @(
    '.cs', '.xaml', '.csproj', '.props', '.targets', '.sln', '.md',
    '.json', '.yml', '.yaml', '.xml', '.ps1', '.txt', '.manifest'
)
$patterns = @(
    'AKIA[0-9A-Z]{16}',
    'AIza[0-9A-Za-z_-]{35}',
    'gh[pousr]_[A-Za-z0-9_]{20,}',
    '-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----'
)

$matches = New-Object System.Collections.Generic.List[string]
Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    -not $excluded.IsMatch($_.FullName) -and
    $textExtensions -contains $_.Extension.ToLowerInvariant()
} | ForEach-Object {
    $file = $_
    foreach ($pattern in $patterns) {
        Select-String -LiteralPath $file.FullName -Pattern $pattern -AllMatches |
            ForEach-Object {
                $relative = [IO.Path]::GetRelativePath($root, $file.FullName)
                $matches.Add("$relative`:$($_.LineNumber): potential secret pattern")
            }
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$forbiddenArchiveEntries = [regex]'(?i)(^|/)(accounts\.json|\.env(?:\..*)?|[^/]*\.(?:pem|key|pfx|p12|log))$|(^|/)webview/'
Get-ChildItem -LiteralPath (Join-Path $root 'release-assets') -Filter '*.zip' -File -ErrorAction SilentlyContinue |
    ForEach-Object {
        $archive = [IO.Compression.ZipFile]::OpenRead($_.FullName)
        try {
            foreach ($entry in $archive.Entries) {
                if ($forbiddenArchiveEntries.IsMatch($entry.FullName)) {
                    $matches.Add("$($_.Name):$($entry.FullName): forbidden release entry")
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }

if ($matches.Count -gt 0) {
    $matches | Sort-Object -Unique | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Security scan passed: no embedded secret patterns or forbidden release entries found.'
