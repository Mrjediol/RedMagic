# RedMagic — Roadmap al prototipo

2026-09-18 · @Someone

## Objetivo del prototipo

"Listo" para entrar en balanceo cuando exista una run jugable de \~10 minutos:

- \~5 min en Mundo 1, \~5 min en Mundo 2
- Se pueden experimentar todos los sistemas: matar enemigos, probar builds, luchar contra bosses, obtener mejoras entre runs

## Vía A — Lógica (Claude Code / tokens)

Orden por complejidad de prompt necesaria:

1. DONE **Web + tool importer de proyectiles y VFX** — construir la herramienta (parte de Claude Code, no de arte IA), con import a Unity listo para usar por jugador, enemigos y bosses. Primera prioridad ahora mismo.
2. DONE **Dash tipo Skull Slayer** — dash en salto y dash en suelo con cooldowns independientes (permite encadenar 2 dashes casi instantáneos). El más simple, un prompt directo.
3. **Ataques del player** — 2 o 3 ataques con proyectiles y progresión/mejoras pensadas. Depende de tener el arte listo primero.
4. **Sistema de builds/upgrades** — quitar placeholders de items, pensar pasivas únicas y de conjunto, mejoras reales fuera de la run, definir el anvil (tipo de disparo o forma). El más complejo, requiere diseño previo.
5. **Boss 2 — lógica** — ataques únicos y comportamiento del segundo boss (depende de tener también el arte del boss listo, ver Vía B, y el diseño, ver Vía C).

## Vía B — Arte con IA (cuota diaria)

Cuello de botella: ChatGPT es la única IA que da sprites buenos sin fondo, y solo permite 1-2 imágenes al día. Otras IAs cometen errores o dejan contorno mal recortado al quitar fondo.

Prioridad actual:

1. **Enemigos nuevos** — ya hay \~5 que están bien; seguir sumando hasta 5-10 para el prototipo.
2. **Arte del boss 2** — sprites/animaciones únicas del segundo boss.
3. **Sonidos** — dejarlo para lo último, justo antes del prototipo; aún sin decidir la fuente (alternativa: reusar sonidos de juegos viejos, ej. Ratchet & Clank PS2).

## Vía C — Manual (tu tiempo)

1. **Testear los 3 métodos de mapas en serio**: web, Unity a mano, o pedir mapas a la IA y hacer tú las colisiones. Elegir tras probar.
2. **Testear el ciclo de creación de enemigos vía web** — ya funciona bien de punta a punta, pero falta añadir más animaciones para cubrir todos los casos de enemigos posibles.
3. **Boss 2 — diseño y testeo** — pensar su comportamiento/ataques y probarlo en juego (en paralelo con la lógica de Vía A y el arte de Vía B).
4. **Balanceo de números** — cuando ya haya 1-2 builds, 2-3 armas, 5-10 enemigos, los 2 primeros bosses y el sistema de upgrades fuera de la run. Sin buscar perfección.

## Ideas a futuro / backlog

Ideas fuera del alcance del prototipo, apuntadas para no olvidarlas:

- Herramienta en la web para diseñar UI y un importer que las pase a Unity (explorar viabilidad; útil también para futuros juegos)
- Creación de bosses en la web: cada ataque/comportamiento nuevo se guarda como ataque general reusable en futuros bosses
- Herramienta de biblioteca adiciones : 1 - Boton al lado de actualizar para ocultar y mostrar las flechas de mover los campos arriba abajo. 2 - hacer que clickando 2 veces en el enemigo/scene etc, te lo abra, 
  y hacer que en el caso de prefabs, como enemigos, player etc puedas arrastrar a la scena directamente.
  Mejorar Flujo de pedir prompts y hacer removebackground.
  Arreglar bug de que las animacion de los projectiles impact no son limpias, aveces al acabar sale algun artefacto visual.
  Añadir la opcion al projectile? mira al player? de ser asi la punta del projectile debe estar girada para salir siempre mirando al player, el ejemplo seria una flecha, la rotacion deberia de hacer que salga hacia el player este arriba o abajo.
  Bugs : hitbox personaje al suelo, hay que moverse bien- 
  bug las muertes, de personajes y projectiles se bugean, tengo que ver como hago el impacto si que salga despues de morir.