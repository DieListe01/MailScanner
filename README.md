# MailScanner

MailScanner ist eine WPF-Desktop-App fuer Windows, die private IMAP-Postfaecher durchsucht, PDF-Anhaenge erkennt, priorisiert und fuer die lokale Ablage vorbereitet.

## Highlights

- Vollscan oder Rueckblick in Tagen fuer Erstlaeufe
- Globale und konto-spezifische Ordnerausschluesse
- Priorisierung per Score fuer Rechnungen und wichtige Dokumente
- Moderne Review-Oberflaeche mit Status-, Kategorie- und Prioritaets-Badges
- GitHub-Release-Pruefung beim Start
- Windows-Installer per Inno Setup und GitHub Actions Release-Workflow

## Entwicklung

```powershell
dotnet build MailScanner.slnx
```

## Einstellungen

- Benutzer-Einstellungen liegen unter `%LocalAppData%\MailScanner\appsettings.json`.
- Beim ersten Start kopiert die App die mitgelieferte Vorlage in dieses Benutzerprofil und verwendet danach nur noch diese Datei.
- Updates und neue Installer-Versionen ueberschreiben die Benutzer-Einstellungen damit nicht mehr.

## Versionen und Releases

- Die App-Version kommt zentral aus `Directory.Build.props`.
- GitHub Releases sollten Tags wie `v0.1.0` oder `0.1.0` verwenden.
- Der Workflow in `.github/workflows/release.yml` baut ein Windows-x64-Paket und einen Installer.
- Der Installer wird ueber `installer/MailScanner.iss` erstellt.
- Fuer den kompletten Release-Ablauf gibt es `scripts/New-Release.ps1`.

```powershell
pwsh ./scripts/New-Release.ps1 -Version 0.1.6 -PublishRelease
```

## Icons

- Das aktive App-Icon liegt in `src/MailScanner.App/Assets/MailScanner.ico`.
- Weitere Varianten werden nach `src/MailScanner.App/Assets/IconVariants` erzeugt.
- Die Varianten lassen sich mit `scripts/Build-MailScannerIcons.ps1` neu generieren.

## Update-Pruefung

Beim Start fragt die App die neueste freigegebene GitHub-Release von `DieListe01/MailScanner` ab. Entwuerfe und Pre-Releases werden ignoriert.
