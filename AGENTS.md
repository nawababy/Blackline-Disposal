# Repository Guidelines

## Kommunikation

- Antworte Pascal auf Deutsch. Code, Klassen, Methoden, Dateinamen und technische Kommentare bleiben auf Englisch.
- Erklaere notwendige Unity-Editor-Schritte einfach, exakt und in der Reihenfolge, in der Pascal sie ausfuehren soll.
- Wenn Pascal ein Script manuell ersetzen muss, liefere immer das vollstaendige Script, keine einzelnen Ersatzzeilen.
- Behaupte niemals, etwas sei getestet, wenn es nicht tatsaechlich getestet wurde. Nenne klar, was nur geprueft oder nicht geprueft wurde.

## Projektstruktur

- Unity-Version: `6000.2.14f1`. Hauptcode liegt in `Assets/Scripts`.
- `Assets/Scripts/Core`: Bootstrap und Game Flow, z. B. `GameManager`.
- `Assets/Scripts/Systems`: Input, Saving und Settings.
- `Assets/Scripts/Gameplay`: Player, Inventory, Economy, Facilities, Trash und Character Creation.
- `Assets/Scripts/UI`: HUD und Menues. Szenen liegen in `Assets/Scenes` mit `MainMenu`, `CharacterCreation` und `GameScene`.
- Third-Party-Assets bleiben in ihren bestehenden `Assets/*`-Ordnern und werden nur auf ausdrueckliche Anforderung geaendert.

## Arbeitsweise vor jeder Änderung

- Lies zuerst alle direkt und indirekt betroffenen eigenen Scripts. Suche alle Verwendungen der zu aendernden Klassen, Methoden, Events, Interfaces und serialisierten Felder.
- Pruefe Abhaengigkeiten, Initialisierungsreihenfolge, Unity-Lifecycle, Zustaendigkeiten und Call Sites.
- Bewerte Auswirkungen auf UI, Gameplay, Saving, Input, Szenen, Prefabs, Performance und spaeteren Multiplayer.
- Bei groesseren Aenderungen zuerst nur einen Plan mit betroffenen Dateien, Risiken und Migrationsschritten erstellen. Grosse Refactorings erst nach ausdruecklicher Freigabe durchfuehren.
- Keine unnoetigen Aenderungen ausserhalb der eigentlichen Aufgabe vornehmen.

## Unity-Sicherheit

- Unity-`.meta`-Dateien und GUIDs immer erhalten. Scripts und Assets nicht verschieben oder umbenennen, ohne ihre `.meta`-Dateien zu erhalten.
- Bestehende `MonoBehaviour`-Namespaces nicht ohne Migrationsplan aendern.
- Serialisierte Felder nicht sorglos umbenennen oder entfernen. Bei Feldumbenennungen `FormerlySerializedAs` verwenden, wenn passend.
- Vor Aenderungen an serialisierten Feldern Szenen-, Prefab- und Inspector-Risiken nennen.
- Packages, Unity-Version, Render Pipeline, Input System oder Netzwerk-Pakete nicht ohne Freigabe veraendern.
- Szenen, Prefabs, ScriptableObjects und ProjectSettings nicht stillschweigend aendern.
- `Library`, `Temp`, `obj`, `Logs`, `Builds` und `UserSettings` niemals bearbeiten oder committen.
- Keine Inspector-Referenz einfach voraussetzen. Benoetigte Inspector-Zuweisungen am Ende exakt nennen.

## Architektur und Script-Harmonie

- Bestehende Systeme zuerst verstehen und erweitern, bevor neue Systeme oder Alternativloesungen erstellt werden.
- Keine parallelen Manager oder doppelten Loesungen fuer denselben Zustand erstellen. Eine eindeutige Source of Truth pro Zustand verwenden.
- `Core`, `Systems`, `Gameplay` und `UI` klar trennen. UI zeigt Gameplay-Zustand an und sendet Benutzerabsichten, besitzt aber keine autoritative Geschaeftslogik.
- Versteckte globale Abhaengigkeiten, mutable statics, zirkulaere Abhaengigkeiten und grosse Allzweck-Manager vermeiden.
- Explizite Abhaengigkeiten, Events und Interfaces an sinnvollen Systemgrenzen bevorzugen. Event-Abonnements zuverlaessig wieder abmelden.
- Konfigurationsdaten und Runtime-Zustand trennen. ScriptableObjects primaer fuer Konfiguration und erstellte Daten verwenden, nicht als unkontrollierten globalen Runtime-Zustand.
- Keine unnoetige Abstraktion oder komplizierte Architektur fuer einfache Probleme einfuehren.
- Neue Scripts muessen stilistisch und architektonisch mit den bestehenden Systemen harmonieren.

