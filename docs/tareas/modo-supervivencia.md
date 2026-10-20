# Modo Supervivencia (tarea `modo-supervivencia`)

Pedido del usuario (2026-10-20): una escena grande donde salen oleadas de enemigos; simula el progreso de una run en un solo
mapa y sirve para probar el combate y balancear daño, moneda y todo. Sistema muy flexible, centralizado y cambiable.
Rama `task/supervivencia` (sale de `task/armas-unity`, sin fusionar). Arquitectura: `docs/architecture/survival.md`.

## Hecho (2026-10-20, esqueleto)
- Menú principal: Jugar → Clásico / Supervivencia / Atrás.
- `SurvivalConfig` (un asset con todo), `SurvivalManager` + `SurvivalHud`, `SurvivalSpawnFinder` (reglas por tipo: volador / suelo / torreta /
  jefe), `SurvivalWaveBuilder` (rareza por peso, especiales por dificultad).
- Mundos: Hielo 4 + jefe IceBoss2, Bosque 5 (verde), Volcán 5 (rojo); luego se repite Volcán con el escalado subiendo.
  Enemigos: koros, IceRobot, PolarTurretBot (voladores), IceCannonOrb (volador, poco común), FrostKnight (suelo), Turret.
- Variantes (`EnemyVariant`): Naranja (dif. 140 %, ×1,4, más grande), Morada (200 %, ×2, más pequeña), Dorada (160 %, daño ×3,
  vida ×0,5, con destello), Escudo (120 %, cuelga el escudo; sólo FrostKnight), zonas Bosque y Volcán (sólo color).
- Escalado: vida +12 %/oleada, daño +8 %/oleada (lineal), × por mundo. Dificultad 100 % +20 %/oleada, especial 30 %.
- Entre rondas: tienda (la del juego, altares flotantes) y botón Siguiente ronda (Intro / Select). Item que cae: activable.
- `SurvivalArena`: prefab `Survival` colocado (8 voladores, 6 suelo, 4 torreta, 1 jefe, tienda en el suelo centro).

## Hecho (2026-10-20, segunda tanda)
- ~~Puntos de aparición con botones en el Inspector~~ → sustituidos el mismo día por reglas (`SurvivalSpawnFinder`): con
  puntos fijos salían amontonados (pocos dentro del radio). Ahora al azar alrededor del jugador, 6–15 u, ±8 u de altura,
  3 u de separación entre enemigos; voladores en aire libre, suelo/torretas sobre suelo o plataforma, jefe en el centro.
- Oleadas **dinámicas** (por defecto) además de las simples: máximo a la vez (6 +1/oleada, tope 20), reaparición a los 3 s,
  acaban por tiempo (Hielo 30/45/60/90 s, Bosque 45…120, Volcán 60…120) o por bajas (Hielo 15/20/25/30…).
- Aparecen sólo a ≤ 15 u del jugador y ≥ 4 u (configurable).
- Dificultad Fácil / Normal / Difícil (menú): enemigos a la vez ×1 / ×1,5 / ×2, bajas igual, vida ×1 / ×1,25 / ×1,6, daño
  ×1 / ×1,2 / ×1,5.

## Prueba del usuario (2026-10-20, fin de sesión)
- Torretas (suelo/pared/techo): "parece que están mejor" tras `SurfaceMount`.
- Progresión de armas (núcleos, portador, mejoras permanentes): feedback y pasos en `docs/tareas/armas-meta-upgrades.md`.

## Pendiente / ideas
- Respuestas del usuario a las preguntas de abajo.
- Torretas en pared (girar la base contra la pared): tarea aparte, pedida para después.
- Métricas por oleada a fichero (hoy una línea en consola) para comparar builds.
- `impactDamage` de proyectiles no escala con `IOutgoingDamageScale`.
- Pool de enemigos (hoy Instantiate/Destroy como WaveManager).
- Cuando el usuario lo dé por bueno: mover `Assets/Test/Survival/` a su sitio y fusionar la rama.

## Preguntas abiertas (hechas al usuario el 2026-10-20)
1. Enemigos que aparecen lejos se quedan quietos hasta que el jugador entra en su `detectionRange`. Mitigado: sólo
   aparecen a ≤ `maxDistance` (15 u). ¿Persecución global en Supervivencia o así?
4. Oleada por tiempo al acabarse: hoy dejan de salir y hay que rematar a los que quedan (alternativa en el config:
   desaparecen sin botín). ¿Cuál por defecto?
2. ¿La dificultad (para las especiales) se reinicia en cada mundo o sigue subiendo (así está)?
3. ¿Al morir, se pierde todo (así está) o algo de meta (almas/calaveras)?
