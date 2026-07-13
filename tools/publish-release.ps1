param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$ProjectPath = "$PSScriptRoot\..\src\ArmZavuch\ArmZavuch.csproj",
    [string]$PublishDir = "$PSScriptRoot\..\releases\publish",
    [string]$OutputDir = "$PSScriptRoot\..\releases",
    [string]$PackId = "ArmZavuch",
    [string]$MainExe = "ArmZavuch.exe"
)

$ErrorActionPreference = "Stop"

# PowerShell 5.1 без BOM портит кириллицу в исходнике скрипта.
# Читаем отображаемое имя из UTF-8 исходника приложения.
$brandingPath = Join-Path $PSScriptRoot "..\src\ArmZavuch\AppBranding.cs"
$brandingText = [System.IO.File]::ReadAllText((Resolve-Path $brandingPath), [System.Text.UTF8Encoding]::new($false))
if ($brandingText -notmatch 'ProductName\s*=\s*"([^"]+)"') {
    throw "Ne udalos prochitat AppBranding.ProductName iz $brandingPath"
}
$PackTitle = $Matches[1]

Write-Host "Publishing $PackId $Version (title: $PackTitle)..."

if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

dotnet publish $ProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -p:InformationalVersion=$Version `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$vpkArgs = @(
    "pack",
    "--packId", $PackId,
    "--packVersion", $Version,
    "--packDir", $PublishDir,
    "--mainExe", $MainExe,
    "--packTitle", $PackTitle,
    "--outputDir", $OutputDir
)
& vpk @vpkArgs

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($env:GITHUB_TOKEN) {
    $owner = (Select-String -Path "$PSScriptRoot\..\src\ArmZavuch\Services\Update\UpdateChannelOptions.cs" -Pattern 'GitHubOwner = "([^"]+)"').Matches.Groups[1].Value
    $repo = (Select-String -Path "$PSScriptRoot\..\src\ArmZavuch\Services\Update\UpdateChannelOptions.cs" -Pattern 'GitHubRepo = "([^"]+)"').Matches.Groups[1].Value

    if ($owner -eq "REPLACE_ME") {
        Write-Warning "GitHubOwner ne nastroen - propuskaem vpk upload. Zagruzite releases vruchnuyu na GitHub."
        exit 0
    }

    vpk upload `
        --outputDir $OutputDir `
        --repoUrl "https://github.com/$owner/$repo" `
        --token $env:GITHUB_TOKEN `
        --publish "never"

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "Uploaded to https://github.com/$owner/$repo/releases"
}
else {
    Write-Host "GITHUB_TOKEN ne zadan - artefakty sobrany v $OutputDir"
}

Write-Host "Done."
