# Acumulador (`volt`)

**Estado**: prototipo web, árbol completo (3 evoluciones × 3 mejoras), **sin probar por el usuario** (hecho 2026-10-20 sin él).
Código: `g-acumulador.js`; dibujo en `fx-armas2.js` (`voltAura`, `skyBolt`; usa `rodBolt` y `shockRing`). Paleta nueva `volt`
(amarillo eléctrico). Reglas comunes: [README](README.md) · progresión: [progresion.md](progresion.md).

## Rol

**Se carga con la otra arma.** No dispara nada propio hasta que tu otra arma ha golpeado: cada golpe suyo (y cada baja) llena
la batería; al usar el Acumulador lo descargas todo de golpe. Es la versión extrema de «mejoras que funcionan sin usar el arma»:
la mayor parte del tiempo la llevas guardada, cargándose (chispas que orbitan alrededor del player, más con más carga).

## Comportamiento (base)

- Carga: cada golpe de arma **que no sea el Acumulador** suma `perHit` + `perDmg` × daño; cada baja con otra arma, `perKill`.
  Máximo `maxCharge` («CARGADO» al llenarse). Los golpes del propio Acumulador no cargan.
- Clic con al menos `minCharge`: **nova** alrededor de ti (`novaRadius`) con `novaBase` + `novaPer` × carga a cada enemigo,
  rayos hacia cada uno; con más del 80 % de carga, golpe fuerte + impact frame. Gasta toda la carga.
- Sin carga: chispazo y «sin carga».

## Árbol (evolución en nivel 2, mejoras en 3-5)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Sobrecarga** (`overload`) | Al llenarse, descarga SOLA alrededor de ti (aunque lleves la otra arma en la mano). | **Cortocircuito** (`short`): Descarga antes (con menos carga): más a menudo y más pequeña.<br>**Electrocución** (`stun`): Las descargas aturden.<br>**Pulso** (`pulse`): Cada descarga recarga del todo tu otra arma. |
| **Arco** (`arc`) | La descarga es un rayo en cadena: sale hacia donde apuntas y salta de enemigo en enemigo; más carga = más saltos. | **Cadena larga** (`long`): Más saltos y más lejos.<br>**Bifurcación** (`fork`): Cada salto se divide en dos.<br>**Retorno** (`refund`): Cada baja del arco devuelve carga. |
| **Celdas** (`cells`) | Guarda hasta 3 celdas de carga; cada clic gasta UNA en un rayo del cielo donde apuntas (golpe fuerte). | **Tormenta** (`salvo`): Mantén para soltar todas las celdas seguidas sobre los enemigos más cercanos al ratón.<br>**Suelo cargado** (`field`): Donde cae el rayo, el suelo queda electrificado un rato.<br>**Goteo** (`trickle`): Las celdas también se cargan solas, despacio. |
<!-- /gen:tree -->

Detalle:
- **Sobrecarga**: al llegar al máximo descarga **sola** (con la otra arma en la mano) — la batería se convierte en una nova
  periódica que depende de lo que pegues.
  - Cortocircuito: descarga a `shortAt` del máximo. Electrocución: las descargas aturden `stunTime`. Pulso: cada descarga
    recarga del todo tu otra arma.
- **Arco**: la descarga es un rayo en cadena: sale hacia el enemigo más cercano al ratón y salta (`arcRange`) de enemigo en
  enemigo; saltos = `arcJumps` + carga / `arcPerJump`; daño `arcBase` + `arcPer` × carga, ×`arcFalloff` en cada salto.
  - Cadena larga: +`longJumps` y ×`longRange`. Bifurcación: cada salto se divide en dos. Retorno: cada baja del arco devuelve
    `refundKill` de carga (encadenar arcos).
- **Celdas**: la carga sube hasta `cellMax` (3 celdas de 100; se ven orbitando). Cada clic gasta **una** celda en un rayo del
  cielo donde apuntas (`cellDamage`, ancho `cellWidth`, golpe fuerte, estampa voladores).
  - Tormenta: mantener suelta las celdas una tras otra (`salvoGap`) sobre los enemigos más cercanos al ratón. Suelo cargado:
    donde cae queda electrificado (`fieldTime`, `fieldDps`). Goteo: las celdas se cargan solas `trickle`/s.

