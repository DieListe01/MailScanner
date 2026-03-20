$ErrorActionPreference = "Stop"
if (Get-Variable PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$repoPath = "\\FILMESERVER\Homeoffice\_Dirk\Projekte\MailScanner"
$appProject = "src/MailScanner.App/MailScanner.App.csproj"
$propsPath = Join-Path $repoPath "Directory.Build.props"
$buildConfig = "Release"
$logPath = Join-Path $repoPath "mailscanner-script.log"
$scriptFile = if ($PSCommandPath) { $PSCommandPath } elseif ($MyInvocation.MyCommand.Path) { $MyInvocation.MyCommand.Path } else { "<interaktive Session>" }
$scriptName = [System.IO.Path]::GetFileName($scriptFile)

function Write-Log {
    param([string]$Text)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Add-Content -Path $logPath -Value "[$timestamp] $Text"
}

function Write-Step($text) {
    Write-Host ""
    Write-Host "== $text ==" -ForegroundColor Cyan
    Write-Log "STEP: $text"
}

function Write-Ok($text) {
    Write-Host $text -ForegroundColor Green
    Write-Log "OK: $text"
}

function Write-Warn($text) {
    Write-Host $text -ForegroundColor Yellow
    Write-Log "WARN: $text"
}

function Write-Info($text) {
    Write-Host $text -ForegroundColor DarkGray
    Write-Log "INFO: $text"
}

function Write-Headline($text) {
    Write-Host $text -ForegroundColor Cyan
    Write-Log "HEADLINE: $text"
}

function Write-ErrorText($text) {
    Write-Host $text -ForegroundColor Red
    Write-Log "ERROR_TEXT: $text"
}

function Pause-Menu {
    Write-Host ""
    Read-Host "Weiter mit Enter"
}

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Befehl nicht gefunden: $Name"
    }
}

function Assert-FunctionAvailable {
    param([string]$Name)

    $command = Get-Command -Name $Name -CommandType Function -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Funktion '$Name' ist nicht geladen. Wahrscheinlich wurde die falsche Datei gestartet, nur ein Teil des Skripts ausgefuehrt oder noch eine alte PowerShell-Session verwendet. Gestartete Datei: $scriptFile"
    }
}

function Test-ScriptContext {
    Assert-FunctionAvailable "ShowHeader"
    Assert-FunctionAvailable "ShowMenu"
}

function Write-StartupInfo {
    Write-Info "Skriptdatei: $scriptFile"
    Write-Info "PowerShell: $($PSVersionTable.PSVersion)"
}

function Set-RepoLocation {
    Set-Location $repoPath
}

function Get-AppVersion {
    if (-not (Test-Path $propsPath)) { return "unbekannt" }
    $content = Get-Content $propsPath -Raw
    $match = [regex]::Match($content, "<Version>(.*?)</Version>")
    if ($match.Success) { return $match.Groups[1].Value }
    "unbekannt"
}

function Assert-VersionFormat {
    param([string]$Value)
    if ($Value -notmatch '^\d+\.\d+\.\d+$') {
        throw "Version muss im Format x.y.z sein, z.B. 0.3.3"
    }
}

function Parse-Version {
    param([string]$Version)
    Assert-VersionFormat $Version
    $parts = $Version.Split(".")
    [PSCustomObject]@{
        Major = [int]$parts[0]
        Minor = [int]$parts[1]
        Patch = [int]$parts[2]
    }
}

function Get-NextPatchVersion {
    $v = Parse-Version (Get-AppVersion)
    "$($v.Major).$($v.Minor).$($v.Patch + 1)"
}

function Get-NextMinorVersion {
    $v = Parse-Version (Get-AppVersion)
    "$($v.Major).$($v.Minor + 1).0"
}

function Get-NextMajorVersion {
    $v = Parse-Version (Get-AppVersion)
    "$($v.Major + 1).0.0"
}

function Compare-VersionValues {
    param(
        [string]$Left,
        [string]$Right
    )

    $leftVersion = Parse-Version $Left
    $rightVersion = Parse-Version $Right

    foreach ($part in @("Major", "Minor", "Patch")) {
        if ($leftVersion.$part -gt $rightVersion.$part) { return 1 }
        if ($leftVersion.$part -lt $rightVersion.$part) { return -1 }
    }

    return 0
}

