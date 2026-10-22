# README 多语言完整性校验：结构对齐、翻译说明、英文标题残留
#
# 用法：
#   pwsh -File Tools/Scripts/check-readmes.ps1
#   pwsh -File Tools/Scripts/check-readmes.ps1 -Root D:\src\MyLib
param(
    [String]$Root = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [String]$Config = "readme-languages.json"
)

$ErrorActionPreference = "Stop"

$configPath = Join-Path $Root $Config
if (!(Test-Path -LiteralPath $configPath)) { throw "未找到语言配置：$configPath" }

$languages = @((Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json).languages)
if ($languages.Count -eq 0) { throw "$Config 未配置任何语种" }

$baselinePath = Join-Path $Root "README.md"
if (!(Test-Path -LiteralPath $baselinePath)) { throw "未找到英文基准 README：$baselinePath" }

function Get-ReadmeStats([String]$path) {
    $lines = Get-Content -LiteralPath $path -Encoding UTF8
    $text = $lines -join "`n"
    return @{
        Headings = @($lines | Where-Object { $_ -match '^#{1,4} ' })
        Fences   = @($lines | Where-Object { $_ -match '^```' }).Count
        Links    = ([regex]::Matches($text, '\]\(')).Count
        Text     = $text
    }
}

$baseline = Get-ReadmeStats $baselinePath
$englishHeadings = @(
    'Features', 'Documentation', 'Quick Start', 'Architecture Philosophy',
    'Target Frameworks', 'Ecosystem Map', 'License', 'Team'
)
# 目标语言中与英文同形的合法标题，不按残留处理
$allowedSameWord = @{
    'de' = @('Team')
    'fr' = @('Documentation')
}
$errors = @()

foreach ($item in $languages) {
    $path = Join-Path $Root $item.file
    if (!(Test-Path -LiteralPath $path)) {
        $errors += "缺少文件：$($item.file)"
        continue
    }

    $stats = Get-ReadmeStats $path
    if ($stats.Headings.Count -ne $baseline.Headings.Count) {
        $errors += "$($item.file)：标题数 $($stats.Headings.Count)，基准 $($baseline.Headings.Count)"
    }
    if ($stats.Fences -ne $baseline.Fences) {
        $errors += "$($item.file)：代码围栏 $($stats.Fences)，基准 $($baseline.Fences)"
    }
    if ([Math]::Abs($stats.Links - $baseline.Links) -gt 1) {
        $errors += "$($item.file)：链接数 $($stats.Links)，基准 $($baseline.Links)（允许 ±1）"
    }

    if ($item.code -eq 'en') { continue }

    if ($stats.Text -notmatch '\]\(README\.md\)') {
        $errors += "$($item.file)：缺少指向 README.md 的翻译说明"
    }
    foreach ($heading in $stats.Headings) {
        $title = ($heading -replace '^#+\s*', '').Trim()
        if ($englishHeadings -contains $title -and -not ($allowedSameWord[$item.code] -contains $title)) {
            $errors += "$($item.file)：存在未翻译标题「$title」"
        }
    }
}

if ($errors.Count -gt 0) {
    foreach ($e in $errors) { Write-Host $e -ForegroundColor Red }
    Write-Host "README 多语言校验未通过：$($errors.Count) 个问题" -ForegroundColor Red
    exit 1
}

Write-Host "README 多语言校验通过：$($languages.Count) 个语种，标题与代码围栏结构一致" -ForegroundColor Green
