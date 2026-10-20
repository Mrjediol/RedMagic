# Enjambre (`swarm`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras), **sin probar por el usuario** (hecho 2026-10-20 sin él).
Código: `g-enjambre.js`; dibujo en `fx-armas2.js` (`firefly`, `stingFx`, `lanternLight`). Paleta nueva `ember` (ámbar cálido).
Reglas comunes: [README](README.md) · progresión: [progresion.md](progresion.md).

## Rol

**Daño repartido que no se apunta.** Clic = suelta luciérnagas que vuelan hasta donde apuntas, se reparten entre los enemigos
cercanos y los pican varias veces antes de volver. Poco daño por picadura, pero muchas, y siguen trabajando mientras tú haces
otra cosa (con la otra arma en la mano). Las evoluciones la convierten en un **compañero** (Colmena), un **contagio** (Plaga)
o un **apoyo de zona** (Faroles).

## Comportamiento (base)

- Clic = `count` luciérnagas desde el cristal hacia el punto apuntado (`speed`). Al llegar, cada una elige un enemigo a
  `seekRadius` de ese punto (repartidas: la 1.ª al más cercano, la 2.ª al siguiente…) y lo rodea picando cada `stingGap` s
  (`stingDamage`) hasta `stings` picaduras; si muere, cambia al más cercano. Después vuelven y desaparecen.
- Sin nadie cerca del punto, vuelven sin hacer nada.

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Colmena** (`hive`) | Las luciérnagas viven a tu alrededor y atacan SOLAS a cada enemigo que golpeas con CUALQUIER arma; el clic las manda todas a la vez. | **Reina** (`queen`): Una luciérnaga grande más, cuyas picaduras son golpes fuertes.<br>**Cría** (`brood`): Cada baja suma una luciérnaga más durante un rato.<br>**Néctar** (`nectar`): Cada picadura recarga un poco tu otra arma. |
| **Plaga** (`plague`) | Las luciérnagas se METEN dentro del enemigo y lo queman por dentro; si muere, saltan al siguiente (y una más). | **Fiebre** (`fever`): Los infestados reciben más daño de todo.<br>**Eclosión** (`hatch`): Al morir un infestado, estalla.<br>**Contagio** (`spread`): Cada poco, un infestado contagia a un vecino. |
| **Faroles** (`lantern`) | El clic planta un farol de luciérnagas: ILUMINA a los enemigos cercanos (tus golpes de cualquier arma pueden ser críticos ×2) y les pica despacio. | **Polillas** (`moth`): Los faroles atraen a los enemigos hacia su luz.<br>**Bengala** (`flare`): Al apagarse, el farol estalla y aturde.<br>**Luz guía** (`guide`): Tus proyectiles que pasan cerca de un farol se curvan hacia los iluminados. |
<!-- /gen:tree -->

Detalle:
- **Colmena**: `hiveCount` luciérnagas viven girando a tu alrededor (se reponen solas). Cada golpe de **tus otras armas**
  manda luciérnagas libres a ese enemigo (1 por cada 15 de daño del golpe, máx. 3) que pican `hiveStings` veces y vuelven.
  **Funciona con el Enjambre guardado.** El clic manda a todas las libres a donde apuntas.
  - Reina: una más, grande (×1,6), cuyas picaduras son golpe fuerte ×`queenMul`. Cría: cada baja suma una luciérnaga
    `broodTime` s (máx. `broodMax`). Néctar: cada picadura quita `nectarRefund` s a la recarga de tu otra arma.
- **Plaga**: las luciérnagas se **meten dentro** del enemigo (puntito que late): `infestDps` cada una durante `infestTime` s.
  Si el huésped muere, saltan al más cercano a 260 px y nace una más (una vez por luciérnaga; máx. `spreadMax` en total).
  - Fiebre: los infestados son vulnerables ×`feverVuln`. Eclosión: al morir un infestado, estalla (`hatchDamage`,
    `hatchRadius`). Contagio: cada `spreadEvery` s un infestado pasa una luciérnaga a un vecino.
- **Faroles**: el clic planta un farol (luz cálida de `lanternRadius`, `lanternTime` s, máx. `lanternMax`; el más viejo se
  apaga). Los enemigos dentro quedan **iluminados**: cualquier golpe de arma suyo tiene `litCrit` de ser **CRÍTICO ×2**
  (regla nueva en `damage`). Además el farol les pica despacio (`lanternDps`).
  - Polillas: atrae a los enemigos hacia la luz (`mothSpeed`). Bengala: al apagarse estalla y aturde 0,6 s.
    Luz guía: tus proyectiles con velocidad que pasan cerca (1,6 × radio) giran hacia el iluminado más cercano (`guideTurn`).

