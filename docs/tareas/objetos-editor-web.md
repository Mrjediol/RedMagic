# Objetos en el editor de niveles web: plataformas y puertas

Pendiente desde 2026-10-13. Leer antes: `docs/architecture/levels.md` ▸ *Painted objects* y *EditorNiveles*, `docs/tareas/editor-niveles.md`.

## Objetivo

Poder **dibujar en la web (🧱 Niveles) plataformas y puertas** y que **Unity las cree al importar**, sin colocarlas a mano.
Todo en celdas enteras: una puerta es 1-2 de ancho × 3-5 de alto (tiene que caber exacta entre techo y suelo), una plataforma de
arte mide las celdas que se pinten (extremos + centro se eligen solos).

## Qué ya existe en Unity (en `Assets/Scripts/Test/`, sin mover aún)

- **Plataformas de arte pintables**: `ArtPlatformTile` (sprite según vecinos) + `ArtPlatformTilemap` (una colisión fina de un sentido por
  tramo, `surfaceDrop` 0,04 + `colliderThickness`). Tiles `ArtTile_FinaPequena/Mediana/Larga` en `Assets/Levels/_Biomas/Hielo/Objetos/Plataformas/Tiles`.
  Paleta `PaletaPlataformasArte`. Menú *Tools ▸ RedMagic ▸ Niveles ▸ Preparar plataformas de arte*.
- **Puertas pintables**: `DoorTile` (cerrada / abierta: `DoorTile_Energia[_Abierta]`) + `DoorTilemap` (una `LevelDoor` por grupo de celdas, no se
  guarda en la escena). Menú *Preparar puertas pintables*.
- **Salas**: `LevelRoom` + `WaveManager` (ver `docs/architecture/run-world.md`). Las salas y las oleadas se montan en Unity a mano.
- Los tiles se llaman `ArtTile_*` a propósito: si empezaran por `Plataforma` el importador actual los metería en el Tilemap `Plataformas`
  (roca con borde procedural).

## Pasos

1. **Formato v3** de `*.levelsketch.json`: capa nueva `objects` (o más capas en `legend`) con tipo (`artPlatform`, `door`), tile por nombre y
   celdas. Mantener lectura de v1 y v2 (`LevelSketchFile`, `docs/schemas/`).
2. **Web**: en 🧱 Niveles, paleta de objetos (plataformas finas pequeña/mediana/larga, puerta cerrada / abierta); pintar con el mismo pincel,
   rectángulo y borrar; vista previa aproximada (la puerta como rectángulo; la plataforma con extremos y centro). `datos-unity.json` ya lista
   tiles por nombre: añadir los `ArtTile_*` y `DoorTile_*` (los genera `EditorNivelesWebData`).
3. **Importador** (`EditorNivelesMenu`): al importar, crear (si faltan) los Tilemaps `PlataformasArte` y `Puertas` con sus componentes y pintar
   las celdas. Sólo toca tiles, como hasta ahora: lo colocado a mano en Unity (salas, enemigos) sobrevive.
4. Validar con el usuario y **mover** `Assets/Scripts/Test/{ArtPlatform*,DoorTile*,DoorTilemap,LevelDoor,LevelPlatform}.cs` y `Test/Editor/` a su sitio
   (`Assets/Scripts/Levels/`), y `Assets/Test/Plataformas|Puertas` a su sitio definitivo.
5. Lo que queda en Unity a mano: **salas** (`LevelRoom`) y **oleadas**. Posible paso futuro: `rooms` en el formato.

## Decisiones del usuario

- Las plataformas de juego serán **las finas** (las gruesas no gustaron como tiles). Las gruesas, si vuelven, necesitan tiles de varias celdas de alto.
- Colisión de la plataforma = una línea fina arriba; abajo se atraviesa; en vertical da igual que no sea celda exacta, en horizontal sí.
- La nieve del borde del terreno no queda bien junto a objetos pegados al suelo/techo (probado y rechazado el solape bajo la nieve). **Lo pensará él**; no repetir.
