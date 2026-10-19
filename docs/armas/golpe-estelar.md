# Golpe estelar (`star`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras) en prueba. Código: `g-golpe-estelar.js` (jugable),
`a6-golpe-estelar.js` (clips de la primera versión). Reglas comunes: [README](README.md) · progresión: [progresion.md](progresion.md).

## Rol

Señal al cielo, círculo rúnico que avisa y lanza de luz que golpea **toda la columna** (voladores incluidos). Mucho daño y mucho
retraso. Evoluciones: **Bombardeo** (fila de lanzas), **Cazador** (cae sola sobre los marcados por tu otra arma), **Tormenta** (zona).

## Comportamiento (base)

- Clic = marca el suelo en la x del ratón; el círculo se cierra durante `delay` s mientras crece un destello arriba.
- La lanza cae en 0,07 s: a todo enemigo cuya x esté en la columna (`width`) le hace `damage`, golpe fuerte, hit-stop, impact
  frames; los voladores alcanzados se estampan. Onda alrededor (`shockRadius`, sólo cerca del suelo): `shockDamage`, sin reacciones.

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Bombardeo** (`barrage`) | Lanzas más pequeñas en fila desde donde apuntas hacia delante. | **Salva** (`salvo`): +2 lanzas en la fila.<br>**Final** (`finale`): La última lanza es enorme (doble de ancho y de daño).<br>**Rodante** (`rolling`): La fila sigue al ratón mientras va cayendo. |
| **Cazador** (`hunter`) | Tus otras armas marcan al golpear; las lanzas caen solas sobre los marcados (sin apuntar). | **Acumulación** (`stack`): Las marcas se apilan hasta 3: cada una suma daño a la lanza.<br>**Ejecución** (`exec`): Remata a los que quedan con poca vida y marca al enemigo más cercano.<br>**Vigilancia** (`watch`): Cada pocos segundos cae una lanza sola sobre un marcado, aunque lleves la otra arma. |
| **Tormenta** (`storm`) | Lluvia de agujas en diagonal sobre una zona ancha. | **Diluvio** (`deluge`): Más agujas, dura el doble y la zona sigue al ratón.<br>**Agujas pesadas** (`heavyN`): Cada aguja es golpe fuerte (rompe congelados) y hace más daño.<br>**Ojo de la tormenta** (`eye`): La lluvia atrae hacia el centro y acaba con una lanza en medio. |
<!-- /gen:tree -->

Detalle:
- **Bombardeo**: recarga ×1,15. `barrageCount` lanzas (`barrageDamage`, `barrageWidth`, a 0,75 de tamaño) desde donde apuntas
  hacia delante (lejos del player), separadas `barrageGap` px y `barrageStep` s.
  - Salva: +2 lanzas. Final: la última ×`finaleMul` de ancho, daño y tamaño. Rodante: cada lanza sigue al ratón (con su
    separación) hasta que empieza a caer.
- **Cazador** (sistema de marcas): con esta evolución equipada, **los golpes de tus otras armas marcan** (8 s; rombo encima). Al
  pulsar, si hay marcados, cae una lanza sobre cada uno (hasta `maxTargets`, del más cercano al más lejano, cada `chainGap` s, con
  el aviso más corto) siguiéndolos; consume las marcas. Sin marcados, cae donde apuntas.
  - Acumulación: las marcas se apilan hasta 3 (una por ataque, no por esquirla); la lanza hace +`stackMul` por marca extra (×N
    encima del rombo).
  - Ejecución: si tras la lanza le queda menos de `execThreshold` de vida, lo remata; al morir alguien por una lanza, marca al
    enemigo más cercano (≤ 300 px) → cadenas.
  - Vigilancia: cada `watchEvery` s cae una lanza sola sobre un marcado al azar, **aunque lleves la otra arma**.
- **Tormenta**: paleta oro, recarga ×1,15. `rainNeedles` agujas en diagonal repartidas en `rainWidth` px durante `rainTime` s;
  cada una golpea lo primero que corta (`needleDamage`).
  - Diluvio: ×`delugeMul` agujas, el doble de duración y la zona sigue al ratón. Agujas pesadas: ×`heavyMul` de daño y golpe
    fuerte. Ojo de la tormenta: atrae hacia el centro (`eyePull`) mientras dura y acaba con una lanza normal en medio.

## Elemento (sinergia: con 4 items de ese elemento) y Duplicador (item)

- **Hielo**: los de la columna se congelan al instante; la onda congela a medias en `iceRadius`; nova de escarcha en el impacto.
- **Fuego**: cráter ardiendo (`craterTime`, `craterBurn`) donde cae cada lanza; contra congelados, deshielo.
- **Duplicador**: cada lanza cae doble (a ±0,9 de su ancho); la Tormenta, el doble de agujas.
- **Constelación** (item) usa esta lanza en pequeño (`miniDamage`, ancho 24) aunque no lleves el Golpe estelar.

