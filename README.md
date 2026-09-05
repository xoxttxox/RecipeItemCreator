# Recipe Item Creator

Ein kleines Windows-Tool zum Erstellen und Vorbereiten von Rezept-Item-Bildern, z. B. für FiveM-Projekte.

## Funktionen

- Rezept-Item-Bilder aus einer Vorlage erzeugen
- PNG, JPG/JPEG und WebP laden
- Transparente Bildbereiche automatisch erkennen
- Item-Bild skalieren und positionieren
- Vorschau vor dem Export
- Saubere Bildverarbeitung ohne dauerhaften Dateilock
- Dunkle Windows-Titelleiste
- Versionsanzeige innerhalb der Anwendung
- GitHub-Update-Prüfung über das neueste Release

## Voraussetzungen

- Windows 10 oder Windows 11
- .NET Runtime passend zur verwendeten Projektversion

Für die Entwicklung wird das passende .NET SDK benötigt.

## Projekt bauen

Repository klonen und anschließend im Projektordner ausführen:

```bash
dotnet restore
dotnet build -c Release
```

Optional kann eine veröffentlichbare Version erzeugt werden:

```bash
dotnet publish -c Release
```

## GitHub-Update-Prüfung

Die Anwendung kann beim Start bzw. über die Benutzeroberfläche prüfen, ob auf GitHub eine neuere Version verfügbar ist.

Die Repository-URL wird in:

```text
Configuration/AppSettings.cs
```

gesetzt:

```csharp
public const string GitHubRepositoryUrl =
    "https://github.com/DEIN-NAME/RecipeItemCreator";
```

Für ein öffentliches Repository wird für die normale Release-Prüfung kein GitHub-Token benötigt.

## Releases erstellen

Für Releases sollte Semantic Versioning verwendet werden:

```text
v1.0.0
v1.0.1
v1.1.0
v2.0.0
```

Die Anwendung entfernt beim Versionsvergleich automatisch ein führendes `v`.

### Empfohlener Ablauf

1. Versionsnummer im Projekt erhöhen.
2. Projekt im `Release`-Modus bauen.
3. Anwendung testen.
4. Änderungen committen und zu GitHub pushen.
5. Unter **GitHub → Releases → Draft a new release** ein neues Release erstellen.
6. Einen Tag wie `v1.0.0` verwenden.
7. Release-Dateien, ZIP oder Installer anhängen.
8. Release veröffentlichen.

Die Update-Prüfung verwendet das neueste veröffentlichte GitHub-Release.

## Versionsnummer

Eine typische Konfiguration in der `.csproj`:

```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
</PropertyGroup>
```

Für sichtbare Versionsnummern wird empfohlen:

```text
Major.Minor.Build
```

Beispiel:

```text
1.0.0
```

## Verzeichnisstruktur

Beispiel:

```text
RecipeItemCreator/
├─ Configuration/
│  └─ AppSettings.cs
├─ Forms/
├─ Services/
│  ├─ AppInfo.cs
│  ├─ GitHubUpdateService.cs
│  ├─ ImageComposer.cs
│  └─ WindowsTheme.cs
├─ Properties/
├─ Resources/
├─ Program.cs
└─ RecipeItemCreator.csproj
```

Die tatsächliche Struktur kann je nach Projektstand abweichen.

## Unterstützte Bildformate

Über SkiaSharp können unter anderem folgende Formate verarbeitet werden:

- PNG
- JPG / JPEG
- WebP

Für Item-Bilder wird PNG mit transparentem Hintergrund empfohlen.

## Sicherheit

Es werden keine GitHub-Zugangsdaten oder Tokens benötigt bzw. in der Anwendung gespeichert.

GitHub-Tokens sollten niemals fest in eine öffentlich verteilte EXE eingebaut werden.

## Lizenz

Dieses Projekt steht unter der MIT-Lizenz.

Weitere Informationen befinden sich in der Datei [LICENSE](LICENSE).