## Elemento (con 4 de ese elemento) y Duplicador

- **Hielo**: cada picadura congela `iceFreeze` (muchas picaduras → congelado rápido; la Reina lo rompe).
- **Fuego**: cada picadura quema `fireBurn`.
- **Duplicador**: el doble de luciérnagas (en Colmena, el doble viviendo contigo; los faroles llevan más).

## Sinergias

- **Colmena + armas de golpe grande** (Golpe estelar, Jabalina cargada): cada golpe manda 3. **+ Néctar**: tus armas lentas
  recargan con cada picadura.
- **Plaga + Reloj · Estasis / Látigo · Cadena**: el daño continuo se guarda o se reparte.
- **Faroles + Abanico** (Luz guía): las esquirlas se curvan hacia los iluminados; con hielo, los críticos rompen.
- Las picaduras son golpes de arma: marcan con el Cazador, activan el Pararrayos y el Eco.

## Decisiones

- 2026-10-20 · Creada sin el usuario. Colmena: al principio acudía 1 luciérnaga por golpe; con armas de un golpe grande apenas
  se notaba → 1 por cada 15 de daño (máx. 3). Regla nueva «iluminado» (`e.litT`, `e.litChance`) en `damage`.

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 2.2 | 0.2 – 8 (paso 0.1) | Base: recarga (s) |
| `count` | 4 | 1 – 12 (paso 1) | Base: luciérnagas por clic |
| `speed` | 380 | 100 – 900 (paso 10) | Base: velocidad |
| `stingDamage` | 5 | 1 – 40 (paso 1) | Base: daño por picadura |
| `stingGap` | 0.3 | 0.05 – 1 (paso 0.01) | Base: s entre picaduras |
| `stings` | 4 | 1 – 12 (paso 1) | Base: picaduras antes de volver |
| `seekRadius` | 160 | 30 – 400 (paso 5) | Base: radio en el que buscan enemigos desde donde apuntas |
| `iceFreeze` | 0.12 | 0 – 0.6 (paso 0.01) | Hielo: congelación por picadura |
| `fireBurn` | 1 | 0 – 5 (paso 0.1) | Fuego: quemadura por picadura (s) |
| `hiveCount` | 5 | 1 – 12 (paso 1) | Colmena: luciérnagas |
| `hiveStings` | 2 | 1 – 8 (paso 1) | Colmena: picaduras al acudir solas |
| `queenMul` | 3 | 1 – 8 (paso 0.1) | Colmena · Reina: × daño |
| `broodTime` | 10 | 1 – 30 (paso 0.5) | Colmena · Cría: s que dura la extra |
| `broodMax` | 6 | 1 – 12 (paso 1) | Colmena · Cría: extras máx. |
| `nectarRefund` | 0.08 | 0 – 0.5 (paso 0.01) | Colmena · Néctar: s por picadura |
| `infestDps` | 10 | 1 – 50 (paso 1) | Plaga: daño/s por luciérnaga dentro |
| `infestTime` | 5 | 1 – 15 (paso 0.5) | Plaga: s dentro |
| `feverVuln` | 1.25 | 1 – 3 (paso 0.05) | Plaga · Fiebre: × daño que reciben |
| `hatchDamage` | 30 | 0 – 150 (paso 1) | Plaga · Eclosión: daño |
| `hatchRadius` | 80 | 20 – 250 (paso 5) | Plaga · Eclosión: radio |
| `spreadEvery` | 1.5 | 0.3 – 5 (paso 0.1) | Plaga · Contagio: cada (s) |
| `spreadMax` | 12 | 2 – 30 (paso 1) | Plaga · Contagio: luciérnagas máx. en total |
| `lanternTime` | 8 | 1 – 20 (paso 0.5) | Faroles: duración (s) |
| `lanternRadius` | 140 | 40 – 300 (paso 5) | Faroles: radio de luz |
| `lanternMax` | 3 | 1 – 6 (paso 1) | Faroles: a la vez |
| `litCrit` | 0.2 | 0 – 1 (paso 0.01) | Faroles: probabilidad de crítico ×2 a los iluminados |
| `lanternDps` | 6 | 0 – 40 (paso 1) | Faroles: daño/s a los de dentro |
| `mothSpeed` | 70 | 0 – 300 (paso 5) | Faroles · Polillas: atracción |
| `flareDamage` | 35 | 0 – 150 (paso 1) | Faroles · Bengala: daño |
| `flareRadius` | 110 | 20 – 300 (paso 5) | Faroles · Bengala: radio |
| `guideTurn` | 6 | 0 – 20 (paso 0.5) | Faroles · Luz guía: giro de tus proyectiles (rad/s) |
<!-- /gen:tuning -->
