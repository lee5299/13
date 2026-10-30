param([string]$Dotnet = 'dotnet', [switch]$Publish)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $PSScriptRoot '.nuget-http'
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $PSScriptRoot '.nuget-plugins'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $Dotnet build (Join-Path $PSScriptRoot 'src\WorkOrganizer.App\WorkOrganizer.App.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw '프로그램 빌드 실패' }
$backupEngine = Join-Path $PSScriptRoot '도구\restic.exe'
if (Test-Path -LiteralPath $backupEngine) { & $Dotnet run --project (Join-Path $PSScriptRoot 'tests\WorkOrganizer.Checks\WorkOrganizer.Checks.csproj') -- $backupEngine }
else { & $Dotnet run --project (Join-Path $PSScriptRoot 'tests\WorkOrganizer.Checks\WorkOrganizer.Checks.csproj') }
if ($LASTEXITCODE -ne 0) { throw '핵심 동작 검사 실패' }
if ($Publish) {
    & $Dotnet publish (Join-Path $PSScriptRoot 'src\WorkOrganizer.App\WorkOrganizer.App.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $PSScriptRoot '실행') --nologo
    if ($LASTEXITCODE -ne 0) { throw '실행파일 생성 실패' }
    if (Test-Path -LiteralPath $backupEngine) {
        $publishedTools = Join-Path $PSScriptRoot '실행\도구'
        New-Item -ItemType Directory -Path $publishedTools -Force | Out-Null
        Copy-Item -LiteralPath $backupEngine -Destination (Join-Path $publishedTools 'restic.exe') -Force
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot '도구\restic-LICENSE.txt') -Destination (Join-Path $publishedTools 'restic-LICENSE.txt') -Force
    }
}
