# Vida cerca de la vista sin tapar el mapa

## Objetivo
Que el jugador lea su vida sin desviar la mirada a la esquina superior izquierda, sin que el HUD
tape mapa ni enemigos.

## Estado (2026-10-07)
- HUD unificado hecho (`Assets/Ui/Hud/`, `docs/architecture/ui.md`): vida + cooldowns arriba a la
  izquierda, monedas arriba a la derecha, posiciones movibles por el jugador (Opciones ▸ Interfaz).
- El usuario descartó por ahora "abajo en el centro" (estilo LoL) por miedo a tapar el mapa.
- Ya hay feedback periférico: viñeta roja al recibir daño y con vida baja (≤25 %).

## Ideas a valorar
- Mini-barra junto al personaje que sólo aparece unos segundos tras recibir daño / curar, y se
  desvanece (la del HUD queda como referencia fija).
- Barra abajo-centro muy fina y translúcida que se vuelve opaca sólo al cambiar la vida o con vida baja.
- Atenuar el HUD (alpha) cuando el jugador o un enemigo pasa por detrás de él.
- Arco de vida alrededor del personaje (estilo Dead Cells / Hades) sólo en combate.

## Decisiones abiertas
Cuál de las ideas probar primero; decidir jugando.
