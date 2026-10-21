# 多语言词表人工观察：按语言输出词条译文，便于核对翻译是否生效、是否有漏翻
#
# 用法：
#   pwsh -File Tools/Scripts/show-locales.ps1
#   pwsh -File Tools/Scripts/show-locales.ps1 -All
#   pwsh -File Tools/Scripts/show-locales.ps1 -Key "Invalid AVI.,No codec is registered for [{0}]."
#   pwsh -File Tools/Scripts/show-locales.ps1 -All -Csv > locales.csv
param(
    [String]$LocaleDir = "NewLife.Video\Locales",
    [String]$Baseline = "zh-Hans.yml",
    [String]$Key,
    [Switch]$All,
    [Switch]$Csv
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$localeDir = if ([IO.Path]::IsPathRooted($LocaleDir)) { $LocaleDir } else { Join-Path $root $LocaleDir }
if (!(Test-Path -LiteralPath $localeDir)) { throw "未找到语言目录：$localeDir" }

$pattern = '^\s*"((?:[^"\\]|\\.)*)"\s*:\s*"((?:[^"\\]|\\.)*)"\s*$'

function Unescape-Yaml([string]$text) {
    return [regex]::Replace($text, '\\(u[0-9a-fA-F]{4}|.)', {
        param($m)
        $v = $m.Groups[1].Value
        if ($v.Length -eq 5) { return [string][char][Convert]::ToInt32($v.Substring(1), 16) }
        switch -CaseSensitive ($v) {
            'n' { return "`n" }
            'r' { return "`r" }
            't' { return "`t" }
            '\\' { return '\' }
            '"' { return '"' }
            default { return $m.Value }
        }
    })
}

function Read-Locale([string]$path) {
    $entries = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $path -Encoding UTF8) {
        if ($line.Trim().Length -eq 0 -or $line.TrimStart().StartsWith("#")) { continue }
        $m = [regex]::Match($line, $pattern)
        if (!$m.Success) { continue }
        $entries[(Unescape-Yaml $m.Groups[1].Value)] = Unescape-Yaml $m.Groups[2].Value
    }
    return $entries
}

$baselinePath = Join-Path $localeDir $Baseline
if (!(Test-Path -LiteralPath $baselinePath)) { throw "未找到基准语言文件：$baselinePath" }
$baselineName = [IO.Path]::GetFileNameWithoutExtension($Baseline)

$files = @(Get-ChildItem $localeDir -Filter *.yml | Sort-Object Name)
$tables = [ordered]@{}
foreach ($file in $files) { $tables[$file.BaseName] = Read-Locale $file.FullName }

$reference = $tables[$baselineName]
$keys = if ($Key) { @($Key -split '\s*,\s*' | Where-Object { $_ }) } elseif ($All) { @($reference.Keys) } else { @($reference.Keys | Select-Object -First 10) }

# 未翻译条目：译值与英文原文完全相同（不含中文基准本身）
Write-Host "词表：$($files.Count) 种语言，基准 $($reference.Count) 条" -ForegroundColor Cyan
foreach ($name in $tables.Keys) {
    if ($name -eq $baselineName) { continue }
    $same = @($reference.Keys | Where-Object { $tables[$name].Contains($_) -and $tables[$name][$_] -ceq $_ })
    if ($same.Count -gt 0) {
        Write-Host "  $name：$($same.Count) 条与英文原文相同（可能是未翻译）" -ForegroundColor Yellow
        foreach ($item in $same) { Write-Host "    $item" -ForegroundColor Yellow }
    }
}

$rows = foreach ($item in $keys) {
    $row = [ordered]@{ Key = $item }
    foreach ($name in $tables.Keys) {
        $row[$name] = if ($tables[$name].Contains($item)) { $tables[$name][$item] } else { "❌ 缺少" }
    }
    [pscustomobject]$row
}

if ($Csv) {
    $rows | ConvertTo-Csv -NoTypeInformation
}
else {
    $rows | Format-Table -AutoSize -Wrap | Out-String -Width 4096 | Write-Host
}
