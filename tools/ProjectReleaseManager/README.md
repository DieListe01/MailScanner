# ProjectReleaseManager

Kleine C#-CLI fuer Versionspflege und GitHub-Releases ueber mehrere Projekte unter `W:\_Dirk\Projekte`.

## Build

```powershell
dotnet build tools/ProjectReleaseManager/ProjectReleaseManager.csproj
```

## Konfiguration

Die Standard-Konfiguration liegt in `tools/ProjectReleaseManager/projects.json`.

Wichtige Felder pro Projekt:

- `name`: Anzeigename im Tool
- `repositoryRoot`: Projektordner absolut oder relativ zu `projectsRoot`
- `versionFile`: Datei mit der Version, z. B. `Directory.Build.props`
- `versionScheme`: `DirectoryBuildProps` oder `PackageJson`
- `buildCommand`: lokaler Vorab-Build
- `releaseBranch`: meist `main`
- `releaseWorkflow`: GitHub-Workflow-Datei, z. B. `release.yml`
- `releaseStrategy`:
  - `GitHubRelease`: erstellt direkt eine GitHub-Release `vX.Y.Z`
  - `WorkflowDispatch`: startet nur den Workflow mit `version=X.Y.Z`

## Aufrufe

```powershell
dotnet run --project tools/ProjectReleaseManager
dotnet run --project tools/ProjectReleaseManager -- discover
dotnet run --project tools/ProjectReleaseManager -- list
dotnet run --project tools/ProjectReleaseManager -- status MailScanner
dotnet run --project tools/ProjectReleaseManager -- bump MailScanner patch
dotnet run --project tools/ProjectReleaseManager -- release MailScanner 0.4.0
dotnet run --project tools/ProjectReleaseManager -- release MailScanner minor
```

## Verhalten

- startet ohne Argumente in ein kleines Konsolenmenue
- kann neue Git-Repositories unter `projectsRoot` erkennen und in `projects.json` eintragen
- laesst untracked Dateien im Repo in Ruhe
- bricht bei getrackten, uncommitteten Aenderungen absichtlich ab
- aktualisiert nur die konfigurierte Versionsdatei
- fuehrt optional einen lokalen Build aus
- pusht nach GitHub
- erstellt je nach Strategie eine GitHub-Release oder startet einen Workflow
- wartet danach optional auf den neuesten Workflow-Run
