# Pestaña 🎨 Designs de la web: propuestas por aprobar, enemigos con pendientes y make-enemy

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
- **Pestaña aparte "🎨 Designs"** (no dentro de Tareas). Las propuestas por revisar son de 4 tipos: enemigos,
  proyectiles, efectos VFX y sonidos. Al aceptar: enemigo → biblioteca de enemigos (por montar); proyectil/efecto →
  📚 Biblioteca de la web; sonido → paleta del juego (Biblioteca Jere ▸ Sounds, para calificarlo).

## Fases

1. Galería de resultados (salida de Comfy) con aceptar/descartar individual y masivo; aceptados → biblioteca.
2. Flujo Diseño: formulario (tipo: enemigo / proyectil / efecto / sonido + condiciones + cantidad + modo), prompt
   automático con las reglas de cada tipo, lanza la tarea; la tarea acaba escribiendo un lote en `docs/disenos/`.
3. Flujo Funcional: tarea make-enemy, estado "esperando aprobación", aprobar / descartar / con pendientes.
4. Lista de enemigos existentes con pendientes (plegable, tarjetas, lanzar tarea desde la tarjeta).

## Estado

- **Fase 1 hecha** (2026-10-03): pestaña 🎨 Designs, lotes en `docs/disenos/` con los 4 tipos, biblioteca de
  enemigos en `docs/enemigos/<Name>/`. Diseño técnico y formato del lote: `docs/architecture/designs.md`. Cuatro lotes
  de prueba (`demo: true`). Al aceptar, el enemigo queda `por-montar` (la tarea make-enemy desde ahí = fase 3).
- **Fases 2–4 hechas** (2026-10-03, sin parar, a la espera de la prueba del usuario): formulario "Pedir propuestas"
  (tipo, condiciones, cantidad, una tarea o una por propuesta, modelo/esfuerzo) + skill `design-proposals` + comprobador
  `check-design-batch.mjs`; enemigos con estados (montar con make-enemy, aprobar, descartar, con pendientes), ficha con
  pendientes y lanzar tarea; "＋ Enemigo existente" y Unity ▸ Tools ▸ RedMagic ▸ Enemigos ▸ Pendiente para la web.
- Pendiente: el feedback del usuario y la 2ª prueba de coste (pedir enemigos: una tarea con 5 frente a 5 tareas).
