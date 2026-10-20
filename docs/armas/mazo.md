# Mazo de cristal (`hammer`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras), **sin probar por el usuario** (hecho 2026-10-20 sin él).
Código: `g-mazo.js`; dibujo en `fx-armas2.js` (`crystalHammer`, `groundWave`). Reglas comunes: [README](README.md) ·
progresión: [progresion.md](progresion.md).

## Rol

**Cuerpo a cuerpo pesado.** Mantén para levantar el mazo (brilla al máximo), suelta para estamparlo delante de ti: golpe
fuerte (rompe congelados), lanza a los de delante hacia arriba y una onda empuja a los de alrededor. En el aire, soltar =
**picado**: caes en vertical y golpeas donde aterrizas, más fuerte cuanto más alto saltaste. El arma que pide moverse.

## Comportamiento (base)

- Mantener = carga (`chargeTime`). Soltar en el suelo: golpe a 60 px delante, ancho `slamWidth` (+30 con carga), daño
  `dmgMin`→`dmgMax`, golpe fuerte, impact frame con carga llena; voladores dentro (hasta 170 px por encima) se estampan.
  Onda a `shockRadius` por cada lado (`shockDamage`, empuje, sólo a los de suelo).
- Soltar en el aire: picado a `diveSpeed`; al tocar suelo, golpe con +`fallBonus` por cada 100 px de caída.

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Terremoto** (`quake`) | El golpe suelta una onda que corre por el suelo hacia delante y lanza por los aires a todos los de suelo. | **Réplica** (`after`): Una segunda onda sale poco después.<br>**Fisura** (`fissure`): La onda deja una grieta que hace daño un rato.<br>**Doble frente** (`both`): La onda sale hacia los dos lados. |
| **Meteoro** (`meteor`) | Todo golpe empieza con un salto y cae en picado: cuanto más alto caes, más fuerte y más ancho. | **Cráter** (`crater`): El impacto deja un hoyo que atrae a los enemigos un momento.<br>**Rebote** (`bounce`): Tras el impacto rebotas y caes otra vez (un golpe extra).<br>**Gravedad** (`gravity`): Los golpeados quedan pesados: los voladores caen al suelo y todos van lentos un rato. |
| **Sismo** (`tremor`) | Tras el golpe el suelo queda temblando: cada golpe de tu OTRA arma a un enemigo que lo pisa provoca una réplica a su alrededor. | **Temblor largo** (`long`): El suelo tiembla el doble de tiempo.<br>**Réplica fuerte** (`strong`): Las réplicas son golpes fuertes (rompen congelados).<br>**Desequilibrio** (`topple`): Las réplicas aturden. |
<!-- /gen:tree -->

Detalle:
- **Terremoto**: cada golpe suelta una onda que corre por el suelo hacia donde miras (`waveSpeed`, `waveRange`): a cada
  enemigo de suelo, `waveDamage` (golpe fuerte), lo lanza hacia arriba (`waveLift`) y lo aturde `waveStun`.
  - Réplica: otra onda `afterDelay` s después (×`afterMul`). Fisura: la onda deja grietas que hacen `fissureDps` `fissureTime` s.
    Doble frente: la onda sale a los dos lados.
- **Meteoro**: en el suelo, soltar = salto (`hopSpeed`) y picado automático; el picado suma `meteorFall` por cada 100 px y el
  golpe se ensancha con la caída.
  - Cráter: hoyo que atrae (`craterPull`, `craterRadius`) `craterTime` s. Rebote: tras el golpe rebotas y caes otra vez
    (×`bounceMul`). Gravedad: los golpeados quedan **pesados** `heavyTime` s: van a ×`heavySlow` y los voladores se quedan en el suelo.
- **Sismo**: el golpe apenas empuja y deja el suelo **temblando** `tremorTime` s (±`tremorWidth`). Cada golpe de **tu otra
  arma** a un enemigo de suelo dentro provoca una **réplica** alrededor de él (`tremorDamage`, `tremorRadius`; como mucho una
  cada `tremorGap` s por enemigo).
  - Temblor largo: el doble de tiempo. Réplica fuerte: las réplicas son golpe fuerte. Desequilibrio: aturden `toppleStun`.

## Elemento (con 4 de ese elemento) y Duplicador

- **Hielo**: el golpe congela `iceFreeze` (y como es golpe fuerte, rompe a los ya congelados → el segundo golpe hace añicos).
- **Fuego**: quema y deja el suelo ardiendo del ancho del golpe.
- **Duplicador**: un segundo golpe 0,15 s después, 90 px más adelante (×0,6).

## Sinergias