## Sinergias

- **Cazador + arma rápida** (Rayo, Abanico Lanza): marcas rápido y rematas sin apuntar; con Acumulación + Ejecución, cadenas.
- **Singularidad**: junta y quieta a la presa en una columna → una lanza alcanza a todos.
- **Hielo**: congela la columna; un golpe fuerte de la otra arma los rompe.

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 3 | 0.3 – 10 (paso 0.1) | Base: recarga (s) |
| `delay` | 0.6 | 0.1 – 2 (paso 0.05) | Base: aviso hasta que cae (s) |
| `damage` | 70 | 1 – 300 (paso 1) | Base: daño de la lanza |
| `width` | 40 | 10 – 120 (paso 2) | Base: ancho de la columna |
| `shockDamage` | 15 | 0 – 100 (paso 1) | Base: onda, daño |
| `shockRadius` | 120 | 0 – 300 (paso 5) | Base: onda, radio |
| `iceRadius` | 80 | 0 – 250 (paso 5) | Hielo: radio que congela al caer |
| `craterTime` | 1.3 | 0.2 – 5 (paso 0.1) | Fuego: duración del cráter (s) |
| `craterBurn` | 2 | 0 – 6 (paso 0.1) | Fuego: quemadura (s) |
| `barrageCount` | 3 | 1 – 10 (paso 1) | Bombardeo: lanzas |
| `barrageGap` | 70 | 20 – 200 (paso 2) | Bombardeo: separación (px) |
| `barrageStep` | 0.12 | 0.02 – 0.5 (paso 0.01) | Bombardeo: tiempo entre lanzas (s) |
| `barrageDamage` | 45 | 1 – 200 (paso 1) | Bombardeo: daño por lanza |
| `barrageWidth` | 30 | 10 – 100 (paso 2) | Bombardeo: ancho |
| `finaleMul` | 2 | 1 – 5 (paso 0.1) | Bombardeo · Final: × tamaño y daño |
| `maxTargets` | 5 | 1 – 10 (paso 1) | Cazador: objetivos máx. |
| `chainGap` | 0.15 | 0.03 – 0.6 (paso 0.01) | Cazador: tiempo entre lanzas (s) |
| `stackMul` | 0.6 | 0 – 2 (paso 0.05) | Cazador · Acumulación: + daño por marca extra |
| `execThreshold` | 0.25 | 0 – 0.6 (paso 0.01) | Cazador · Ejecución: remata por debajo de (× vida) |
| `watchEvery` | 4 | 1 – 15 (paso 0.5) | Cazador · Vigilancia: cada (s) |
| `rainNeedles` | 15 | 3 – 40 (paso 1) | Tormenta: agujas |
| `rainWidth` | 170 | 40 – 400 (paso 5) | Tormenta: ancho |
| `rainTime` | 0.55 | 0.1 – 2 (paso 0.05) | Tormenta: duración (s) |
| `needleDamage` | 12 | 1 – 60 (paso 1) | Tormenta: daño por aguja |
| `delugeMul` | 1.6 | 1 – 4 (paso 0.1) | Tormenta · Diluvio: × agujas |
| `heavyMul` | 1.5 | 1 – 4 (paso 0.1) | Tormenta · Agujas pesadas: × daño |
| `eyePull` | 140 | 0 – 400 (paso 5) | Tormenta · Ojo: fuerza de atracción |
| `miniDamage` | 25 | 1 – 100 (paso 1) | Constelación (item): daño de la lanza pequeña |
<!-- /gen:tuning -->

## Visual

Señal vertical, `runeCircle`, destello arriba, `starLance` con agujas de velocidad, `groundImpact` + `cracks`; cráter
(`fireZone` de `game.js`), `frostNova`, `rainNeedle`.

## Sonido

`star_up`, `star_mark`, `star_fall`, `star_impact`, `star_needle`, `star_markon`.

## Port a Unity (plan)

Habilidad diferida con recarga larga y árbol propio. Las marcas son un sistema genérico (estado en el enemigo + «golpe de otra
arma marca» mientras haya un consumidor equipado), no lógica de esta arma.

## Decisiones

- 2026-10-19 · La lanza golpea toda la columna, también a voladores.
- 2026-10-19 · Las marcas pasan de mod a evolución (Cazador); Lluvia → evolución Tormenta; Fuego → item (cráter = su expresión).

## Preguntas abiertas

- ¿Constelación como transformación (3 items astrales) en vez de item suelto?