function Get-VersionState {
    param(
        [string]$LocalVersion,
        [string]$LatestTag
    )

    if ([string]::IsNullOrWhiteSpace($LocalVersion) -or $LocalVersion -eq "unbekannt") {
        return [PSCustomObject]@{
            Text  = "LOKALE VERSION UNBEKANNT"
            Color = "Red"
        }
    }

    if ([string]::IsNullOrWhiteSpace($LatestTag) -or $LatestTag -in @("keins", "unbekannt")) {
        return [PSCustomObject]@{
            Text  = "GITHUB-RELEASE UNBEKANNT"
            Color = "Yellow"
        }
    }

    $githubVersion = $LatestTag.TrimStart("v")
    $compare = Compare-VersionValues -Left $LocalVersion -Right $githubVersion

    if ($compare -gt 0) {
        return [PSCustomObject]@{
            Text  = "RELEASE AUSSTEHEND"
            Color = "Yellow"
        }
    }

    if ($compare -lt 0) {
        return [PSCustomObject]@{
            Text  = "GITHUB IST NEUER"
            Color = "Red"
        }
    }

    return [PSCustomObject]@{
        Text  = "SYNC / AKTUELL"
        Color = "Green"
    }
}

function Choose-VersionBump {
    $current = Get-AppVersion
    $patch = Get-NextPatchVersion
    $minor = Get-NextMinorVersion
    $major = Get-NextMajorVersion

    Write-Host ""
    Write-Host "Version erhoehen" -ForegroundColor Cyan
    Write-Host "Aktuell : $current"
    Write-Host "1 - Patch : $patch"
    Write-Host "2 - Minor : $minor"
    Write-Host "3 - Major : $major"
    Write-Host "4 - Custom"
    Write-Host ""

    $choice = Read-Host "Auswahl"
    switch ($choice) {
        "1" { return $patch }
        "2" { return $minor }
        "3" { return $major }
        "4" {
            $custom = Read-Host "Version eingeben"
            Assert-VersionFormat $custom
            return $custom
        }
        default { throw "Ungueltige Versionsauswahl." }
    }
}

function Stop-Processes {
    Write-Step "Prozesse beenden"
    $processes = @("MailScanner", "devenv", "dotnet")
    foreach ($name in $processes) {
        $running = Get-Process -Name $name -ErrorAction SilentlyContinue
        if ($running) {
            $running | Stop-Process -Force
            Write-Ok "Beendet: $name"
        } else {
            Write-Info "Nicht aktiv: $name"
        }
    }
    Start-Sleep -Seconds 2
}

function Remove-BuildFolders {
    Write-Step "Build-Ordner loeschen"
    $folders = @("src/MailScanner.App/bin","src/MailScanner.App/obj")
    foreach ($folder in $folders) {
        if (Test-Path $folder) {
            Remove-Item -Recurse -Force $folder
            Write-Ok "Geloescht: $folder"
        } else {
            Write-Info "Nicht vorhanden: $folder"
        }
    }
}

function Cleanup-PublishFolders {
    Write-Step "Alte Publish-Ordner aufraeumen"
    $folders = Get-ChildItem -Path $repoPath -Directory -Filter "publish-*"
    if (-not $folders) {
        Write-Info "Keine publish-Ordner gefunden."
        return
    }

    foreach ($folder in $folders) {
        Remove-Item -Recurse -Force $folder.FullName
        Write-Ok "Geloescht: $($folder.Name)"
    }
}

function Git-Pull {
    Write-Step "Git Pull"
    git pull
    if ($LASTEXITCODE -ne 0) { throw "git pull fehlgeschlagen." }
    Write-Ok "Repository aktualisiert"
}

