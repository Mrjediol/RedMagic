# Pacto de sangre (`blood`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras), **sin probar por el usuario** (hecho 2026-10-20 sin él).
Código: `g-pacto.js`; dibujo en `fx-armas2.js` (`bloodOrb`, `bleedDrops`, `bloodNova`; usa `lifeMotes` y `energyBeam`).
Paleta nueva `blood` (rojo oscuro). Reglas comunes: [README](README.md) · progresión: [progresion.md](progresion.md).

## Rol

**Riesgo / recompensa: pagas con tu vida.** Cada orbe de sangre cuesta un poco de vida y pega mucho; las bajas del orbe te la
devuelven. Pareja natural de las etiquetas **Robo de vida** y **Tanque** (curarse = poder disparar más). Si no te queda vida
para pagar, el orbe sale gratis pero a medias (nunca te mata).

## Comportamiento (base)

- Clic = orbe (`orbSpeed`, atraviesa `pierce`) que cuesta `cost` de vida (número rojo junto al player) y hace `orbDamage`.
- Cada baja del orbe cura `healOnKill`.
- Es **capturable** (Singularidad) y se acelera/desvía con Reloj, Corriente y Espejo.

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Hemorragia** (`bleed`) | El orbe hace SANGRAR (se acumula). Golpear con tu OTRA arma a un enemigo que sangra mucho revienta la sangre: daño extra y te cura. | **Arterias** (`artery`): Al morir un enemigo que sangra, su sangrado pasa a los de alrededor.<br>**Transfusión** (`transfuse`): El derrame cura el doble y lo que sobra se queda de escudo.<br>**Rabia** (`rage`): Cuanta menos vida tengas, más daño hace el derrame. |
| **Vampiro** (`vamp`) | El orbe vuelve a ti después de golpear y te devuelve parte del daño que hizo. | **Sed** (`thirst`): Cada baja del orbe te devuelve lo que costó y lo deja listo al instante.<br>**Murciélagos** (`bats`): Al golpear, el orbe suelta murciélagos que buscan a otros enemigos.<br>**Drenar** (`drain`): Mantén: en vez de lanzar, enlazas al enemigo más cercano al ratón y le chupas la vida (no cuesta). |
| **Mártir** (`martyr`) | Cuanta MENOS vida tienes, más daño hacen TODAS tus armas. El clic gasta mucha vida en una nova de sangre que crece con la vida que te falta. | **Último aliento** (`breath`): Un golpe que te mataría te deja a 1 de vida y suelta una nova enorme (cada 30 s).<br>**Sacrificio** (`sacrifice`): La nova cuesta más pero aturde a todos.<br>**Penitencia** (`penance`): Cada vez que te hieren, tu siguiente ataque (de cualquier arma) es mucho más fuerte. |
<!-- /gen:tree -->

Detalle:
- **Hemorragia**: cada orbe suma 1 carga de **sangrado** (hasta `bleedMax`; dura `bleedTime`; `bleedDps` por carga; gotas y 🩸N
  encima). Cuando **tu otra arma** golpea a uno con al menos `burstMin` cargas: **derrame** = `burstPer` × cargas (golpe fuerte)
  y te cura `burstHeal` × cargas; las cargas se gastan. Juego: marcas con el Pacto, rematas con la otra.
  - Arterias: al morir un sangrante, su sangrado pasa a los que estén a `arteryRadius`. Transfusión: el derrame cura el doble
    y lo que sobra es escudo (hasta 40). Rabia: el derrame hace hasta ×(1 + `rageMax`) cuanta menos vida tengas.
- **Vampiro**: el orbe vuelve a ti en cuanto golpea y te cura `lifesteal` del daño que hizo.
  - Sed: cada baja del orbe te devuelve lo que costó y deja el arma lista. Murciélagos: al golpear suelta `batCount` murciélagos
    que buscan a otros (`batDamage`) y vuelven curando. Drenar: mantener = hilo de sangre al enemigo más cercano al ratón
    (`drainRange`), `drainDps` y te cura `drainHeal`; no cuesta.
- **Mártir**: **todas tus armas** hacen hasta ×(1 + `martyrMax`) cuanta menos vida tienes (aura roja que late más deprisa).
  El clic es una **nova de sangre** que cuesta `novaCost` de tu vida máxima y hace `novaDamage` + `novaPerMissing` × vida que te
  falta (golpe fuerte, radio `novaRadius`).
  - Último aliento: un golpe que te mataría te deja a 1, 1,5 s intocable y suelta una nova doble (cada `breathCd` s). Sacrificio:
    la nova cuesta ×1,5 y aturde `sacrificeStun`. Penitencia: cada vez que te hieren, tu siguiente ataque (de cualquier arma)
    hace ×`penanceMul` y es golpe fuerte.

