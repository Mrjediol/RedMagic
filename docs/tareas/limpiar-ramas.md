# Limpiar las ramas `task/*` ya fusionadas

Pendiente desde 2026-10-12, a petición del usuario: primero un backup, después borrar.

## Estado

- Comprobado el 2026-10-12: las 11 ramas `task/*` que quedan están **totalmente fusionadas en `main`**
  (`git merge-base --is-ancestor <rama> main` da sí en todas). Borrarlas no pierde cambios ni rompe el juego; sólo se
  van las etiquetas con el nombre de cada tarea.
- Ramas: `task/combo-separado`, `task/cometa`, `task/enemy-attack-deck`, `task/enemy-studio`,
  `task/gesture-attack-editor`, `task/gesture-timeline`, `task/humanoid-walker`, `task/legacy-reorg`, `task/movil`,
  `task/rig-driver-tween`, `task/tareas-unificado`.
- `task/editor-niveles` ya se borró (fusionada en `main`, commit de merge `c397a501`).
- `main` va por delante de `origin/main` (sin push: lo sube el usuario desde GitHub Desktop).

## Pasos

1. **Backup** (lo decide el usuario cómo): lo más simple, `git bundle create <ruta>/redmagic-<fecha>.bundle --all`
   (un solo archivo con todas las ramas y la historia) o copiar la carpeta del proyecto sin `Library/`. Subir `main`
   a GitHub también es un backup.
2. Antes de borrar, volver a comprobar que cada rama sigue fusionada.
3. Borrar con `git branch -d <rama>` (el borrado seguro: se niega si algo no está fusionado). Nunca `-D`.
4. Si hay ramas remotas `origin/task/*`, borrarlas desde GitHub Desktop (el push es del usuario).

## Reglas

CLAUDE.md prohíbe borrar ramas sin permiso: pedir confirmación explícita en el momento de borrar.


## Añadida 2026-10-13
- `task/movimiento-plano` (movimiento sin rampas ni escalones) también está fusionada en `main`: entra en la limpieza (ahora son 12 ramas `task/*`).
