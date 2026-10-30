$ErrorActionPreference = 'Stop'
$version = '0.19.1'
$fileName = "restic_${version}_windows_amd64.zip"
$expectedHash = 'da948ad707ed690426473aaba2046cd61f8f90f6f0e7dab6be0d5796531de67d'
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolFolder = Join-Path $projectRoot '도구'
$downloadFolder = Join-Path ([IO.Path]::GetTempPath()) ('WorkOrganizerTool-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $downloadFolder -Force | Out-Null
New-Item -ItemType Directory -Path $toolFolder -Force | Out-Null
$archive = Join-Path $downloadFolder $fileName
Invoke-WebRequest -Uri "https://github.com/restic/restic/releases/download/v$version/$fileName" -OutFile $archive
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) { throw '다운로드 파일 검증 실패. 실행파일을 설치하지 않았습니다.' }
$expanded = Join-Path $downloadFolder 'expanded'
Expand-Archive -LiteralPath $archive -DestinationPath $expanded
$binary = Join-Path $expanded "restic_${version}_windows_amd64.exe"
if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) { throw '압축파일의 실행파일을 찾지 못했습니다.' }
$target = Join-Path $toolFolder 'restic.exe'
if (Test-Path -LiteralPath $target) {
    if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $binary).Hash) { throw '다른 버전의 restic.exe가 있습니다. 기존 파일을 보존하고 중단했습니다.' }
} else { Copy-Item -LiteralPath $binary -Destination $target }
Write-Host "백업 도구 준비 완료: $target"
Write-Host "다운로드 검사 자료: $downloadFolder"