## Zukünftiger Multiplayer

Das Spiel soll spaeter bis zu vier Spieler im player-hosted Koop unterstuetzen.

- Noch kein Networking-Framework installieren oder festlegen, solange Pascal es nicht ausdruecklich freigibt.
- Gameplay-Systeme so gestalten, dass spaeter Host-Autoritaet moeglich ist.
- Shared Bank, Weltzustand, Trash, Maschinen, Facilities, Missionen, Heat, Polizei, gemeinsame Ziele und gemeinsamer Fortschritt sollen langfristig host-autoritaer sein.
- Kamera, lokale Eingabe, lokale Menues und rein visuelle Darstellung bleiben lokal.
- UI darf autoritativen Zustand nicht direkt veraendern. Request, Validierung, Zustandsmutation und Darstellung logisch trennen.
- Client-Angaben zu Geld, Inventar, Belohnungen oder Interaktionen niemals blind vertrauen.
- Keine Architektur voraussetzen, in der immer genau ein lokaler Player existiert.
- Ownership, Authority, Late Join, Reconnect, doppelte Befehle und ungueltige Client-Anfragen beruecksichtigen.
- Stabile IDs fuer speicherbare und spaeter netzwerkrelevante Objekte vorsehen. Keine Unity-Objektreferenzen als zukuenftige Netzwerkdaten einplanen.
- Keine spekulative Networking-Komplexitaet implementieren, solange sie noch nicht benoetigt wird.
- Bei jeder Gameplay-Aenderung kurz bewerten, ob sie spaeter netzwerkfaehig ist oder ein Rewrite verursachen wuerde.

## Saving

- Das Save-System soll spaeter mehrere Slots, Dateiversionen, Migrationen, beschaedigte Dateien, Backups und host-autoritative Koop-Saves unterstuetzen.
- `PlayerPrefs` nicht als dauerhafte Hauptspeicherung des Spielstandes verwenden; nur fuer kleine lokale Einstellungen einsetzen.
- Plain serializable data und stabile IDs speichern, keine Szenenobjekt-Referenzen.
- Gemeinsame Weltdaten und spielerspezifische Daten trennen. Save-Daten nicht direkt an `MonoBehaviour`s koppeln.
- Save-Dateien atomar oder ueber sichere temporaere Dateien schreiben, damit keine halbfertigen Saves entstehen.
- Vor Aenderungen an Save-Strukturen Abwaertskompatibilitaet pruefen. Breaking Changes benoetigen Migration oder eine ausdruecklich bestaetigte Zuruecksetzung.

## Performance

- Nicht blind optimieren, sondern Haeufigkeit und Hot Paths beruecksichtigen.
- In `Update`, `FixedUpdate` und haeufigen Events unnoetige Allokationen vermeiden.
- Wiederholte Scene-Suchen, `FindObjectOfType`, `FindFirstObjectByType`, Tag-Suchen und `GetComponent`-Aufrufe in Hot Paths vermeiden. Stabile Referenzen cachen.
- LINQ in haeufig ausgefuehrten Bereichen vermeiden, wenn dadurch Allokationen entstehen.
- Kein permanentes Logging, String-Building oder Speichern auf Datentraeger pro Frame.
- Physikabfragen auf sinnvolle Layer, Reichweite und Frequenz begrenzen.
- Haeufiges `Instantiate`/`Destroy` pruefen und gegebenenfalls Pooling erwaegen.
- Unnoetige UI-Layout-Rebuilds vermeiden.
- Einstellungen nicht waehrend jeder Slider-Bewegung staendig auf Datentraeger schreiben; bei Bedarf Debouncing verwenden, waehrend die sichtbare Einstellung sofort angewendet wird.
- Lesbarkeit und Korrektheit vor Mikrooptimierungen priorisieren, solange kein gemessener Engpass besteht.
- Bei relevanten Aenderungen moegliche GC-Allokationen und Main-Thread-Kosten nennen.

## Async- und Lifecycle-Sicherheit