## Elemento (con 4 de ese elemento) y Duplicador

- **Hielo**: el orbe y la nova congelan (`iceFreeze`). **Fuego**: queman (`fireBurn`).
- **Duplicador**: dos orbes por el mismo precio.

## Sinergias

- **Robo de vida / Tanque** (items): cuanto más te curas, más orbes; con Mártir, curarte es perder fuerza → equilibrio raro.
- **Hemorragia + armas de golpe fuerte** (Golpe estelar, Jabalina): el derrame es golpe fuerte → con hielo, añicos.
- **Mártir** potencia también a la otra arma: Mártir + Golpe estelar o + Singularidad a poca vida = golpes enormes.
- Con «Los enemigos disparan»: Penitencia y Último aliento cobran sentido.

## Decisiones

- 2026-10-20 · Creada sin el usuario. Motor: `inst.outMul(e, o)` (multiplica todos tus golpes de arma), `inst.onHurt(d)` y
  `inst.onLethal()` (evitar la muerte) en `hurtPlayer`. El Vampiro vuelve en cuanto golpea (antes atravesaba uno y tardaba).

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 0.55 | 0.1 – 3 (paso 0.05) | Base: recarga (s) |
| `cost` | 3 | 0 – 20 (paso 0.5) | Base: vida que cuesta cada orbe |
| `orbDamage` | 38 | 1 – 200 (paso 1) | Base: daño del orbe |
| `orbSpeed` | 700 | 200 – 1500 (paso 10) | Base: velocidad |
| `pierce` | 1 | 0 – 5 (paso 1) | Base: enemigos que atraviesa |
| `healOnKill` | 6 | 0 – 40 (paso 1) | Base: vida que devuelve cada baja del orbe |
| `iceFreeze` | 0.4 | 0 – 1 (paso 0.05) | Hielo: congelación |
| `fireBurn` | 2 | 0 – 6 (paso 0.1) | Fuego: quemadura (s) |
| `bleedMax` | 5 | 1 – 12 (paso 1) | Hemorragia: cargas máximas |
| `bleedDps` | 3 | 0 – 20 (paso 0.5) | Hemorragia: daño/s por carga |
| `bleedTime` | 4 | 0.5 – 12 (paso 0.5) | Hemorragia: s que dura |
| `burstMin` | 3 | 1 – 12 (paso 1) | Hemorragia: cargas para que reviente |
| `burstPer` | 8 | 0 – 40 (paso 1) | Hemorragia: daño del derrame por carga |
| `burstHeal` | 1 | 0 – 10 (paso 0.5) | Hemorragia: vida por carga |
| `arteryRadius` | 110 | 20 – 300 (paso 5) | Hemorragia · Arterias: radio |
| `rageMax` | 1 | 0 – 3 (paso 0.05) | Hemorragia · Rabia: + daño con 1 de vida (×) |
| `lifesteal` | 0.35 | 0 – 1 (paso 0.01) | Vampiro: fracción del daño que te cura al volver |
| `batCount` | 3 | 1 – 8 (paso 1) | Vampiro · Murciélagos: cuántos |
| `batDamage` | 14 | 1 – 60 (paso 1) | Vampiro · Murciélagos: daño |
| `drainDps` | 30 | 0 – 120 (paso 1) | Vampiro · Drenar: daño/s |
| `drainHeal` | 0.5 | 0 – 1 (paso 0.05) | Vampiro · Drenar: fracción que cura |
| `drainRange` | 280 | 50 – 600 (paso 10) | Vampiro · Drenar: alcance |
| `martyrMax` | 0.6 | 0 – 2 (paso 0.05) | Mártir: + daño de todas tus armas sin vida (×) |
| `novaCost` | 0.12 | 0 – 0.5 (paso 0.01) | Mártir: nova, fracción de vida máxima que cuesta |
| `novaRadius` | 180 | 40 – 400 (paso 5) | Mártir: radio de la nova |
| `novaDamage` | 40 | 0 – 200 (paso 1) | Mártir: daño fijo de la nova |
| `novaPerMissing` | 1 | 0 – 5 (paso 0.05) | Mártir: + daño por punto de vida que te falta |
| `breathCd` | 30 | 5 – 120 (paso 1) | Mártir · Último aliento: cada (s) |
| `sacrificeStun` | 1.2 | 0 – 4 (paso 0.1) | Mártir · Sacrificio: aturdido (s) |
| `penanceMul` | 2 | 1 – 5 (paso 0.1) | Mártir · Penitencia: × siguiente ataque |
<!-- /gen:tuning -->
