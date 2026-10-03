# Apunte → taller: que un apunte lleve al taller que lo hace

## Objetivo

En 📋 Tareas, un apunte (la parte escrita, el to-do) como "15 enemigos: 5 por zona" debe dar acceso directo al taller
que lo resuelve y seguir su avance: desde el apunte → 🎨 Diseños (pedir diseños de enemigos) → al aceptar → 👾 Enemigos
(montar con make-enemy) → aprobar. Lo mismo para mapas, ataques del jugador, items, mecánicas y bugs cuando esos
talleres existan.

## Estado (2026-10-04)

Hecho (rama `task/tareas-unificado`):
- El tab 📋 Tareas está unificado: 📝 Apuntes (fases plegables, abiertas por defecto) + ⚙ Talleres (secciones plegables,
  cerradas por defecto); el antiguo tab 🎨 Designs vive dentro de los talleres 🎨 Diseños y 👾 Enemigos.
- Barra lateral izquierda para saltar a fases y talleres; clic en una tarea → panel derecho con título, descripción,
  pasos, lanzar, informe.
- Talleres "por definir" (Mapas, Ataques del jugador, Items, Mecánicas nuevas, Bugs): solo tareas libres.
- Arquitectura: `docs/architecture/tareas.md` ▸ The web; `TALLERES` en `Web/RedMagicWeb/modules/todo-panel.js`.

Pendiente (lo que el usuario dejó sin definir): **cómo se conecta un apunte con su taller**.

## Ideas ya vistas (prototipo `Web/RedMagicWeb/prototipos/tareas-v2.html`, http://localhost:3000/prototipos/tareas-v2.html)

- El apunte guarda `taller: '<id>'` y opcionalmente un objetivo (`goal: 15`); la fila muestra "👾 Enemigos →" y una
  barra `hechos/objetivo`.
- En el panel del apunte: la ruta (Apunte → 🎨 Diseños → 👾 Montar → ✓ Aprobar) y un botón "🎨 Pedir diseños para
  esto" que abre el taller con el formulario relleno y deja el lote / las tareas enlazados al apunte (`from: <id>`).
- Las tareas del taller muestran "↖ de: 15 enemigos" para volver.

## Preguntas abiertas (decidir con el usuario antes de construir)

1. ¿Cómo se cuenta el progreso? (¿enemigos `aprobado` en `docs/enemigos/` que vengan de ese apunte, o cualquiera?)
2. ¿El vínculo lo pone el usuario (selector de taller en el panel) o se deduce?
3. ¿Al terminar un paso (diseños aceptados) se pasa solo al siguiente taller, o solo se avisa?
4. Cada taller definitivo necesita su formulario, su prompt (`design-prompts.mjs` como modelo) y su skill.