- `async void` nur an echten Event-Handler-Grenzen verwenden.
- Cancellation beruecksichtigen, wenn Aufgaben Szenen oder GameObjects ueberleben koennten.
- Keine zerstoerten Unity-Objekte aus verzoegerter oder asynchroner Arbeit ansprechen.
- Unity-Main-Thread-Anforderungen beachten.
- Coroutines, Tasks, Events, Timer und Callbacks beim Deaktivieren oder Zerstoeren korrekt beenden oder abmelden.
- Initialisierung und Shutdown explizit behandeln.

## Fehlerbehandlung

- Externe Daten und Save-Daten validieren. Bei ungueltigen persistenten Daten sicher und nachvollziehbar reagieren.
- Exceptions nicht stillschweigend verschlucken.
- Null Checks nicht nur verwenden, um fehlerhafte Initialisierung zu verstecken.
- Fehlermeldungen sollen das verantwortliche System oder Objekt klar nennen.
- In haeufig ausgefuehrten Release-Pfaden kein uebermaessiges Logging erzeugen.

## Codequalität

- Klare, beschreibende englische Namen verwenden. Eine primaere Verantwortung pro Klasse.
- Private serialized fields statt unnoetiger oeffentlicher veraenderbarer Felder verwenden. Oeffentliche APIs klein halten.
- Keine Magic Strings und unerkaerten Magic Numbers. Konstanten, Konfiguration oder benannte Felder bevorzugen.
- Kommentare erklaeren Absicht und nicht offensichtliche Einschraenkungen, nicht triviale Syntax.
- Kein toter, auskommentierter oder temporaerer Debug-Code.
- Keine neue Abhaengigkeit nur aus Bequemlichkeit hinzufuegen.
- Die im Projekt unterstuetzte Unity- und C#-Version beachten. Keine neuen Compiler-Warnungen hinterlassen.

## Build, Tests und Entwicklung

- Projekt mit Unity Hub oder `Unity.exe -projectPath "C:\OwnGame\My project"` oeffnen.
- Edit-Mode-Tests: `Unity.exe -batchmode -quit -projectPath "C:\OwnGame\My project" -runTests -testPlatform EditMode`.
- Play-Mode-Tests entsprechend mit `-testPlatform PlayMode` ausfuehren, wenn vorhanden.
- Windows-Build-Beispiel: `Unity.exe -batchmode -quit -projectPath "C:\OwnGame\My project" -buildWindows64Player Builds\Windows\MyProject.exe`.
- Die Unity Test Framework Package ist installiert. Wenn Tests ergaenzt werden, liegen sie unter `Assets/Tests/EditMode` oder `Assets/Tests/PlayMode` und heissen z. B. `SaveManagerTests.cs`.

## Prüfung nach Änderungen

Nach jeder Implementierung:

1. Den vollstaendigen Diff pruefen.
2. Alle Call Sites pruefen.
3. Serialisierte Felder und moegliche Inspector-Referenzen pruefen.
4. Initialisierung und Shutdown pruefen.
5. Save-Kompatibilitaet pruefen.
6. Multiplayer-Auswirkungen pruefen.
7. Performance und moegliche Hot-Path-Allokationen pruefen.
8. Verfuegbare Kompilierung oder Tests ausfuehren.
9. Klar nennen, was ausserhalb des Unity Editors nicht geprueft werden konnte.
10. Exakte manuelle Unity-Testschritte liefern.

Der Abschlussbericht muss enthalten: Zusammenfassung der Aenderungen, exakte Liste veraenderter Dateien, Architekturentscheidungen, Risiken und verbleibende Bedenken, automatisch ausgefuehrte Pruefungen, noch notwendige manuelle Unity-Schritte und einen Vorschlag fuer eine kurze Git-Commit-Summary.

## Git

- Git-Historie niemals umschreiben oder loeschen. Niemals Force Push verwenden.
- Nicht gespeicherte Aenderungen von Pascal niemals verwerfen.
- Keine generierten Unity-Ordner committen.
- Aenderungen klein, fokussiert und ueberpruefbar halten.
- Bei grossen Aenderungen einen separaten Branch oder Worktree empfehlen.
- Keine Commits ohne Ueberpruefung des Diffs durchfuehren.
- Commit-Summaries kurz, imperativ und projektnah formulieren, z. B. `Refine Unity agent guidelines`.
