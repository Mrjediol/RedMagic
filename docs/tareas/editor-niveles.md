# EditorNiveles — editor de niveles web + importador en Unity

Paso `nh-2` de `niveles-hielo` (`docs/tareas/niveles-hielo.md`), sacado a tarea propia el 2026-10-11.

## Decisión del usuario

- Es una **herramienta nueva y propia, "EditorNiveles"**: **no** va como pestaña de la web principal
  (`Web/RedMagicWeb`, la vieja pestaña 🧱 Boceto de nivel) **ni** dentro de *Tools ▸ Web* en Unity.
  - Web: página propia, p. ej. `Web/EditorNiveles/index.html`.
  - Unity: menú propio, p. ej. *Tools ▸ EditorNiveles ▸ …*, con el importador.
  - Confirmar con el usuario al empezar si es esto lo que quiere decir con "nuevo tool".

## Qué hay ya (partir de aquí, no de cero)

- **Sistema de niveles en Unity** (`docs/architecture/levels.md`):
  - `Level` + Tilemaps `Grid/Terreno` (roca) y `Grid/Plataformas` (plataformas atravesables).
  - 1 celda = 1 unidad = 40 px, alto máximo 75.
  - El borde de hielo, el collider (esquinas cuadradas), la cámara y el parallax se generan solos al pintar.
- **Nivel de referencia**: `Assets/Scenes/NivelHielo_1000.unity`.
  - Es el nivel en el que trabaja el usuario y la única sección del Mundo 1.
  - Tiene `SectionEntry` y `SectionExit` (puestos a ojo) y `PlayerNuevo`.
- **Importador viejo**: `Assets/Scripts/Run/Editor/LevelSketchImporter.cs`, menú
  *Tools ▸ RedMagic ▸ Niveles ▸ Importar boceto en el nivel...*.
  - Lee `*.levelsketch.json` (`rows`: `#` roca, `.` aire) y lo vuelca en un `Level`.
  - Aún no lee plataformas.
- **Prototipos web**:
  - `Web/ParallaxTest/boceto-nivel.html` (298 líneas): ya tiene borde, fondo y niveles.
  - `Web/RedMagicWeb/modules/level-sketch.js`: la pestaña vieja de 98 líneas.
- **Otros comandos de niveles**: `LevelSetup.cs` / `LevelEditor.cs` (menú *Tools ▸ RedMagic ▸ Niveles*: bioma,
  paleta, fondo de parallax, nieve).

## Qué tiene que hacer

1. **Editor web de cuadrícula**:
   - pintar roca y plataforma (pincel, rectángulo, borrar);
   - tamaño en celdas, con tope de 75 de alto;
   - vista previa con el borde de hielo y el fondo del bioma;
   - exportar `*.levelsketch.json` con las plataformas en otra marca (p. ej. `=`).
2. **Importador en Unity (menú propio EditorNiveles)**:
   - roca → `Grid/Terreno`, plataformas → `Grid/Plataformas`, tamaño → `Level`;
   - en una escena de nivel nueva o en la abierta.
   - Lo que hay de *Niveles* se puede mover a ese menú.
3. Probablemente también quiera colocar en el editor web la entrada y la salida (`SectionEntry` / `SectionExit`) y los
   enemigos. Preguntarlo; no darlo por hecho.

## Reglas que aplican

- Lo nuevo va primero a `Assets/Test/`.
- Lo que deje de usarse (la pestaña vieja, `LevelSketchImporter` si se sustituye) se mueve a `Assets/Legacy/`.
- Herramienta reutilizable, no un nivel suelto ([[build-the-tool-not-the-instance]]).
- Para probar: pasos cortos en el chat, y una sola importación en Unity al final.
