# Prueba de coste: la misma tarea desde la terminal y desde la cola de la web

**Objetivo**: saber cuánto consume del plan una tarea pequeña (a) pedida en una terminal tras `/clear` y (b) lanzada por la
cola de 📋 Tareas, con el mismo modelo, y comparar. Hipótesis del usuario: el arranque en frío extra de la cola se
compensa con conversaciones cortas, una por tarea.

## La tarea

Variante verde del caballero, **solo teñida en Unity**, sin regenerar arte:

> Crea una variante del enemigo FrostKnight de color verde, llamada FrostKnightGreen. Solo teñirlo en Unity: no
> regeneres arte ni pidas sprites; reutiliza las piezas existentes con un tinte verde (en los SpriteRenderer o con el
> mecanismo que ya tenga el proyecto). Prefab nuevo en Assets/Prefabs/Enemies/Procedural/, con el mismo comportamiento,
> ataques y animaciones. Compila, haz commit y dime qué has hecho y cómo probarlo.

Misma frase en los dos lados (la cola le añade su propio encabezado y las reglas de informe: es parte de lo que se mide).

## Condiciones iguales

- **Mismo modelo y esfuerzo**: Sonnet 5.5, esfuerzo medio (`/model sonnet`, `/effort medium` en la terminal; en la web,
  Modelo "Sonnet", Esfuerzo "medio").
- **Mismo punto de partida**: la **base** es el último commit de la rama `task/humanoid-walker` en el momento de empezar.
  Cada lado en su rama desde la base, para que el segundo no vea el resultado del primero:
  - Lado 1 (terminal): `git switch -c task/prueba-terminal`, trabajar, commit.
  - Vuelve a la base: `git switch task/humanoid-walker`.
  - Lado 2 (web): `git switch -c task/prueba-web`, lanzar la tarea desde la web (la cola trabaja en la rama actual).
  - Al final el usuario decide qué rama conserva; no se borra ninguna (CLAUDE.md: nunca borrar ramas).
- **Unity abierto** en los dos lados (en la web, marcar "necesita Unity"; en la terminal ya está).
- **Sin `/clear` previo cargado de contexto**: el lado 1 es una sesión recién limpiada que solo lee lo imprescindible
  (CLAUDE.md, memoria) y esta tarea. No se le da este documento ni el traspaso antes de pedírsela.
- Mientras se hace el lado 2, la terminal de Claude debe estar cerrada (la cola espera si no; "Lanzar igualmente" existe
  pero ensucia la medida).

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

_(pendiente)_