## Elemento (con 4 de ese elemento) y Duplicador

- **Hielo**: la descarga congela (`iceFreeze` × carga) → con carga llena es golpe fuerte: lo que ya estaba congelado se rompe.
- **Fuego**: la descarga quema (`fireBurn`).
- **Duplicador**: una segunda nova más pequeña 0,15 s después; el arco sale doble; cada celda, dos rayos.

## Sinergias

- **Cualquier arma de muchos golpes** carga rápido: Abanico, Rayo (cada tick), Enjambre (cada picadura), Látigo · Remolino.
- **Sobrecarga + Pulso + arma de recarga larga** (Golpe estelar, Singularidad): cada nova te devuelve la lanza.
- **Arco + Retorno** contra grupos; **Celdas + Cazador** (la marca no importa al rayo, pero el rayo es golpe fuerte → añicos).

## Decisiones

- 2026-10-20 · Creada sin el usuario (idea: un arma que sólo progresa mientras usas la otra).

## Ajustes

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `maxCharge` | 100 | 20 – 300 (paso 5) | Base: carga máxima |
| `perHit` | 2 | 0 – 20 (paso 0.5) | Base: carga por golpe de la otra arma |
| `perDmg` | 0.12 | 0 – 1 (paso 0.01) | Base: carga por punto de daño |
| `perKill` | 8 | 0 – 50 (paso 1) | Base: carga por baja con la otra arma |
| `minCharge` | 10 | 0 – 100 (paso 1) | Base: carga mínima para descargar |
| `cooldown` | 0.4 | 0 – 3 (paso 0.05) | Base: recarga tras descargar (s) |
| `novaRadius` | 200 | 50 – 500 (paso 5) | Base: radio de la nova |
| `novaBase` | 10 | 0 – 100 (paso 1) | Base: daño fijo |
| `novaPer` | 0.9 | 0 – 5 (paso 0.05) | Base: + daño por punto de carga |
| `iceFreeze` | 0.5 | 0 – 1 (paso 0.05) | Hielo: congelación (carga llena) |
| `fireBurn` | 2.5 | 0 – 6 (paso 0.1) | Fuego: quemadura (s) |
| `shortAt` | 0.6 | 0.2 – 1 (paso 0.05) | Sobrecarga · Cortocircuito: descarga a esta fracción |
| `stunTime` | 0.9 | 0 – 3 (paso 0.1) | Sobrecarga · Electrocución: aturdido (s) |
| `arcJumps` | 2 | 0 – 10 (paso 1) | Arco: saltos base |
| `arcPerJump` | 25 | 5 – 100 (paso 1) | Arco: carga por salto extra |
| `arcRange` | 200 | 50 – 500 (paso 10) | Arco: distancia de salto |
| `arcBase` | 12 | 0 – 100 (paso 1) | Arco: daño fijo por golpe |
| `arcPer` | 0.7 | 0 – 5 (paso 0.05) | Arco: + daño por punto de carga |
| `arcFalloff` | 0.85 | 0.3 – 1 (paso 0.01) | Arco: × daño en cada salto |
| `longJumps` | 3 | 0 – 10 (paso 1) | Arco · Cadena larga: saltos extra |
| `longRange` | 1.4 | 1 – 3 (paso 0.05) | Arco · Cadena larga: × distancia |
| `refundKill` | 20 | 0 – 100 (paso 1) | Arco · Retorno: carga por baja |
| `cellMax` | 300 | 100 – 600 (paso 50) | Celdas: carga máxima (3 celdas) |
| `cellDamage` | 75 | 1 – 300 (paso 1) | Celdas: daño del rayo |
| `cellWidth` | 44 | 10 – 150 (paso 2) | Celdas: ancho del rayo |
| `salvoGap` | 0.15 | 0.03 – 0.6 (paso 0.01) | Celdas · Tormenta: s entre rayos |
| `fieldTime` | 2.5 | 0.3 – 8 (paso 0.1) | Celdas · Suelo cargado: duración (s) |
| `fieldDps` | 14 | 0 – 80 (paso 1) | Celdas · Suelo cargado: daño/s |
| `trickle` | 6 | 0 – 40 (paso 0.5) | Celdas · Goteo: carga por segundo |
<!-- /gen:tuning -->
