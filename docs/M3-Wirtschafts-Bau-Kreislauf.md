# M3 – Wirtschafts-/Bau-Kreislauf

Plan für den Wirtschafts- und Bau-Kreislauf als Teil von **M3** (AI & erweiterte Logik:
Armee/Bau/Wirtschaft, Handel, Missionen).

## Ausgangslage (M2, vorhanden)

- Bauplätze → Bauphase (Baumeister) → aktive Produktion
- Transportnetz (Flaggen, Straßen, Karawanen)
- Ressourcenarbeiter (Holzfäller, Steinhauer, Förster)
- Militär-Rekrutierung und Turmangriffe
- Bevölkerungswachstum über Wohnhäuser

## Lücken für einen echten Kreislauf

### 1. Werkzeug-Zyklus (Arbeiter brauchen Werkzeug) — ✅ erledigt

- **Aktuell:** Arbeiter werden gratis zugewiesen.
- **Neu:** Jeder Beruf braucht ein Werkzeug (Axt, Säge, Spitzhacke, Hammer,
  Sense, Angel, Klinge). Das Werkzeug wird beim Zuweisen aus dem Lager
  verbraucht; der Schmied produziert es.
- **Dateien:** `Freeserf3.Core/Game/Game.cs` (Worker-Zuweisung),
  `Freeserf3.Core/Game/BuildingDefinition.cs` (Werkzeug pro Beruf),
  `Freeserf3.Core/Game/Material.cs`.

### 2. Nahrungs-Zyklus (Bevölkerung braucht Essen) — ✅ erledigt

- **Aktuell:** Bevölkerung wächst alle 600 Ticks gratis.
- **Neu:** Siedler konsumieren Nahrung (Brot/Fisch/Fleisch) aus dem Lager;
  Bevölkerung wächst nur bei Nahrungsüberschuss und schrumpft bei Mangel.
  Bergwerke brauchen Nahrung als Input (wie im Original).
- **Dateien:** `Freeserf3.Core/Game/Game.cs` (TickOnce),
  `Freeserf3.Core/Game/Player.cs`.

### 3. Produktionsketten vervollständigen — ✅ erledigt

- Bergwerke: Erz aus Karten-Ressourcen statt unbegrenzt (Kohle, Eisen, Gold,
  Schwefel, Edelsteine auf der Karte).
- Farm/Fischer/Winzer: Boden-/Wasser-Nähe prüfen (WorkArea statt nur Radius).
- Fehlende Arbeiter-Typen ergänzen (Brewery, RiceFarm, Distillery, Beekeeping,
  MeadBrewery, Laboratory haben aktuell `WorkerType.None`).
- **Dateien:** `Freeserf3.Core/Game/BuildingDefinition.cs`,
  `Freeserf3.Core/Game/Game.cs`, `Freeserf3.Core/Game/Map.cs`.

### 4. Bauplatz-Validierung — ✅ erledigt

- **Aktuell:** `CanPlaceBuilding` prüft nur Bounds/Walkable.
- **Neu:** Gelände-Eignung pro Gebäude (Mine → Berg, Farm → fruchtbar,
  Fischer → Wasser, Wohnhaus → eigenes Territorium).
- **Dateien:** `Freeserf3.Renderer/GameView.cs` (CanPlaceBuilding),
  `Freeserf3.Core/Game/Game.cs`.

### 5. Gebäude-Abriss — ✅ erledigt

- Gebäude abreißen → Baumaterial teilweise zurück ins Lager, Arbeiter/Soldaten
  freigeben.
- **Dateien:** `Freeserf3.Core/Game/Building.cs` (Destroy),
  `Freeserf3.Core/Game/Game.cs`, `Freeserf3.Renderer/GameView.cs` (UI).

### 6. Warenverteilung & Prioritäten — ✅ erledigt

- Lager-Verteilung mit Prioritäten (Baumaterial > Produktions-Input > Rest),
  damit Kreisläufe nicht verhungern.
- **Dateien:** `Freeserf3.Core/Game/Game.cs`
  (IsConstructionMaterialNeeded/IsInputMaterialNeeded),
  `Freeserf3.Core/Game/TransportNetwork.cs`.

## Reihenfolge

1 → 2 → 3 → 4 → 5 → 6 (jeder Schritt baut auf dem vorherigen auf; 1+2 sind der
Kern des Kreislaufs).

## Erweiterungen (Armee & KI)

Nach Abschluss des Kreislaufs wurden Armee und KI-Gegner ergänzt:

- **Soldatenkampf:** Soldaten suchen Feinde, bewegen sich auf sie zu und
  greifen an (Schwert/Speer/Bogen mit unterschiedlichen Werten). Türme
  schießen auf Feinde in Reichweite (20 Schaden pro Treffer).
- **Soldaten-Sprites:** aus dat-Datei 12 (Schwertkämpfer 9, Speerkämpfer 15,
  Bogenschütze 21) plus Angriffs-Animationen (ACTION1: 12/18/24).
- **Gebäude-Schaden:** Gebäude haben Gesundheit (basierend auf Baukosten);
  Soldaten können feindliche Gebäude zerstören.
- **KI-Gegner:** Alle Spieler werden geladen; KI-Spieler bauen Wohnhäuser,
  Produktion und Militär (inkl. Minen/Schmieden für Waffen), breiten sich in
  Richtung des menschlichen Spielers aus und schicken Angriffswellen.
- **Sieg/Niederlage:** Sieg bei Zielbevölkerung, Niederlage wenn keine
  Gebäude und Siedler mehr übrig sind; Overlay zeigt das Ergebnis.
- **Kampf-Sounds:** Siedler getötet (35), Gebäude zerstört (93).
