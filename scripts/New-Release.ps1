param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$PublishRelease,

    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

function Update-VersionFile {
    param(
        [string]$FilePath,
        [string]$ReleaseVersion
    )

    [xml]$xml = Get-Content -Path $FilePath
    $propertyGroup = $xml.Project.PropertyGroup
    $propertyGroup.Version = $ReleaseVersion
    $propertyGroup.AssemblyVersion = "$ReleaseVersion.0"
    $propertyGroup.FileVersion = "$ReleaseVersion.0"
    $propertyGroup.InformationalVersion = $ReleaseVersion
    $xml.Save($FilePath)
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionValue = $Version.Trim().TrimStart('v', 'V')
$tagName = "v$versionValue"
$versionFile = Join-Path $repoRoot 'Directory.Build.props'

Push-Location $repoRoot

try {
    Update-VersionFile -FilePath $versionFile -ReleaseVersion $versionValue

    if (-not $SkipBuild) {
        dotnet build "src/MailScanner.App/MailScanner.App.csproj"
    }

    git add "Directory.Build.props"
    git commit -m "Bump version to $versionValue"
    git push origin main
    git tag $tagName
    git push origin $tagName

    if ($PublishRelease) {
        gh release create $tagName --target main --generate-notes --title "MailScanner $versionValue"
    }
}
finally {
    Pop-Location
}
