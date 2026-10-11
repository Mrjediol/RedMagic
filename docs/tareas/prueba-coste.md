# Prueba de coste: la misma tarea desde la terminal y desde la cola de la web

**Objetivo**: saber cuánto consume del plan una tarea pequeña (a) pedida en una terminal tras `/clear` y (b) lanzada por la
cola de 📋 Tareas, con el mismo modelo, y comparar. Hipótesis del usuario: el arranque en frío extra de la cola se
compensa con conversaciones cortas, una por tarea.

## La tarea

Variante teñida del caballero, **solo teñida en Unity**, sin regenerar arte. Lado 1 (terminal) la pide **verde**, lado 2
(web) **roja**: mismo trabajo, distinto color y distinto nombre, así que no se pisan y **no hace falta tocar ramas de git**.

Lado 1 (terminal):
> Crea una variante del enemigo FrostKnight de color verde, llamada FrostKnightGreen. Solo teñirlo en Unity: no regeneres arte ni pidas sprites; reutiliza las piezas existentes con un tinte verde (en los SpriteRenderer o con el mecanismo que ya tenga el proyecto). Prefab nuevo en Assets/Prefabs/Enemies/, con el mismo comportamiento, ataques y animaciones. Compila, haz commit y dime qué has hecho y cómo probarlo.

Lado 2 (web):
> Crea una variante del enemigo FrostKnight de color rojo, llamada FrostKnightRed. Solo teñirlo en Unity: no regeneres arte ni pidas sprites; reutiliza las piezas existentes con un tinte rojo (en los SpriteRenderer o con el mecanismo que ya tenga el proyecto). Prefab nuevo en Assets/Prefabs/Enemies/, con el mismo comportamiento, ataques y animaciones. Compila, haz commit y dime qué has hecho y cómo probarlo.

(La cola le añade su encabezado y las reglas de informe: es parte de lo que se mide. Si el lado 2 reutiliza lo que hizo el
lado 1, es otra diferencia a anotar.)

## Condiciones iguales

- **Mismo modelo y esfuerzo**: Sonnet 5.5, esfuerzo medio (`/model sonnet` y `/effort medium` en la terminal; en la web,
  Modelo "Sonnet", Esfuerzo "medio").
- **Unity abierto** en los dos lados (en la web, marcar "necesita Unity").
- **Lado 1 = sesión recién limpiada** (`/clear` o terminal nueva) que no ha leído este documento ni el traspaso.
- **Orden**: primero el lado 1; al acabar, cierra todas las terminales de Claude (la cola espera si hay una abierta) y
  lanza el lado 2 desde la web. Misma rama (`task/humanoid-walker`), un commit por lado.

## Cómo se mide (exacto)

`node Web/RedMagicWeb/tools/taller-server/session-usage.mjs <id de sesión> [<id de sesión>]` lee los transcripts de
`~/.claude/projects/E--redmagic-Proyecto-RedMagic/`. Da por sesión: llamadas a la API, entrada nueva, escritura de caché,
lectura de caché, salida, y "unidades equivalentes" (proxy: entrada + 1,25×escritura + 0,1×lectura + 5×salida). Con dos
sesiones imprime también la comparación.
- Lado 1: tras terminar, `node …session-usage.mjs --last 3` y localizar la sesión (la del título/hora). Si el `/clear` no
  crea una sesión aparte en el transcript, usar `--since <hora del primer mensaje del lado 1>` sobre la sesión larga.
- Lado 2: el id de sesión está en la tarea (`sessionId` en `docs/tareas/todo.json`); el flujo completo en
  `Logs/Taller/<run>.jsonl` (trae además el `rate_limit_event` con el % de uso del plan, 5 h y semanal).
- Opcional pero útil: anotar el % semanal/5 h antes y después de cada lado (la web/CLI lo da; en la terminal, `/usage`).

## Qué hay que informar

Tabla con los dos lados (llamadas, entrada, caché escrita/leída, salida, unidades), la comparación, el resultado de la
tarea en cada lado (¿quedó igual de bien? ¿compila? ¿se ve verde en Unity?) y una conclusión: ¿compensa la cola?,
¿cuánto es el arranque en frío de la cola frente al de una terminal limpia?, ¿qué se puede recortar (por ejemplo, el
índice de memoria, el encabezado de la cola)? Dejarlo como informe de la tarea y en este documento.

## Resultados

Medido con `session-usage.mjs --last 3` (2026-10-03). Sonnet 5.5, esfuerzo medio, Unity abierto.

| | Terminal (verde) | Web (rojo) |
|---|---|---|
| Sesión | `eb06e43b` | `d6d48ba9` |
| Duración | 1,3 min | 0,4 min |
| Llamadas a la API | 14 | 6 |
| Entrada nueva | 28 | 12 |
| Caché escrita | 39.671 | 32.773 |
| Caché leída | 778.086 | 191.437 |
| Salida | 3.746 | 2.241 |
| **Unidades equivalentes** | **146.155** | **71.327** |

- La web gastó ~49 % de la terminal. La cola no añadió coste extra apreciable por su arranque en frío.
- La diferencia sale de la caché leída: más llamadas = más relecturas del contexto.
- Sesgos: la terminal hizo además el commit `wip: pre-task snapshot` y un resumen más largo; es una sola muestra por lado.
- Los % de uso del plan (46→47 % 5 h, 78→79 % semanal) son demasiado gruesos para comparar tareas pequeñas.
- Resultado de la tarea: ambos prefabs creados y commiteados (`FrostKnightGreen` 058662c, `FrostKnightRed` 86b9256).
  Falta que el usuario compruebe en Unity que se ven bien.
- Conclusión: para tareas pequeñas, lanzarlas desde la web no cuesta más que la terminal. Confirmar con 2–3 tareas más.