function Update-VersionInProps {
    param([string]$NewVersion)

    Assert-VersionFormat $NewVersion
    $assemblyVersion = "$NewVersion.0"
    $content = Get-Content $propsPath -Raw

    $content = [regex]::Replace($content, '<Version>.*?</Version>', "<Version>$NewVersion</Version>")
    $content = [regex]::Replace($content, '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$assemblyVersion</AssemblyVersion>")
    $content = [regex]::Replace($content, '<FileVersion>.*?</FileVersion>', "<FileVersion>$assemblyVersion</FileVersion>")
    $content = [regex]::Replace($content, '<InformationalVersion>.*?</InformationalVersion>', "<InformationalVersion>$NewVersion</InformationalVersion>")

    Set-Content -Path $propsPath -Value $content -NoNewline
    Write-Ok "Version gesetzt auf $NewVersion"
}

function Build-App {
    Write-Step "Build"
    dotnet build $appProject -c $buildConfig
    if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen." }
    Write-Ok "Build erfolgreich"
}

function Run-App {
    Write-Step "App starten"
    dotnet run --project $appProject -c $buildConfig
}

function Publish-App {
    param([string]$Version)

    $publishFolder = "publish-$Version"
    Write-Step "Publish"
    dotnet publish $appProject -c $buildConfig -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version -o $publishFolder
    if ($LASTEXITCODE -ne 0) { throw "Publish fehlgeschlagen." }

    $fullPath = Join-Path $repoPath $publishFolder
    Write-Ok "Publish erfolgreich: $fullPath"
    $fullPath
}

function Open-Folder {
    param([string]$Path)
    if (Test-Path $Path) {
        Start-Process explorer.exe $Path
        Write-Ok "Explorer geoeffnet: $Path"
    } else {
        Write-Warn "Ordner nicht gefunden: $Path"
    }
}

function Open-Url {
    param([string]$Url)

    if ([string]::IsNullOrWhiteSpace($Url)) {
        Write-Warn "Keine URL vorhanden."
        return
    }

    Start-Process $Url
    Write-Ok "Im Browser geoeffnet: $Url"
}

function Write-MenuEntry {
    param(
        [string]$Number,
        [string]$Title,
        [string]$Hint
    )

    $left = " {0,2}   {1}" -f $Number, $Title
    Write-Host ("{0,-48}" -f $left) -NoNewline -ForegroundColor White
    Write-Host (" - {0}" -f $Hint) -ForegroundColor DarkGray
}

function Git-Status {
    Write-Step "Git Status"
    git status --short
}

function Remove-FromIndexIfPresent {
    param([string]$PathSpec)

    git rm --cached --ignore-unmatch --quiet -- $PathSpec 2>$null | Out-Null
}

function Git-CommitAndPush {
    param([string]$Message)

    Write-Step "Commit und Push"
    $statusOutput = git status --short
    $statusOutput

    if (-not $statusOutput) {
        Write-Warn "Keine Aenderungen zum Commit vorhanden."
        return
    }

    git add .
    Remove-FromIndexIfPresent "publish-*"
    Remove-FromIndexIfPresent "src/MailScanner.App.zip"
    Remove-FromIndexIfPresent "src/MailScanner.App/README.txt"
    Remove-FromIndexIfPresent "Textdokument (neu).txt"
    Remove-FromIndexIfPresent "mailscanner-menu-v3.ps1"
    Remove-FromIndexIfPresent "mailscanner-menu.ps1"
    Remove-FromIndexIfPresent "rebuild-mailscanner.ps1"

    git commit -m $Message
    if ($LASTEXITCODE -ne 0) { throw "Commit fehlgeschlagen." }

    git push origin main
    if ($LASTEXITCODE -ne 0) { throw "Push fehlgeschlagen." }

    Write-Ok "Push erfolgreich"
}

function Create-GitHubRelease {
    param([string]$Version)

    $tag = "v$Version"

    Write-Step "GitHub Auth"
    gh auth status

    Write-Step "Release erstellen"
    gh release create $tag --target main --generate-notes --title "MailScanner $Version"
    if ($LASTEXITCODE -ne 0) { throw "Release-Erstellung fehlgeschlagen." }

    Write-Ok "Release erstellt: $tag"
}

function Start-ReleaseWorkflow {
    param([string]$Version)

    Write-Step "GitHub Release-Workflow starten"
    gh workflow run release.yml -f version=$Version
    if ($LASTEXITCODE -ne 0) { throw "Release-Workflow konnte nicht gestartet werden." }

    Write-Ok "Workflow gestartet fuer Version $Version"
}

function Watch-LatestReleaseWorkflow {
    Write-Step "Auf neuesten Release-Workflow warten"
    Start-Sleep -Seconds 5

    $run = gh run list --workflow release.yml --limit 1 --json databaseId,displayTitle,status,conclusion | ConvertFrom-Json | Select-Object -First 1
    if (-not $run -or -not $run.databaseId) {
        throw "Kein aktueller Release-Workflow gefunden."
    }

    Write-Info "Workflow: $($run.displayTitle)"
    gh run watch $run.databaseId
    if ($LASTEXITCODE -ne 0) { throw "Workflow-Beobachtung fehlgeschlagen." }

    $final = gh run view $run.databaseId --json status,conclusion | ConvertFrom-Json
    if ($final.status -ne "completed" -or $final.conclusion -ne "success") {
        throw "Workflow fehlgeschlagen: $($final.conclusion)"
    }

    Write-Ok "Workflow erfolgreich abgeschlossen"
}

function Watch-WorkflowWithProgress {
    param([string]$Version)

    $tag = "v$Version"
    Write-Step "Workflow suchen"
    Start-Sleep -Seconds 3

    $runLines = gh run list --workflow release.yml --limit 10
    $runLine = $runLines | Select-String $tag | Select-Object -First 1
    if (-not $runLine) { throw "Kein passender Workflow fuer $tag gefunden." }

    $runText = $runLine.ToString()
    $parts = $runText -split '\s+'
    $runId = $parts[-3]
    if (-not $runId) { throw "Run-ID konnte nicht ermittelt werden." }

    Write-Ok "Workflow-ID: $runId"
    Write-Step "Release-Workflow laeuft"

    $progress = 5
    while ($true) {
        $json = gh run view $runId --json status,conclusion | ConvertFrom-Json
        if ($json.status -eq "completed") {
            Write-Progress -Activity "GitHub Release Upload" -Status "Abgeschlossen" -PercentComplete 100
            if ($json.conclusion -ne "success") {
                throw "Workflow fehlgeschlagen: $($json.conclusion)"
            }
            break
        }

        Write-Progress -Activity "GitHub Release Upload" -Status "Packaging / Upload laeuft..." -PercentComplete $progress
        $progress = [Math]::Min(95, $progress + 5)
        Start-Sleep -Seconds 4
    }

    Write-Progress -Activity "GitHub Release Upload" -Completed
    Write-Ok "Workflow erfolgreich abgeschlossen"
}

function Get-ReleaseData {
    param([string]$Version)
    $tag = "v$Version"
    gh release view $tag --json url,assets | ConvertFrom-Json
}

function Get-ReleaseInfoObject {
    param([string]$Version)

    $releaseJson = Get-ReleaseData $Version
    $installer = $releaseJson.assets | Where-Object { $_.name -like "*.exe" } | Select-Object -First 1

    $installerUrl = $null
    $installerName = $null
    if ($installer) {
        $installerUrl = $installer.url
        $installerName = $installer.name
    }

    [PSCustomObject]@{
        Version       = $Version
        Tag           = "v$Version"
        ReleaseUrl    = $releaseJson.url
        InstallerUrl  = $installerUrl
        InstallerName = $installerName
        HasInstaller  = [bool]$installerUrl
    }
}

function Get-LatestGitHubReleaseTag {
    try {
        $json = gh release list --limit 1 --json tagName | ConvertFrom-Json
        if ($json -and $json[0].tagName) {
            return $json[0].tagName
        }
        return "keins"
    }
    catch {
        return "unbekannt"
    }
}

function Get-LatestGitHubReleaseVersion {
    $tag = Get-LatestGitHubReleaseTag
    if ([string]::IsNullOrWhiteSpace($tag) -or $tag -in @("keins", "unbekannt")) {
        return $tag
    }

    return $tag.TrimStart("v")
}

function Get-OriginMainProjectVersion {
    try {
        git fetch origin main --quiet 2>$null | Out-Null

        $remoteContent = git show "origin/main:Directory.Build.props" 2>$null
        if (-not $remoteContent) {
            return "unbekannt"
        }

        $remoteText = ($remoteContent -join "`n")
        $match = [regex]::Match($remoteText, "<Version>(.*?)</Version>")
        if ($match.Success) {
            return $match.Groups[1].Value
        }

        return "unbekannt"
    }
    catch {
        return "unbekannt"
    }
}

function Get-StatusLine {
    param(
        [string]$Text,
        [string]$Color
    )

    [PSCustomObject]@{
        Text  = $Text
        Color = $Color
    }
}

function Get-RepoSyncState {
    param(
        [string]$LocalVersion,
        [string]$OriginVersion
    )

    if ([string]::IsNullOrWhiteSpace($LocalVersion) -or $LocalVersion -eq "unbekannt") {
        return Get-StatusLine -Text "LOKALE VERSION UNBEKANNT" -Color "Red"
    }

    if ([string]::IsNullOrWhiteSpace($OriginVersion) -or $OriginVersion -eq "unbekannt") {
        return Get-StatusLine -Text "ORIGIN/MAIN UNBEKANNT" -Color "Yellow"
    }

    $compare = Compare-VersionValues -Left $LocalVersion -Right $OriginVersion

    if ($compare -lt 0) {
        return Get-StatusLine -Text "PULL NOETIG - ORIGIN/MAIN IST NEUER" -Color "Red"
    }

    if ($compare -gt 0) {
        return Get-StatusLine -Text "LOKALER STAND IST NEUER ALS ORIGIN/MAIN" -Color "Yellow"
    }

    return Get-StatusLine -Text "LOKAL = ORIGIN/MAIN" -Color "Green"
}

function Get-ReleaseSyncState {
    param(
        [string]$ProjectVersion,
        [string]$ReleaseVersion
    )

    if ([string]::IsNullOrWhiteSpace($ReleaseVersion) -or $ReleaseVersion -in @("keins", "unbekannt")) {
        return Get-StatusLine -Text "GITHUB-RELEASE UNBEKANNT" -Color "Yellow"
    }

    if ([string]::IsNullOrWhiteSpace($ProjectVersion) -or $ProjectVersion -eq "unbekannt") {
        return Get-StatusLine -Text "PROJEKTVERSION UNBEKANNT" -Color "Red"
    }

    $compare = Compare-VersionValues -Left $ProjectVersion -Right $ReleaseVersion

    if ($compare -lt 0) {
        return Get-StatusLine -Text "RELEASE IST NEUER ALS DIE PROJEKTVERSION" -Color "Red"
    }

    if ($compare -gt 0) {
        return Get-StatusLine -Text "PROJEKTVERSION IST NEUER - RELEASE AUSSTEHEND" -Color "Yellow"
    }

    return Get-StatusLine -Text "PROJEKTVERSION = GITHUB-RELEASE" -Color "Green"
}

function New-ReleasePostText {
    param(
        [string]$Version,
        [string]$ReleaseUrl,
        [string]$InstallerUrl
    )

    if ([string]::IsNullOrWhiteSpace($InstallerUrl)) {
@"
Neue MailScanner-Version verfuegbar: $Version
Release:
$ReleaseUrl
"@
    }
    else {
@"
Neue MailScanner-Version verfuegbar: $Version
Installer:
$InstallerUrl

Release:
$ReleaseUrl
"@
    }
}

function Save-ReleasePostText {
    param([string]$Text)

    $targetPath = Join-Path $repoPath "last-release-result.txt"
    Set-Content -Path $targetPath -Value $Text -Encoding UTF8
    Write-Info "Ergebnistext gespeichert: $targetPath"
}

function Show-ReleaseResultBox {
    param([pscustomobject]$Info)

    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║                    RELEASE ERFOLGREICH                      ║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host (" Version   : {0}" -f $Info.Version) -ForegroundColor Magenta
    Write-Host (" GitHubTag : {0}" -f $Info.Tag) -ForegroundColor Cyan
    Write-Host (" Release   : {0}" -f $Info.ReleaseUrl) -ForegroundColor Yellow

    if ($Info.HasInstaller) {
        Write-Host (" Installer : {0}" -f $Info.InstallerUrl) -ForegroundColor Green
    }
    else {
        Write-Warn "Kein Installer gefunden."
    }

    $postText = New-ReleasePostText -Version $Info.Version -ReleaseUrl $Info.ReleaseUrl -InstallerUrl $Info.InstallerUrl
    Save-ReleasePostText -Text $postText

    Write-Host ""
    Write-Info "Diese Links kannst du direkt fuer das neue GitHub-Release verwenden."
    Write-Host ""
    Write-Headline "Post-Text"
    Write-Host $postText -ForegroundColor White
    Write-Host ""

    try {
        Set-Clipboard $postText
        Write-Ok "Post-Text in Zwischenablage kopiert"
    }
    catch {
        Write-Warn "Zwischenablage konnte nicht gesetzt werden: $($_.Exception.Message)"
    }
}

function Show-ReleaseInfo {
    param([string]$Version)

    Write-Step "Release-Infos"
    $info = Get-ReleaseInfoObject $Version

    Write-Host "Release:" -ForegroundColor Cyan
    Write-Host "  $($info.ReleaseUrl)" -ForegroundColor Yellow

    Write-Host "Installer:" -ForegroundColor Cyan
    if ($info.HasInstaller) {
        Write-Host "  $($info.InstallerUrl)" -ForegroundColor Green
    } else {
        Write-ErrorText "  Kein Installer gefunden."
    }

    Write-Host ""
    Write-Headline "Post-Text"
    Write-Host (New-ReleasePostText -Version $info.Version -ReleaseUrl $info.ReleaseUrl -InstallerUrl $info.InstallerUrl) -ForegroundColor White
}

function Copy-ReleaseLink {
    param([string]$Version)
    $info = Get-ReleaseInfoObject $Version
    Set-Clipboard $info.ReleaseUrl
    Write-Ok "Release-Link in Zwischenablage kopiert"
}

function Copy-InstallerLink {
    param([string]$Version)
    $info = Get-ReleaseInfoObject $Version

    if (-not $info.HasInstaller) {
        throw "Kein Installer-Link gefunden."
    }

    Set-Clipboard $info.InstallerUrl
    Write-Ok "Installer-Link in Zwischenablage kopiert"
}

function Copy-ReleasePostText {
    param([string]$Version)

    $info = Get-ReleaseInfoObject $Version
    $postText = New-ReleasePostText -Version $info.Version -ReleaseUrl $info.ReleaseUrl -InstallerUrl $info.InstallerUrl
    Set-Clipboard $postText
    Save-ReleasePostText -Text $postText
    Write-Ok "Ergebnistext in Zwischenablage kopiert"
}

function Show-LatestInstallerLink {
    $tag = Get-LatestGitHubReleaseTag
    if ($tag -eq "keins" -or $tag -eq "unbekannt") {
        Write-Warn "Kein letztes Release gefunden."
        return
    }

    $version = $tag.TrimStart("v")
    Show-ReleaseInfo $version
}

function Copy-LatestInstallerLink {
    $tag = Get-LatestGitHubReleaseTag
    if ($tag -eq "keins" -or $tag -eq "unbekannt") {
        throw "Kein letztes Release gefunden."
    }

    $version = $tag.TrimStart("v")
    Copy-InstallerLink $version
}

function Copy-LatestReleasePostText {
    $tag = Get-LatestGitHubReleaseTag
    if ($tag -eq "keins" -or $tag -eq "unbekannt") {
        throw "Kein letztes Release gefunden."
    }

    $version = $tag.TrimStart("v")
    Copy-ReleasePostText $version
}

function Bump-VersionAndPushOnly {
    $version = Choose-VersionBump
    Update-VersionInProps $version
    Git-CommitAndPush "Bump version to $version"
}

function Full-RebuildRun {
    Stop-Processes
    Git-Pull
    Remove-BuildFolders
    Build-App
    Run-App
}

function Full-Release {
    $version = Choose-VersionBump

    Stop-Processes
    Git-Pull
    Remove-BuildFolders
    Update-VersionInProps $version
    Build-App
    $publishPath = Publish-App $version
    Git-CommitAndPush "Release $version"
    Create-GitHubRelease $version
    Watch-WorkflowWithProgress $version
    $info = Get-ReleaseInfoObject $version
    Show-ReleaseResultBox $info

    $openRelease = Read-Host "Release-Seite im Browser oeffnen? (j/n)"
    if ($openRelease -match '^(j|ja|y|yes)$') {
        Open-Url $info.ReleaseUrl
    }

    if ($info.HasInstaller) {
        $openInstaller = Read-Host "Installer-Link im Browser oeffnen? (j/n)"
        if ($openInstaller -match '^(j|ja|y|yes)$') {
            Open-Url $info.InstallerUrl
        }
    }

    Write-Host ""
    Write-Headline "Lokaler Publish-Ordner"
    Write-Host $publishPath -ForegroundColor Green

    $open = Read-Host "Publish-Ordner im Explorer oeffnen? (j/n)"
    if ($open -match '^(j|ja|y|yes)$') {
        Open-Folder $publishPath
    }
}

function Next-VersionReleaseToGitHub {
    $version = Choose-VersionBump

    Stop-Processes
    Git-Pull
    Remove-BuildFolders
    Update-VersionInProps $version
    Build-App
    Git-CommitAndPush "Prepare $version release"
    Start-ReleaseWorkflow $version
    Watch-LatestReleaseWorkflow
    $info = Get-ReleaseInfoObject $version
    Show-ReleaseResultBox $info

    $openRelease = Read-Host "Release-Seite im Browser oeffnen? (j/n)"
    if ($openRelease -match '^(j|ja|y|yes)$') {
        Open-Url $info.ReleaseUrl
    }

    if ($info.HasInstaller) {
        $openInstaller = Read-Host "Installer-Link im Browser oeffnen? (j/n)"
        if ($openInstaller -match '^(j|ja|y|yes)$') {
            Open-Url $info.InstallerUrl
        }
    }
}

function ShowHeader {
    $version = Get-AppVersion
    $originMainVersion = Get-OriginMainProjectVersion
    $latestTag = Get-LatestGitHubReleaseTag
    $latestGitHubVersion = Get-LatestGitHubReleaseVersion
    $repoState = Get-RepoSyncState -LocalVersion $version -OriginVersion $originMainVersion
    $releaseState = Get-ReleaseSyncState -ProjectVersion $version -ReleaseVersion $latestGitHubVersion

    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                 MailScanner Luxus-Menue                     ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

    Write-Host " Projektversion lokal : " -NoNewline -ForegroundColor White
    Write-Host $version -ForegroundColor Magenta

    Write-Host " Origin/Main Version  : " -NoNewline -ForegroundColor White
    if ($originMainVersion -eq "unbekannt") {
        Write-Host $originMainVersion -ForegroundColor Yellow
    }
    else {
        Write-Host $originMainVersion -ForegroundColor Cyan
    }

    Write-Host " GitHub Release Tag   : " -NoNewline -ForegroundColor White
    if ($latestTag -in @("keins", "unbekannt")) {
        Write-Host $latestTag -ForegroundColor Yellow
    }
    else {
        Write-Host $latestTag -ForegroundColor Green
    }

    Write-Host " GitHub Release Vers. : " -NoNewline -ForegroundColor White
    if ($latestGitHubVersion -in @("keins", "unbekannt")) {
        Write-Host $latestGitHubVersion -ForegroundColor Yellow
    }
    else {
        Write-Host $latestGitHubVersion -ForegroundColor Green
    }

    Write-Host " Repo-Status          : " -NoNewline -ForegroundColor White
    Write-Host $repoState.Text -ForegroundColor $repoState.Color

    Write-Host " Release-Status       : " -NoNewline -ForegroundColor White
    Write-Host $releaseState.Text -ForegroundColor $releaseState.Color

    Write-Host " Pfad                 : $repoPath" -ForegroundColor DarkGray
    Write-Host " Skript               : $scriptName" -ForegroundColor DarkGray
    Write-Host " Skriptdatei          : $scriptFile" -ForegroundColor DarkGray
    Write-Host " Log                  : $logPath" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host " Schnellweg:" -NoNewline -ForegroundColor White
    Write-Host " Fuer ein neues GitHub-Release inkl. Installer meistens " -NoNewline -ForegroundColor DarkGray
    Write-Host "25" -NoNewline -ForegroundColor Yellow
    Write-Host " waehlen." -ForegroundColor DarkGray
    Write-Host " Danach Patch / Minor / Major auswaehlen. Am Ende werden Release-Link und Installer-Link angezeigt." -ForegroundColor DarkGray
    Write-Host " Hinweis:" -NoNewline -ForegroundColor White
    Write-Host " Wenn GitHub neuer ist als lokal, zuerst " -NoNewline -ForegroundColor DarkGray
    Write-Host "1 = Git Pull" -NoNewline -ForegroundColor Yellow
    Write-Host " ausfuehren." -ForegroundColor DarkGray
    Write-Host ""
}

function ShowMenu {
    Assert-FunctionAvailable "ShowHeader"

    Clear-Host
    ShowHeader

    Write-Host "[Entwicklung]" -ForegroundColor Cyan
    Write-MenuEntry "1"  "Git Pull"                                  "holt die neuesten Aenderungen aus GitHub"
    Write-MenuEntry "2"  "Prozesse beenden"                          "schliesst MailScanner / dotnet / Visual Studio"
    Write-MenuEntry "3"  "bin/obj loeschen"                          "raeumt lokale Build-Reste weg"
    Write-MenuEntry "4"  "Build"                                     "kompiliert die App lokal"
    Write-MenuEntry "5"  "App starten"                               "startet MailScanner lokal"
    Write-MenuEntry "6"  "Rebuild + Start"                           "zieht neu, loescht Build-Reste, baut und startet"
    Write-Host ""

    Write-Host "[Release]" -ForegroundColor Cyan
    Write-MenuEntry "25" "Naechste Version + GitHub Release"         "erhoeht die Version, committet, pusht und startet release.yml"
    Write-MenuEntry "7"  "Nur Publish/Installer lokal bauen"         "erstellt nur lokal den Installer, ohne GitHub"
    Write-MenuEntry "8"  "Version setzen"                            "setzt die lokale Version manuell, z.B. 0.3.5"
    Write-MenuEntry "9"  "Git Commit + Push"                         "pusht lokale Aenderungen nach GitHub"
    Write-MenuEntry "10" "GitHub Release erstellen"                  "legt nur das Release / Tag in GitHub an"
    Write-MenuEntry "11" "Auf Release-Workflow warten"               "wartet auf den GitHub-Workflow fuer eine Version"
    Write-MenuEntry "12" "Release-Infos + Post-Text anzeigen"        "zeigt Release-Link, Installer-Link und Post-Text"
    Write-MenuEntry "13" "Vollrelease (Patch/Minor/Major Auswahl)"   "macht aus der Projektversion ein neues GitHub-Release inkl. Installer-Link"
    Write-MenuEntry "16" "Patch-Version automatisch erhoehen"        "zaehlt nur lokal +1 auf die Patch-Version"
    Write-MenuEntry "20" "Nur Version hochzaehlen + Commit + Push"   "Version erhoehen und direkt committen/pushen, ohne Release"
    Write-Host ""

    Write-Host "[Werkzeuge]" -ForegroundColor Cyan
    Write-MenuEntry "14" "Git Status"                                "zeigt geaenderte Dateien"
    Write-MenuEntry "15" "Publish-Ordner aktueller Version oeffnen"  "oeffnet den lokalen publish-Ordner"
    Write-MenuEntry "17" "Alte publish-Ordner loeschen"              "raeumt alte Installer-Builds weg"
    Write-MenuEntry "18" "Release-Link in Zwischenablage"            "kopiert den GitHub-Release-Link einer Version"
    Write-MenuEntry "19" "Installer-Link in Zwischenablage"          "kopiert den direkten Installer-Link einer Version"
    Write-MenuEntry "21" "Letztes Release anzeigen"                  "zeigt das neueste GitHub-Release inkl. Installer"
    Write-MenuEntry "22" "Letzten Installer-Link in Zwischenablage"  "kopiert den Installer-Link des neuesten Releases"
    Write-MenuEntry "23" "Ergebnistext einer Version kopieren"       "kopiert fertigen Post-Text fuer Teams / Mail / Chat"
    Write-MenuEntry "24" "Ergebnistext letztes Release kopieren"     "kopiert den Post-Text des neuesten Releases"
    Write-Host ""

    Write-Host "  0   Beenden" -ForegroundColor White
    Write-Host ""
}

function Invoke-MenuChoice {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Choice
    )

    switch ($Choice) {
        "1"  { Git-Pull; Pause-Menu }
        "2"  { Stop-Processes; Pause-Menu }
        "3"  { Remove-BuildFolders; Pause-Menu }
        "4"  { Build-App; Pause-Menu }
        "5"  { Run-App }
        "6"  { Full-RebuildRun }
        "7"  {
            $version = Get-AppVersion
            $publishPath = Publish-App $version
            $open = Read-Host "Ordner oeffnen? (j/n)"
            if ($open -match '^(j|ja|y|yes)$') { Open-Folder $publishPath }
            Pause-Menu
        }
        "8"  {
            $version = Read-Host "Neue Version (z.B. 0.3.5)"
            Update-VersionInProps $version
            Pause-Menu
        }
        "9"  {
            $msg = Read-Host "Commit-Message"
            if ([string]::IsNullOrWhiteSpace($msg)) { $msg = "Update MailScanner" }
            Git-CommitAndPush $msg
            Pause-Menu
        }
        "10" {
            $version = Read-Host "Release-Version (z.B. 0.3.5)"
            Create-GitHubRelease $version
            Pause-Menu
        }
        "11" {
            $version = Read-Host "Version fuer Workflow (z.B. 0.3.5)"
            Watch-WorkflowWithProgress $version
            Pause-Menu
        }
        "12" {
            $version = Read-Host "Version fuer Release-Infos (z.B. 0.3.5)"
            Show-ReleaseInfo $version
            Pause-Menu
        }
        "13" { Full-Release; Pause-Menu }
        "14" { Git-Status; Pause-Menu }
        "15" {
            $version = Get-AppVersion
            $publishPath = Join-Path $repoPath "publish-$version"
            Open-Folder $publishPath
            Pause-Menu
        }
        "16" {
            $next = Get-NextPatchVersion
            Update-VersionInProps $next
            Pause-Menu
        }
        "17" {
            Cleanup-PublishFolders
            Pause-Menu
        }
        "18" {
            $version = Read-Host "Version fuer Release-Link (z.B. 0.3.5)"
            Copy-ReleaseLink $version
            Pause-Menu
        }
        "19" {
            $version = Read-Host "Version fuer Installer-Link (z.B. 0.3.5)"
            Copy-InstallerLink $version
            Pause-Menu
        }
        "20" {
            Bump-VersionAndPushOnly
            Pause-Menu
        }
        "21" {
            Show-LatestInstallerLink
            Pause-Menu
        }
        "22" {
            Copy-LatestInstallerLink
            Pause-Menu
        }
        "23" {
            $version = Read-Host "Version fuer Ergebnistext (z.B. 0.3.5)"
            Copy-ReleasePostText $version
            Pause-Menu
        }
        "24" {
            Copy-LatestReleasePostText
            Pause-Menu
        }
        "25" {
            Next-VersionReleaseToGitHub
            Pause-Menu
        }
        "0"  {
            return $false
        }
        default {
            Write-Warn "Ungueltige Auswahl."
            Pause-Menu
        }
    }

    return $true
}

function Start-MenuLoop {
    while ($true) {
        try {
            ShowMenu
            $choice = Read-Host "Auswahl"

            if (-not (Invoke-MenuChoice -Choice $choice)) {
                break
            }
        }
        catch {
            Write-Host ""
            Write-Host "FEHLER: $($_.Exception.Message)" -ForegroundColor Red
            Write-Log "ERROR: $($_.Exception.Message)"
            Pause-Menu
        }
    }
}

try {
    Require-Command "git"
    Require-Command "dotnet"
    Require-Command "gh"
    Test-ScriptContext
    Set-RepoLocation
    Write-StartupInfo
    Start-MenuLoop
}
catch {
    Write-Host "Startfehler: $($_.Exception.Message)" -ForegroundColor Red
    Write-Log "START ERROR: $($_.Exception.Message)"
    exit 1
}