- **Hielo**: golpe fuerte garantizado → congelas con la otra arma y rompes con el mazo.
- **Terremoto / Meteoro · Gravedad + Golpe estelar**: todos aturdidos o en el suelo, en fila.
- **Sismo + armas de muchos golpes** (Abanico, Enjambre, Látigo): cada golpe, una réplica.
- **Jabalina · Destello**: te teletransportas junto a la jabalina en el aire y sueltas el picado.

## Decisiones

- 2026-10-20 · Creada sin el usuario. Sismo: el golpe empujaba a los enemigos fuera de la zona que tiembla y casi no había
  réplicas → con Sismo el golpe empuja ×0,25 y la zona pasó de 150 a 200 de medio ancho.

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 0.9 | 0.1 – 4 (paso 0.05) | Base: recarga (s) |
| `chargeTime` | 0.6 | 0.05 – 2 (paso 0.05) | Base: carga máxima (s) |
| `dmgMin` | 30 | 1 – 150 (paso 1) | Base: daño sin carga |
| `dmgMax` | 85 | 1 – 300 (paso 1) | Base: daño con carga |
| `slamWidth` | 90 | 30 – 250 (paso 5) | Base: ancho del golpe |
| `shockRadius` | 110 | 0 – 300 (paso 5) | Base: onda alrededor (a cada lado) |
| `shockDamage` | 15 | 0 – 80 (paso 1) | Base: daño de la onda |
| `diveSpeed` | 1300 | 400 – 2500 (paso 10) | Base: velocidad del picado |
| `fallBonus` | 0.25 | 0 – 1 (paso 0.01) | Base: + daño por cada 100 px de caída |
| `iceFreeze` | 0.6 | 0 – 1 (paso 0.05) | Hielo: congelación del golpe |
| `fireBurn` | 2 | 0 – 6 (paso 0.1) | Fuego: quemadura (y suelo ardiendo) |
| `waveSpeed` | 650 | 100 – 1500 (paso 10) | Terremoto: velocidad de la onda |
| `waveRange` | 420 | 80 – 900 (paso 10) | Terremoto: alcance |
| `waveDamage` | 24 | 0 – 120 (paso 1) | Terremoto: daño |
| `waveLift` | 520 | 0 – 1200 (paso 10) | Terremoto: los lanza hacia arriba |
| `waveStun` | 0.4 | 0 – 2 (paso 0.05) | Terremoto: aturdido (s) |
| `afterMul` | 0.6 | 0.1 – 1.5 (paso 0.05) | Terremoto · Réplica: × daño |
| `afterDelay` | 0.5 | 0.1 – 2 (paso 0.05) | Terremoto · Réplica: retraso (s) |
| `fissureTime` | 2 | 0.3 – 6 (paso 0.1) | Terremoto · Fisura: duración (s) |
| `fissureDps` | 12 | 0 – 60 (paso 1) | Terremoto · Fisura: daño/s |
| `hopSpeed` | 650 | 200 – 1200 (paso 10) | Meteoro: salto antes del picado |
| `meteorFall` | 0.6 | 0 – 2 (paso 0.05) | Meteoro: + daño por cada 100 px de caída (en vez de fallBonus) |
| `craterTime` | 1.5 | 0.3 – 5 (paso 0.1) | Meteoro · Cráter: duración (s) |
| `craterRadius` | 170 | 40 – 400 (paso 5) | Meteoro · Cráter: radio |
| `craterPull` | 180 | 0 – 500 (paso 5) | Meteoro · Cráter: atracción |
| `bounceMul` | 0.6 | 0.1 – 1.5 (paso 0.05) | Meteoro · Rebote: × del golpe extra |
| `heavyTime` | 3 | 0.3 – 8 (paso 0.1) | Meteoro · Gravedad: s pesados |
| `heavySlow` | 0.5 | 0 – 1 (paso 0.05) | Meteoro · Gravedad: × velocidad |
| `tremorTime` | 3 | 0.5 – 10 (paso 0.1) | Sismo: s que tiembla |
| `tremorWidth` | 200 | 40 – 400 (paso 5) | Sismo: medio ancho de la zona |
| `tremorDamage` | 14 | 0 – 80 (paso 1) | Sismo: daño de la réplica |
| `tremorRadius` | 75 | 20 – 200 (paso 5) | Sismo: radio de la réplica |
| `tremorGap` | 0.3 | 0.05 – 2 (paso 0.05) | Sismo: s entre réplicas del mismo enemigo |
| `toppleStun` | 0.5 | 0 – 2 (paso 0.05) | Sismo · Desequilibrio: aturdido (s) |
<!-- /gen:tuning -->
