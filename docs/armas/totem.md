# Tótem de cristal (`totem`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras), **sin probar por el usuario** (hecho 2026-10-20 sin él).
Código: `g-totem.js`; dibujo en `fx-armas2.js` (`crystalTotem`, `resonWave`). Reglas comunes: [README](README.md) ·
progresión: [progresion.md](progresion.md).

## Rol

**Torreta que trabaja con el arma guardada.** Clic = lanzas un cristal en arco que se planta en el suelo (o en la plataforma
que haya debajo del ratón) y dispara solo al enemigo más cercano durante un rato. Lo natural es plantarlo y cambiar a la otra
arma. Las evoluciones lo vuelven un **eco de tus golpes** (Resonador), una **torreta seria** (Centinela) o un **cebo** (Señuelo).

## Comportamiento (base)

- Clic = tótem en la x del ratón (máx. `range` desde ti), sobre la superficie que haya debajo. Dura `totemTime` s (aro en la
  base que se vacía). Sólo uno: el nuevo sustituye al viejo.
- Cada `fireGap` s dispara un cristal (`boltDamage`, `boltSpeed`) al enemigo más cercano a `totemRange`; la punta brilla antes.
- Los cristales son **capturables** (la Singularidad los atrapa y los devuelve), los aceleran el Reloj · Aceleración y la
  Ráfaga · Corriente, y los curva la Luz guía del Enjambre.

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Resonador** (`reson`) | El tótem COPIA tus golpes: cada golpe de tu otra arma a un enemigo cerca del tótem le devuelve una onda con parte de ese daño. | **Amplificador** (`amp`): La copia es más fuerte y llega más lejos.<br>**Coro** (`choir`): Puedes tener dos tótems a la vez; los dos copian.<br>**Acorde** (`chord`): Cada pocas copias, el tótem suelta una onda de choque alrededor. |
| **Centinela** (`sentry`) | Torreta de verdad: dispara el doble de rápido, sus cristales atraviesan a uno y prefiere a los marcados y a los congelados. | **Triple** (`triple`): Cada disparo son tres cristales en abanico.<br>**Ojo de halcón** (`hawk`): Mucho más alcance, y sus disparos a marcados o congelados son golpes fuertes.<br>**Égida** (`aegis`): Derriba las balas enemigas que pasan cerca. |
| **Señuelo** (`decoy`) | El tótem PROVOCA: atrae a los enemigos cercanos y sus disparos van a por él. Tiene vida; al romperse (o acabarse) estalla. | **Espinas** (`thorns`): Los que lo tocan reciben daño continuo y van lentos.<br>**Imán** (`magnet`): Atrae con mucha más fuerza y desde más lejos: los amontona.<br>**Renacer** (`rebirth`): Al estallar, deja un tótem más pequeño en su sitio (una vez). |
<!-- /gen:tree -->

Detalle:
- **Resonador** (dorado): cada golpe de **tu otra arma** a un enemigo a `resonRadius` del tótem → onda desde la punta con
  ×`resonMul` del daño de ese golpe (después de la build; `proc`). Sigue disparando lo suyo.
  - Amplificador: ×`ampMul` y radio ×`ampRadius`. Coro: dos tótems a la vez (los dos copian). Acorde: cada `chordEvery`
    copias, onda de choque alrededor del tótem (`chordDamage`, `chordRadius`).
- **Centinela**: dispara cada `sentryGap` s, sus cristales atraviesan a uno y prefiere marcados (Cazador) y congelados/con hielo.
  - Triple: tres cristales en abanico (`tripleSpread`). Ojo de halcón: alcance ×`hawkRange` y a marcados/congelados son golpe
    fuerte (rompe congelados). Égida: derriba las balas enemigas a `aegisRadius`.
- **Señuelo** (rosa): **provoca** — atrae a los enemigos a `decoyRadius` (`decoyPull`) y las balas de los que están cerca van
  a por él en vez de a por ti (`G.decoys`, regla común nueva). Tiene `decoyHp` de vida (cada bala quita 8); al romperse o
  acabarse **estalla** (`decoyBoom`, `decoyBoomRadius`, golpe fuerte).
  - Espinas: los que lo tocan reciben `thornsDps` y van a la mitad de velocidad. Imán: atrae con `magnetPull` desde ×`magnetRadius`
    (los amontona). Renacer: al estallar deja un tótem ×`rebirthScale` (tamaño, vida, duración; una vez).

