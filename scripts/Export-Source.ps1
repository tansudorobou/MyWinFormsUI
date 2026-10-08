param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$targetRoot = [System.IO.Path]::GetFullPath($Destination)
if ($targetRoot.StartsWith((Join-Path $repoRoot 'src'), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw '元のライブラリのsrc配下にはエクスポートできません。'
}
$registry = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'components.json') | ConvertFrom-Json
$projectFile = Join-Path $targetRoot 'WinformsUI.csproj'
if ((Test-Path -LiteralPath $projectFile) -and -not $Force) { throw '出力先にプロジェクトが存在します。上書きする場合は-Forceを指定してください。' }
New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
foreach ($source in $registry.sourceFiles) {
    $sourcePath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $source))
    $libraryRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'src/WinformsUI')) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $sourcePath.StartsWith($libraryRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'レジストリーにsrc外のパスが含まれています。' }
    $relative = [System.IO.Path]::GetRelativePath($libraryRoot, $sourcePath)
    $targetPath = [System.IO.Path]::GetFullPath((Join-Path $targetRoot $relative))
    if (-not $targetPath.StartsWith($targetRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) { throw '出力先外へのパスが含まれています。' }
    if ((Test-Path -LiteralPath $targetPath) -and -not $Force) { throw "ファイルが存在します: $relative" }
    New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($targetPath)) -Force | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $targetPath
}
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
'@
[System.IO.File]::WriteAllText($projectFile, $project)
Copy-Item -LiteralPath (Join-Path $repoRoot 'components.json') -Destination (Join-Path $targetRoot 'components.json')
Write-Output "Source exported to $targetRoot"
