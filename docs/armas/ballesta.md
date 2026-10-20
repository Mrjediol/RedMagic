# Ballesta de cristal (`bow`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras), **sin probar por el usuario** (hecho 2026-10-20 sin él).
Código: `g-ballesta.js`; dibujo en `fx-armas2.js` (`laserSight`, `crystalBolt`, `weakSpot`). Reglas comunes: [README](README.md) ·
progresión: [progresion.md](progresion.md).

## Rol

**Precisión.** Mantén para apuntar (mira láser punteada; vas más lento) — cuanto más esperas, más daño (`focusTime`); suelta:
virote muy rápido y recto. Si das en la **parte de arriba del enemigo** (el «ojo») es **CRÍTICO**. Recompensa apuntar bien;
con la concentración llena, golpe fuerte (rompe congelados).

## Comportamiento (base)

- Mantener = concentración 0→1 en `focusTime` s (pitido al llegar al máximo; la mira se vuelve blanca y con destello).
- Soltar: virote a `speed`, daño `dmgMin`→`dmgMax`; crítico ×`critMul` si da en la `critZone` de arriba del cuerpo.
- Virote **capturable** (Singularidad), lo desvían el Espejo y lo acelera la Corriente.

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Perforador** (`pierce`) | El virote atraviesa a TODOS los de la línea y gana daño con cada uno que atraviesa. | **Calibre** (`caliber`): Con la concentración llena, el virote es enorme y lanza a los que atraviesa.<br>**Cadencia** (`cadence`): Cada baja deja el siguiente disparo ya concentrado.<br>**Ráfaga** (`volley`): Con la concentración llena dispara tres virotes seguidos. |
| **Rebote** (`ricochet`) | El virote rebota en el suelo y las plataformas (la mira enseña el recorrido); cada rebote suma daño y lo que golpea después de rebotar es crítico. | **Más rebotes** (`more`): Dos rebotes más.<br>**Carambola** (`billiard`): También rebota en los enemigos, hacia el siguiente más cercano.<br>**Astillas** (`splinter`): Cada rebote suelta dos astillas pequeñas. |
| **Marcador** (`spotter`) | El virote deja un PUNTO DÉBIL: los siguientes golpes de tu OTRA arma a ese enemigo son críticos. | **Puntos débiles** (`spots`): El punto débil aguanta más golpes.<br>**Exponer** (`expose`): El marcado recibe además más daño de todo.<br>**Recompensa** (`bounty`): Matar a un marcado recarga del todo tu otra arma y deja el siguiente virote concentrado. |
<!-- /gen:tree -->

Detalle:
- **Perforador**: atraviesa a todos los de la línea (la mira no se para en el primero), +`pierceUp` por cada uno atravesado.
  - Calibre: con concentración llena, virote enorme que lanza (`caliberPush`) a los que atraviesa. Cadencia: cada baja deja el
    siguiente disparo concentrado al instante. Ráfaga: con concentración llena, tres virotes seguidos (`volleyGap`).
- **Rebote**: rebota `bounces` veces en suelo y plataformas (la mira dibuja el recorrido con los puntos de rebote); +`bounceUp`
  por rebote y **todo golpe después de rebotar es crítico** → tiros de habilidad por debajo de los enemigos.
  - Más rebotes: +2. Carambola: también rebota en los enemigos hacia el siguiente más cercano. Astillas: cada rebote suelta dos
    astillas (`splinterDamage`).
- **Marcador**: el virote deja un **punto débil** (diana que gira, ×N): los siguientes `weakHits` golpes de **tu otra arma** a
  ese enemigo son críticos ×`critMul` (dura `weakTime` s). Es el arma de «marcar y cambiar».
  - Puntos débiles: +`spotsHits` golpes. Exponer: el marcado además recibe ×`exposeVuln` de todo. Recompensa: matar a un
    marcado recarga del todo la otra arma y deja el siguiente virote concentrado.

## Elemento (con 4 de ese elemento) y Duplicador

- **Hielo**: el virote congela `iceFreeze`; el siguiente concentrado lo rompe. **Fuego**: quema `fireBurn`.
- **Duplicador**: segundo virote 0,08 s después (casi la misma línea).

## Sinergias

- **Marcador + armas de muchos golpes** (Abanico, Enjambre, Rayo): cada golpe crítico mientras dure.
- **Marcador + Golpe estelar / Mazo**: el golpe grande cae crítico.
- **Rebote + Espejo**: el virote rebota en el suelo, cruza el espejo y busca al enemigo.
- **Perforador + Ráfaga · Corriente / Tótem · Señuelo**: enemigos en fila.

## Decisiones

- 2026-10-20 · Creada sin el usuario. Mira más gruesa y brillante tras verla en el navegador (casi no se veía).

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 0.35 | 0.05 – 3 (paso 0.05) | Base: recarga (s) |
| `focusTime` | 0.9 | 0.1 – 3 (paso 0.05) | Base: s para concentración máxima |
| `aimMove` | 0.5 | 0.1 – 1 (paso 0.05) | Base: × tu velocidad apuntando |
| `dmgMin` | 18 | 1 – 100 (paso 1) | Base: daño sin concentrar |
| `dmgMax` | 90 | 1 – 300 (paso 1) | Base: daño con concentración llena (golpe fuerte) |
| `speed` | 1800 | 400 – 3000 (paso 10) | Base: velocidad del virote |
| `critMul` | 1.8 | 1 – 4 (paso 0.05) | Base: × crítico (parte de arriba del enemigo) |
| `critZone` | 0.35 | 0 – 1 (paso 0.05) | Base: zona de crítico (fracción de arriba del cuerpo) |
| `knock` | 120 | 0 – 600 (paso 5) | Base: empuje |
| `iceFreeze` | 0.5 | 0 – 1 (paso 0.05) | Hielo: congelación |
| `fireBurn` | 2 | 0 – 6 (paso 0.1) | Fuego: quemadura (s) |
| `pierceUp` | 0.25 | 0 – 1 (paso 0.05) | Perforador: + daño por enemigo atravesado |
| `caliberPush` | 700 | 0 – 1500 (paso 10) | Perforador · Calibre: empuje (lanzado) |
| `volleyGap` | 0.09 | 0.03 – 0.4 (paso 0.01) | Perforador · Ráfaga: s entre virotes |
| `bounces` | 2 | 0 – 8 (paso 1) | Rebote: rebotes |
| `bounceUp` | 0.3 | 0 – 1 (paso 0.05) | Rebote: + daño por rebote |
| `splinterDamage` | 10 | 1 – 50 (paso 1) | Rebote · Astillas: daño |
| `weakHits` | 3 | 1 – 10 (paso 1) | Marcador: golpes críticos que aguanta el punto débil |
| `weakTime` | 6 | 1 – 15 (paso 0.5) | Marcador: s que dura |
| `spotsHits` | 3 | 0 – 10 (paso 1) | Marcador · Puntos débiles: golpes extra |
| `exposeVuln` | 1.25 | 1 – 3 (paso 0.05) | Marcador · Exponer: × daño que recibe |
<!-- /gen:tuning -->
