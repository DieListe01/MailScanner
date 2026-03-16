# MailScanner Architektur

## Ziel

Die Anwendung startet als lokales DMS fuer private Rechnungen und Dokumente.
Dateien liegen im Dateisystem oder spaeter auf dem NAS, waehrend Metadaten,
Suchinformationen und Mail-Verarbeitungsstatus in einer SQLite-Datenbank landen.

## Projekte

- `MailScanner.App`: WPF-Oberflaeche fuer Ergebnisliste, Suche und spaetere Verwaltung
- `MailScanner.Core`: Domainenmodelle und Schnittstellen
- `MailScanner.Infrastructure`: IMAP, PDF, OCR, KI, NAS, Dateisystem
- `MailScanner.Data`: Persistenz und spaetere SQLite-Anbindung

## Geplanter Ablauf

1. IMAP-Konten abrufen
2. Bereits verarbeitete Nachrichten anhand Ordner und UID ueberspringen
3. PDF-Anhaenge als Kandidaten auflisten
4. Benutzer entscheidet ueber Download, Kategorie und Ablage
5. Dokument speichern und Metadaten persistieren
6. Spaeter OCR und KI-Extraktion hinzufuegen
7. Scanner-Import spaeter ueber getrennten Eingangsordner anbinden

## Datenspeicher

- Dateien: `storage/` oder spaeter NAS-Freigabe
- Metadaten: SQLite
- Suchfunktion: zuerst SQL-basierte Filter, spaeter Volltextindex

## Aktueller Stand

- IMAP-Konfiguration liegt in `src/MailScanner.App/appsettings.json`
- Bereits gescannte Postfaecher werden ueber `MailboxScanStates` in SQLite verfolgt
- Gefundene PDF-Kandidaten werden in `DocumentCandidates` persistiert und in der UI angezeigt
- Scanner-Anbindung ist bewusst noch nicht umgesetzt und folgt spaeter als eigener Importpfad
