# Abanico de cristal (`fan`)

**Estado**: prototipo web (clips + jugable), pendiente de que el usuario lo pruebe. Código: `a1-abanico.js` (clips),
`g-abanico.js` (jugable). Reglas comunes: [README](README.md).

## Rol

Disparo inmediato sin carga: varias esquirlas en abanico, corto alcance. El arma de «pegarse al enemigo», opuesta al Cometa
(que premia la distancia). Mucho daño de cerca porque aciertan casi todas; de lejos se abren y fallan.

## Comportamiento (jugable)

- Clic = una ráfaga; mantener = ráfaga cada `cooldown`.
- `count` esquirlas repartidas en ±`spread` rad alrededor de la puntería (pequeño azar de ±0,01 rad y de ±5 % de velocidad).
- Vuelan recto a `speed` durante `life` s. Chocan con el primer enemigo (elipse) o con el suelo; atraviesan las plataformas.
- Al chocar con enemigo: `damage` + empuje `knockback`; impacto pequeño (agujas + gotas). Contra el suelo: chispas y polvo.
- Son **capturables** por la Singularidad mientras vuelan.

<!-- gen:mods -->
| Mod (`id`) | Qué hace |
|---|---|
| Hielo (`ice`) | Las esquirlas se clavan y congelan; congelado + 0,5 s = añicos. |
| Rebote (`bounce`) | Cada esquirla rebota una vez (suelo o enemigo → hacia el siguiente). |
| Retorno (`ret`) | Se clavan y vuelven al cristal golpeando otra vez; cada una recarga el arma de reserva. |
<!-- /gen:mods -->

Detalle de los mods:
- **Hielo**: paleta hielo; cada esquirla suma `freeze` de congelación y **se queda clavada** (asoma la mitad trasera). Cuando el
  enemigo se congela, las clavadas esperan `shatterDelay` s y provocan un golpe fuerte → reacción **añicos**.
- **Rebote**: paleta oro; un rebote por esquirla. En el suelo sale hacia arriba (75 % de la vertical). En un enemigo se desvía
  hacia el **enemigo más cercano** (≤ 420 px) que no haya tocado; si no hay, sale hacia atrás y arriba. Conserva `bounceKeep`.
- **Retorno**: se clavan (enemigo o suelo), esperan `stickTime` y vuelven en curva al cristal: al arrancarse hacen
  `returnDamage`. Cada una que llega **resta `refund` s a la recarga del arma de reserva** (sinergia de 2 armas).
- Combinables entre sí (Hielo + Retorno: se clavan, congelan y vuelven).

## Sinergias

- **Singularidad**: las esquirlas que pasan cerca quedan orbitando y salen en todas direcciones al colapsar (Catapulta). Con
  Hielo, la metralla congela todo el grupo atraído.
- **Retorno** acelera el arma de reserva → pareja natural con armas de recarga larga (Singularidad, Golpe estelar).
- **Golpe estelar con Marcas**: cada esquirla marca.

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 0.45 | 0.1 – 2 (paso 0.01) | Recarga (s) |
| `count` | 5 | 1 – 12 (paso 1) | Esquirlas |
| `spread` | 0.2 | 0 – 0.8 (paso 0.01) | Apertura (rad, media) |
| `speed` | 950 | 300 – 2000 (paso 10) | Velocidad (px/s) |
| `damage` | 9 | 1 – 60 (paso 1) | Daño por esquirla |
| `knockback` | 60 | 0 – 400 (paso 5) | Empuje |
| `life` | 0.6 | 0.1 – 2 (paso 0.05) | Alcance (s de vuelo) |
| `freeze` | 0.34 | 0 – 1 (paso 0.01) | Hielo: congelación por esquirla |
| `shatterDelay` | 0.5 | 0 – 2 (paso 0.05) | Hielo: espera hasta añicos (s) |
| `stickTime` | 0.35 | 0 – 1.5 (paso 0.05) | Retorno: tiempo clavada (s) |
| `returnDamage` | 7 | 0 – 60 (paso 1) | Retorno: daño al volver |
| `refund` | 0.15 | 0 – 1 (paso 0.01) | Retorno: recarga del arma de reserva por esquirla (s) |
| `bounceKeep` | 0.85 | 0.3 – 1.2 (paso 0.01) | Rebote: velocidad que conserva |
<!-- /gen:tuning -->

## Visual

`crystalShard` (core.js: lente en 3 capas + faceta clara + resplandor), estela `ribbon` en 3 pasadas (morado ancho, medio,
núcleo) que nace en la cabeza y acaba en punta. Impacto `impact(…, 0.45)`. Clavada: la misma esquirla desplazada 8 px hacia atrás
(sólo asoma la cola) + destello `star4` si es de hielo. Añicos: `hitFx('shatter')` en `game.js` (onda, fragmentos `chunk`, chispas).

## Sonido

`fan_release` (disparo: ruido filtrado que barre + parciales de cristal + golpe grave corto), `fan_hit`, `fan_ground`,
`fan_stick` (hielo), `fan_freeze`, `fan_shatter`, `fan_ricochet`, `fan_rip` (se arrancan), `fan_absorb` (vuelven al cristal).

## Port a Unity (plan)

Forma nueva del disparo base (abanico) en el pipeline de `ShotResolver`; los mods son items (Hielo = Elemento; Rebote y Retorno
= Trayectorias nuevas que el resto de armas también pueden usar). Esquirla = proyectil con pool y estado (vuela / clavada /
vuelve). El «refund» al arma de reserva necesita que `WeaponInventory` tenga dos armas activas.

## Decisiones

- 2026-10-19 · Apertura 0,2 rad (0,24 dejaba pasar dos por encima del enemigo en el clip): 3 aciertan, 1 al suelo, 1 se pierde.
- 2026-10-19 · Retorno recarga el arma de reserva (idea propia para dar sentido al sistema de 2 armas; pendiente de opinión).

## Preguntas abiertas

- ¿El Retorno debería recargar el arma de reserva, dar escudo o curar?
