param(
    [Parameter(Mandatory = $true)]
    [string] $Ymm4DirPath
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'SequenceTIFFPlugin.csproj'
[xml]$projectXml = Get-Content -LiteralPath $projectFile -Raw
[string]$version = @($projectXml.Project.PropertyGroup.Version)[0]
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'プロジェクトのバージョンを取得できません。'
}

$outputDirectory = Join-Path $projectRoot 'bin\Release\net10.0-windows10.0.19041.0'
$distributionDirectory = Join-Path $projectRoot 'dist'
$stagingDirectory = Join-Path $distributionDirectory 'staging'
$pluginDirectory = Join-Path $stagingDirectory 'SequenceTIFFPlugin'
$zipPath = Join-Path $distributionDirectory 'SequenceTIFFPlugin.zip'
$packagePath = Join-Path $distributionDirectory "SequenceTIFFPlugin-v$version.ymme"

dotnet build $projectFile -c Release -p:YMM4DirPath="$Ymm4DirPath"
if ($LASTEXITCODE -ne 0) {
    throw 'プラグインのビルドに失敗しました。'
}

if (Test-Path -LiteralPath $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $outputDirectory 'SequenceTIFFPlugin.dll') -Destination $pluginDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $pluginDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $pluginDirectory

$cultures = @('ar-SA', 'en-US', 'es-ES', 'ja-JP', 'ko-KR', 'zh-CN', 'zh-TW', 'id-ID')
foreach ($culture in $cultures) {
    $sourceCultureDirectory = Join-Path $outputDirectory $culture
    if (-not (Test-Path -LiteralPath $sourceCultureDirectory)) {
        throw "翻訳リソースが見つかりません: $sourceCultureDirectory"
    }

    Copy-Item -LiteralPath $sourceCultureDirectory -Destination $pluginDirectory -Recurse
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

Compress-Archive -LiteralPath $pluginDirectory -DestinationPath $zipPath -CompressionLevel Optimal
Move-Item -LiteralPath $zipPath -Destination $packagePath
Remove-Item -LiteralPath $stagingDirectory -Recurse -Force

Write-Host "作成しました: $packagePath"
