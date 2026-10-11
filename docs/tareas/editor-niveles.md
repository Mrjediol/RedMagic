# EditorNiveles — editor de niveles web + importador en Unity

Paso `nh-2` de `niveles-hielo` (`docs/tareas/niveles-hielo.md`), sacado a tarea propia el 2026-10-11.
**Estado (2026-10-12): funciona (importado y probado por el usuario). Falta reubicarlo (ver Queda).**
Arquitectura y formato: `docs/architecture/levels.md` ▸ *EditorNiveles*.

## Decisiones del usuario

- **Corrección del usuario (2026-10-12)**: el editor va DENTRO de la web principal (`Web/RedMagicWeb`, pestaña propia), y en
  Unity sólo el importador, en un menú propio aparte de Tools (ya hecho: *EditorNiveles ▸ Importar nivel…*). Nada de
  opciones extra que no pidió (se quitaron: abrir web, exportar, actualizar biomas, ayudas de la web).
- Por ahora sólo **terreno y plataformas (placeholder)**. Entrada/salida y enemigos: no desde la web (la escena nueva
  pone `SectionEntry`/`SectionExit` en los extremos y se mueven a mano).
- Pronto: **varios tipos de plataforma** y **objetos** (decoración y fuentes de luz, p. ej. cristales) → `nh-3`.
- Tras importar, el nivel **se sigue editando en Unity** (paleta, objetos): reimportar sólo toca tiles, tamaño y bioma.

## Hecho

- Web (`index.html`, `editor.js`, `terreno.js`): pincel / rectángulo / relleno / borrar, grosor, deshacer, tamaño
  (alto ≤ 75, pegado abajo a la izquierda), vista previa con borde,
  textura y fondo del bioma, guardar/abrir (File System Access, descarga si no hay), autoguardado, arrastrar .json.
- `datos-unity.js` generado desde Unity (tiles + valores y capas de fondo de cada `LevelBiome`).
- Unity (`Assets/Test/EditorNiveles/Editor/`): `LevelSketchFile` (formato v2 con leyenda por nombre de tile, lee v1),
  `EditorNivelesMenu` (sólo *EditorNiveles ▸ Importar nivel…*), `EditorNivelesWebData`.
- Unity: interruptores *Nieve del borde* en `Level` (roca / plataformas) y botones *🖌 Pintar roca / plataformas*.
- Ejemplo: `Web/EditorNiveles/bocetos/NivelHielo_1000.levelsketch.json` (exportado del nivel real).
- A Legacy: `LevelSketchImporter` (sin menú) y la pestaña web vieja (`Web/legacy/level-sketch.js`).

## Queda

- **Mover el editor web a una pestaña de `Web/RedMagicWeb`** (hoy es la página suelta `Web/EditorNiveles/index.html`).
- Cuando funcione: mover `Assets/Test/EditorNiveles/` a su sitio (p. ej. `Assets/Scripts/Levels/Editor/EditorNiveles/`).
- Con `nh-3`: tipos de plataforma (tile `Plataforma<Algo>` en `_Biomas` → sale solo en la paleta web) y objetos
  colocables (decoración y luces, p. ej. cristales: lista `objects` en el formato y prefabs por convención de nombre).
