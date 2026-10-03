# Pestaña Enemigos de la web: pendientes, diseño y make-enemy

Decisiones del usuario (2026-10-03). Se hace en la misma sesión, con Opus, en 4 fases (una por sesión/commit).

## Estructura pedida

**A. Enemigos existentes con pendientes** — listas plegables por enemigo. Tarjeta = imagen del enemigo (sprite general,
solo referencia visual) + etiquetas de lo pendiente: comportamiento, sprite, gesto, ataque, bug. Pulsar la tarjeta lanza
una tarea con ese contexto. Se puede marcar un pendiente desde la web o desde Unity. Maqueta de referencia: la rejilla
de tarjetas plegables de la Biblioteca (Proyectiles (13), Impactos…).

**B. Crear enemigos — dos flujos claramente separados**
1. **Diseño**: el usuario escribe solo las condiciones ("tortuga voladora") y la cantidad (5). El prompt se completa solo
   con las reglas de arte del proyecto (bordes negros, estilo Fracture, etc.; la skill make-enemy ya las tiene: reutilizar,
   no duplicar). Al acabar, galería con los diseños: aceptar / descartar uno a uno, aceptar todos, descartar todos. Los
   aceptados van a la biblioteca y crean una tarea pendiente de make-enemy.
2. **Funcional (make-enemy)**: el enemigo montado en Unity. La tarea queda "hecha, esperando aprobación": aprobar,
   descartar al enemigo, o "tiene pendientes" (pasa a A).

## Decisiones

- **N diseños**: ofrecer los dos modos (una tarea con N variantes / N tareas, una por diseño) con un selector, para que el
  usuario compare cuál sale más barato con `session-usage.mjs` (la siguiente prueba de coste).
- **Pendientes por enemigo**: archivo aparte (`docs/enemigos/<nombre>.json`), no en `todo.json`.
- **Visual**: solo el sprite general del enemigo; los gestos/ataques los prueba el usuario en Unity.
- **Modelo**: Opus para arquitectura (fase 1 y esqueleto); Sonnet para retoques.

## Fases

1. Galería de resultados (salida de Comfy) con aceptar/descartar individual y masivo; aceptados → biblioteca.
2. Flujo Diseño: formulario (condiciones + cantidad + modo), prompt automático con las reglas, lanza la tarea.
3. Flujo Funcional: tarea make-enemy, estado "esperando aprobación", aprobar / descartar / con pendientes.
4. Lista de enemigos existentes con pendientes (plegable, tarjetas, lanzar tarea desde la tarjeta).

## Estado

- **Fase 1 hecha** (2026-10-03): pestaña 👾 Enemigos, lotes en `docs/enemigos/lotes/`, biblioteca en
  `docs/enemigos/<Name>/`. Diseño técnico: `docs/architecture/enemigos-web.md`. Dos lotes de prueba (`demo: true`).
  Al aceptar, el enemigo queda `por-montar` en la biblioteca (la tarea make-enemy desde ahí = fase 3).
- Siguiente: fase 2.
