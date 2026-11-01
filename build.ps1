param([string]$Dotnet = 'dotnet', [switch]$Publish, [switch]$Package, [string]$Version = '0.1.0')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $PSScriptRoot '.nuget-http'
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $PSScriptRoot '.nuget-plugins'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if ($Package) { $Publish = $true }
& $Dotnet build (Join-Path $PSScriptRoot 'src\WorkOrganizer.App\WorkOrganizer.App.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw '프로그램 빌드 실패' }
$backupEngine = Join-Path $PSScriptRoot '도구\restic.exe'
if (Test-Path -LiteralPath $backupEngine) { & $Dotnet run --project (Join-Path $PSScriptRoot 'tests\WorkOrganizer.Checks\WorkOrganizer.Checks.csproj') -- $backupEngine }
else { & $Dotnet run --project (Join-Path $PSScriptRoot 'tests\WorkOrganizer.Checks\WorkOrganizer.Checks.csproj') }
if ($LASTEXITCODE -ne 0) { throw '핵심 동작 검사 실패' }
$publishDir = Join-Path $PSScriptRoot '실행'
if ($Publish) {
    # 배포본에는 이전 실행에서 생긴 기기별 설정·시험자료가 섞이지 않도록 빌드 출력 폴더를 비우고 다시 만든다.
    if ($Package -and (Test-Path -LiteralPath $publishDir)) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
    & $Dotnet publish (Join-Path $PSScriptRoot 'src\WorkOrganizer.App\WorkOrganizer.App.csproj') -c Release -r win-x64 --self-contained true -o $publishDir -p:Version=$Version --nologo
    if ($LASTEXITCODE -ne 0) { throw '실행파일 생성 실패' }
    if (Test-Path -LiteralPath $backupEngine) {
        $publishedTools = Join-Path $publishDir '도구'
        New-Item -ItemType Directory -Path $publishedTools -Force | Out-Null
        Copy-Item -LiteralPath $backupEngine -Destination (Join-Path $publishedTools 'restic.exe') -Force
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot '도구\restic-LICENSE.txt') -Destination (Join-Path $publishedTools 'restic-LICENSE.txt') -Force
    } elseif ($Package) { Write-Warning '도구\restic.exe 가 없어 배포본에 백업 도구가 빠집니다. 먼저 .\scripts\백업도구준비.ps1 을 실행하세요.' }
}
if ($Package) {
    $outDir = Join-Path $PSScriptRoot '배포'
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    # 설치 없이 실행해 보는 zip
    $zip = Join-Path $outDir "업무정리_실행판_$Version.zip"
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Get-ChildItem -LiteralPath $publishDir -Recurse -Filter '*.pdb' | Remove-Item -Force
    # Windows PowerShell 5.1의 Compress-Archive는 한글 파일명이 깨질 수 있어 UTF-8 이름으로 직접 압축한다.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false, [System.Text.Encoding]::UTF8)
    Write-Host "zip 생성: $zip"
    # 셋업 파일 (Inno Setup 6 필요)
    $iscc = @((Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue).Source, "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
    if (-not $iscc) { throw 'Inno Setup 6을 찾지 못해 셋업 파일을 만들지 않았습니다(zip은 만들었습니다). 설치: winget install JRSoftware.InnoSetup' }
    & $iscc "/DAppVersion=$Version" "/DSourceDir=$publishDir" "/DOutputDir=$outDir" (Join-Path $PSScriptRoot 'installer\업무정리.iss')
    if ($LASTEXITCODE -ne 0) { throw '셋업 파일 생성 실패' }
    Write-Host "셋업 파일 생성: $(Join-Path $outDir "업무정리_설치_$Version.exe")"
}
