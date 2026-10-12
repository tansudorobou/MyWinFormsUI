param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$targetRoot = [System.IO.Path]::GetFullPath($Destination)
$targetPrefix = $targetRoot.TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
if ($targetRoot -eq $repoRoot -or $repoRoot.StartsWith($targetPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'リポジトリ自身またはその親にはエクスポートできません。'
}
foreach ($protected in @('src', 'samples')) {
    $protectedRoot = Join-Path $repoRoot $protected
    if ($targetRoot -eq $protectedRoot -or $targetRoot.StartsWith($protectedRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw '元のsrc/samples配下にはエクスポートできません。'
    }
}
$registry = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'components.json') | ConvertFrom-Json
$names = @('Tansu.Forms.Layout', 'Tansu.Forms.Execution', 'Tansu.Forms.Components')
if ($registry.projects.Count -ne $names.Count -or @($registry.projects | Where-Object { $_.name -notin $names }).Count -or @($registry.projects.name | Sort-Object -Unique).Count -ne $names.Count) { throw 'プロジェクトのレジストリーが不正です。' }
$copies = [System.Collections.Generic.List[object]]::new()
$generated = [System.Collections.Generic.List[object]]::new()
foreach ($project in $registry.projects) {
    $name = $project.name
    $libraryRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "src/$name")) + [System.IO.Path]::DirectorySeparatorChar
    foreach ($source in $project.sourceFiles) {
        $sourcePath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $source))
        if (-not $sourcePath.StartsWith($libraryRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'レジストリーにプロジェクト外のソースがあります。' }
        $relative = [System.IO.Path]::GetRelativePath($libraryRoot, $sourcePath)
        $targetPath = [System.IO.Path]::GetFullPath((Join-Path $targetRoot "$name/$relative"))
        if (-not $targetPath.StartsWith($targetPrefix, [System.StringComparison]::OrdinalIgnoreCase)) { throw '出力先外へのパスが含まれています。' }
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "ソースがありません: $source" }
        $copies.Add(@{ Source = $sourcePath; Target = $targetPath })
    }
    $references = foreach ($dependency in $project.dependencies) {
        if ($dependency -notin $names -or $dependency -eq $name) { throw 'プロジェクト依存が不正です。' }
        '    <ProjectReference Include="../{0}/{0}.csproj" />' -f $dependency
    }
    $content = if ($name -eq 'Tansu.Forms.Components') { '    <None Include="../themes/*.json" Link="themes/%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />' } else { '' }
    $framework = $project.targetFramework
    $windowsForms = $project.useWindowsForms.ToString().ToLowerInvariant()
    $xml = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$framework</TargetFramework>
    <UseWindowsForms>$windowsForms</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
$($references -join [Environment]::NewLine)
$content
  </ItemGroup>
</Project>
"@
    $generated.Add(@{ Target = (Join-Path $targetRoot "$name/$name.csproj"); Content = $xml })
}
foreach ($relative in @('README.md', 'components.json', 'themes/native.json', 'themes/normal.json', 'themes/dark.json')) {
    $copies.Add(@{ Source = (Join-Path $repoRoot $relative); Target = (Join-Path $targetRoot $relative) })
}
# Check all destinations before writing any files.
foreach ($file in @($copies.ToArray()) + @($generated.ToArray())) {
    if ((Test-Path -LiteralPath $file.Target) -and -not $Force) { throw "出力先にファイルが存在します。上書きには-Forceを指定してください: $($file.Target)" }
}
foreach ($file in $copies) {
    New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($file.Target)) -Force | Out-Null
    Copy-Item -LiteralPath $file.Source -Destination $file.Target
}
foreach ($file in $generated) {
    New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($file.Target)) -Force | Out-Null
    [System.IO.File]::WriteAllText($file.Target, $file.Content)
}
Write-Output "Layout, Execution and Components source exported to $targetRoot"