## Elemento (con 4 de ese elemento) y Duplicador

- **Hielo**: cada cristal congela `iceFreeze` (con Centinela · Ojo de halcón, el siguiente lo rompe).
- **Fuego**: cada cristal quema `fireBurn`.
- **Duplicador**: cada disparo sale doble (un poco abierto).

## Sinergias

- **Resonador + golpes grandes** (Golpe estelar, Jabalina cargada, Sobrecarga del Rayo). Con Coro, dos copias.
- **Señuelo + Singularidad / Golpe estelar / Ráfaga · Tornado**: los amontona quietos junto al tótem.
- **Centinela + Cazador**: dispara primero a los marcados. **Singularidad · Arsenal**: atrapa sus cristales y los multiplica.

## Decisiones

- 2026-10-20 · Creada sin el usuario.

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 5 | 0.5 – 15 (paso 0.1) | Base: recarga (s) |
| `range` | 420 | 80 – 800 (paso 10) | Base: distancia máxima para plantarlo |
| `totemTime` | 9 | 1 – 25 (paso 0.5) | Base: duración (s) |
| `fireGap` | 0.7 | 0.1 – 3 (paso 0.05) | Base: s entre disparos |
| `boltDamage` | 9 | 1 – 60 (paso 1) | Base: daño por cristal |
| `boltSpeed` | 650 | 200 – 1500 (paso 10) | Base: velocidad del cristal |
| `totemRange` | 380 | 80 – 800 (paso 10) | Base: alcance del tótem |
| `iceFreeze` | 0.2 | 0 – 1 (paso 0.05) | Hielo: congelación por cristal |
| `fireBurn` | 1.2 | 0 – 5 (paso 0.1) | Fuego: quemadura por cristal (s) |
| `resonRadius` | 320 | 80 – 700 (paso 10) | Resonador: radio |
| `resonMul` | 0.4 | 0 – 2 (paso 0.05) | Resonador: × daño del golpe copiado |
| `ampMul` | 0.7 | 0 – 2 (paso 0.05) | Resonador · Amplificador: × daño (en vez de resonMul) |
| `ampRadius` | 1.5 | 1 – 3 (paso 0.05) | Resonador · Amplificador: × radio |
| `chordEvery` | 6 | 2 – 20 (paso 1) | Resonador · Acorde: cada N copias |
| `chordDamage` | 30 | 0 – 150 (paso 1) | Resonador · Acorde: daño |
| `chordRadius` | 140 | 30 – 350 (paso 5) | Resonador · Acorde: radio |
| `sentryGap` | 0.35 | 0.05 – 2 (paso 0.05) | Centinela: s entre disparos |
| `tripleSpread` | 0.18 | 0.02 – 0.6 (paso 0.01) | Centinela · Triple: abertura (rad) |
| `hawkRange` | 1.6 | 1 – 3 (paso 0.05) | Centinela · Ojo de halcón: × alcance |
| `aegisRadius` | 160 | 30 – 400 (paso 5) | Centinela · Égida: radio |
| `decoyRadius` | 300 | 50 – 700 (paso 10) | Señuelo: radio de provocación |
| `decoyPull` | 60 | 0 – 300 (paso 5) | Señuelo: atracción |
| `decoyHp` | 60 | 5 – 400 (paso 5) | Señuelo: vida |
| `decoyBoom` | 40 | 0 – 200 (paso 1) | Señuelo: daño al estallar |
| `decoyBoomRadius` | 120 | 30 – 350 (paso 5) | Señuelo: radio del estallido |
| `thornsDps` | 15 | 0 – 80 (paso 1) | Señuelo · Espinas: daño/s |
| `magnetPull` | 140 | 0 – 400 (paso 5) | Señuelo · Imán: atracción |
| `magnetRadius` | 1.5 | 1 – 3 (paso 0.05) | Señuelo · Imán: × radio |
| `rebirthScale` | 0.6 | 0.2 – 1 (paso 0.05) | Señuelo · Renacer: tamaño, vida y duración (×) |
<!-- /gen:tuning -->
