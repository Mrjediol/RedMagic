# Rayo (`ray`)

**Estado**: prototipo web (clips + jugable); árbol de evoluciones hecho 2026-10-19, pendiente de la prueba del usuario. Código: `a3-rayo.js`, `g-rayo.js`.

## Rol

Mantener para canalizar un haz continuo que quema por ticks al primer enemigo que toca. Daño sostenido; obliga a quedarse y
apuntar. Otra forma de disparar («mantener» en vez de «cargar y soltar»).

## Comportamiento (jugable)

- Mantener clic = haz desde el cristal. Se extiende de golpe (0,07 s) hasta el primer enemigo, el suelo o `range`.
- La dirección sigue al ratón con giro limitado (`turnRate` rad/s): barrer tiene inercia.
- Cada `tick` s: `tickDamage` al primero + empuje `push`. El calor sube mientras canalizas; a `maxChannel` s se corta solo.
- Al soltar (o al cambiar de arma, o al llegar al máximo): se apaga en 0,14 s dejando restos que flotan; recarga `cooldown`.

## Árbol (evolución en nivel 2 + 3 mejoras)

<!-- gen:tree -->
| Evolución (id) | Idea | Mejoras |
|---|---|---|
| **Sobrecarga** (`over`) | El daño crece mientras canalizas y al soltar descarga a lo largo de todo el haz. Si llegas al máximo se recalienta y tarda más en recargar. | **Punto justo** (`sweet`): Soltar en el último tramo del calor, sin llegar al máximo: descarga doble y golpe fuerte.<br>**Perforante** (`pierce`): Con más de medio calor el haz atraviesa a todos los de la línea.<br>**Onda** (`wave`): La descarga estalla también en el punto de contacto, en área. |
| **Prisma** (`prism`) | Atraviesa al primero y se divide en haces dorados hacia los enemigos más cercanos. | **Cadena** (`chain`): Cada haz dorado salta a un enemigo más.<br>**Foco** (`focus`): Cada haz que no encuentra a quién ir se suma al principal (bueno contra uno solo).<br>**Reflejo** (`reflect`): El haz rebota una vez en el suelo con el alcance que le queda. |
| **Faro** (`beacon`) | Al soltar, el cristal se queda flotando donde estabas y sigue disparando solo al enemigo más cercano. Premia cambiar de arma. | **Vínculo** (`link`): Un hilo de luz une el faro contigo: daña lo que lo cruza y parte del daño del faro te cura.<br>**Estallido** (`nova`): Al apagarse, el faro lo descarga todo de golpe: estallido en área donde apunta (golpe fuerte).<br>**Vigía** (`watch`): Cada baja del faro alarga su vida. |
<!-- /gen:tree -->

Detalle:
- **Sobrecarga**: el daño por tick sube de ×1 a ×`overMax` con el calor y el haz se engrosa. Al soltar (con al menos `overMin` de
  calor) **descarga** por toda la línea del haz hasta el alcance, atravesando: `overBurst` × calor a cada uno; golpe fuerte desde
  `overHeavyAt`. Si llega al máximo se corta solo, descarga igual pero queda **RECALENTADO**: recarga ×`overHeatCd`.
  - Punto justo: soltar con calor ≥ `sweetFrom` sin llegar al máximo → descarga ×`sweetMul`, dorada, golpe fuerte, «¡JUSTO!».
    Mientras estás en la franja, una estrella dorada late en el cristal (aviso de «suelta ya»).
  - Perforante: con calor ≥ `pierceAt` los ticks dan a todos los de la línea (no sólo al primero).
  - Onda: la descarga estalla también en el punto de contacto (`waveDamage` × calor en `waveRadius`).
- **Prisma** (era la mejora provisional): sale por detrás del primero y se divide en `prismCount` haces dorados hacia los más
  cercanos a ese punto (≤ `prismRange`), cada uno ×`prismMul`. Gema-prisma girando en el punto de salida.
  - Cadena: cada haz dorado salta `chainJumps` vez/veces más al siguiente más cercano.
  - Foco: cada haz que no encuentra enemigo suma +`focusMul` al daño del principal (contra uno solo: ×2,2 con 2 haces).
  - Reflejo: al tocar el suelo rebota una vez (ángulo de espejo) con el alcance que le queda; el primero tras el rebote cuenta para el Prisma.
- **Faro**: al soltar (o al cambiar de arma, o al llegar al máximo) el cristal se queda flotando donde estaba la boca y dispara solo
  `beaconTime` s al enemigo más cercano (≤ `beaconRange`), con ×`beaconMul` de daño y el mismo giro limitado. Un faro (dos con
  Duplicador) a la vez: al plantar los nuevos, los anteriores se apagan. La recarga corre mientras el faro dispara.
  - Vínculo (era Robo de vida): hilo carmesí faro → player; daña lo que lo cruza (`linkDps`) y `lifesteal` del daño del faro cura.
  - Estallido: al apagarse descarga donde apunta (si no apunta a nadie, en el sitio): `novaDamage` en `novaRadius`, golpe fuerte.
    Primera versión estallaba en el faro y no alcanzaba a nadie: el propio haz empuja a los enemigos fuera del radio.
  - Vigía: cada baja del faro +`watchTime` s de vida (sin tope; los faros nuevos lo sustituyen igualmente).

## Elemento (sinergia: con 4 items de ese elemento) y Duplicador (item)

- **Hielo**: paleta hielo; cada tick suma `freezeTick`. **Al soltar** (o al apagarse el faro), todo lo que tocó y esté congelado
  recibe un golpe fuerte → añicos (canalizar hasta congelar y soltar = ritmo propio del arma). La descarga de Sobrecarga y el
  Estallido del Faro congelan algo más (×3 / 0,5).
- **Fuego**: cada tick quema `burnTick` s.
- **Duplicador**: dos haces separados ±`dupAngle` rad (sólo uno suena); con Faro, dos faros y el segundo busca al segundo más cercano.

## Sinergias

- **Singularidad**: los atraídos quedan juntos → el Prisma los encadena y la descarga de Sobrecarga los atraviesa a todos.
- **Faro + cualquier otra arma**: canalizar, cambiar de arma (planta el faro) y seguir con la otra.
- **Reacciones**: Hielo + soltar = añicos; un rayo de fuego (futuro) haría deshielo.
- **Golpe estelar con Marcas**: cada tick marca (muy rápido).

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `maxChannel` | 2 | 0.3 – 6 (paso 0.1) | Base: canalización máxima (s) |
| `cooldown` | 0.8 | 0 – 4 (paso 0.05) | Base: recarga tras soltar (s) |
| `range` | 520 | 100 – 900 (paso 10) | Base: alcance (px) |
| `turnRate` | 9 | 1 – 40 (paso 0.5) | Base: giro hacia el ratón (rad/s) |
| `tick` | 0.1 | 0.03 – 0.5 (paso 0.01) | Base: cada cuánto quema (s) |
| `tickDamage` | 6 | 1 – 40 (paso 1) | Base: daño por tick |
| `push` | 18 | 0 – 120 (paso 1) | Base: empuje por tick |
| `freezeTick` | 0.07 | 0 – 0.5 (paso 0.01) | Hielo: congelación por tick |
| `burnTick` | 0.6 | 0 – 4 (paso 0.1) | Fuego: quemadura por tick (s) |
| `dupAngle` | 0.07 | 0 – 0.4 (paso 0.01) | Duplicador: separación de los dos haces (rad) |
| `overMax` | 2.5 | 1 – 6 (paso 0.1) | Sobrecarga: × daño por tick con el calor al máximo |
| `overBurst` | 40 | 0 – 200 (paso 1) | Sobrecarga: daño de la descarga (calor al máximo) |
| `overMin` | 0.2 | 0 – 1 (paso 0.05) | Sobrecarga: calor mínimo para descargar (fracción) |
| `overHeavyAt` | 0.5 | 0 – 1 (paso 0.05) | Sobrecarga: calor para que la descarga sea golpe fuerte |
| `overHeatCd` | 2 | 1 – 5 (paso 0.1) | Sobrecarga: × recarga si se recalienta |
| `sweetFrom` | 0.75 | 0.3 – 0.98 (paso 0.01) | Sobrecarga · Punto justo: empieza la franja (fracción del calor) |
| `sweetMul` | 2 | 1 – 5 (paso 0.1) | Sobrecarga · Punto justo: × descarga |
| `pierceAt` | 0.5 | 0 – 1 (paso 0.05) | Sobrecarga · Perforante: calor a partir del que atraviesa |
| `waveRadius` | 90 | 20 – 250 (paso 5) | Sobrecarga · Onda: radio |
| `waveDamage` | 30 | 0 – 150 (paso 1) | Sobrecarga · Onda: daño (calor al máximo) |
| `prismCount` | 2 | 1 – 5 (paso 1) | Prisma: haces |
| `prismRange` | 300 | 50 – 600 (paso 10) | Prisma: alcance |
| `prismMul` | 0.6 | 0.1 – 1.5 (paso 0.05) | Prisma: daño de cada haz (×) |
| `chainJumps` | 1 | 1 – 4 (paso 1) | Prisma · Cadena: saltos extra por haz |
| `focusMul` | 0.6 | 0 – 2 (paso 0.05) | Prisma · Foco: + daño del principal por haz sin usar |
| `beaconTime` | 3 | 0.5 – 10 (paso 0.1) | Faro: duración (s) |
| `beaconMul` | 0.8 | 0.1 – 2 (paso 0.05) | Faro: × daño por tick |
| `beaconRange` | 420 | 100 – 900 (paso 10) | Faro: alcance para buscar enemigo |
| `linkDps` | 20 | 0 – 100 (paso 1) | Faro · Vínculo: daño/s del hilo |
| `lifesteal` | 0.3 | 0 – 1 (paso 0.01) | Faro · Vínculo: fracción del daño del faro que cura |
| `novaDamage` | 50 | 0 – 200 (paso 1) | Faro · Estallido: daño |
| `novaRadius` | 110 | 20 – 300 (paso 5) | Faro · Estallido: radio |
| `watchTime` | 1 | 0 – 4 (paso 0.1) | Faro · Vigía: + s de vida por baja |
<!-- /gen:tuning -->

## Visual

`energyBeam` (fx.js): 5 cintas (morado ancho → núcleo blanco) que respiran, dos hilos en hélice (por detrás y por delante),
anillos de flujo y motas que avanzan. `beamContact`: luz, destello que parpadea y chispas continuas hacia atrás.
`prismGem`, `lifeMotes`, `healCrosses`. Evoluciones (fx.js): `beamDischarge` (descarga de Sobrecarga y del Estallido),
`shockRing` (Onda, Estallido), `beaconCrystal` (cristal doble con aro que marca el tiempo que le queda).

## Sonido

`ray_charge` (clips), `ray_ignite`, zumbido en bucle mientras canaliza (dos sierras de 110 Hz desafinadas + armónico + ruido;
en la jugable se genera en `g-rayo.js`), tick grave, `ray_off`, `ray_split`, `ray_prism`, `ray_shatter`, `ray_life`.

## Port a Unity (plan)

Modo de disparo nuevo «canalizar» en `WeaponUser` (hoy: tocar o cargar). Raycast al primer enemigo/suelo cada frame; ticks de
daño por temporizador; el haz con `FxCanvas` (cintas por puntos, como la estela del dash).

## Decisiones

- 2026-10-19 · Árbol: Sobrecarga / Prisma / Faro. Prisma pasa de mejora a evolución; Robo de vida se rehace como Vínculo del Faro
  (robo de vida suelto ya es la etiqueta universal). Responde a la pregunta abierta: la Sobrecarga es evolución, no comportamiento base.

- 2026-10-19 · Calor en vez de munición: `maxChannel` 2 s, recarga 0,8 s.

## Preguntas abiertas

- ¿El faro debería poder plantarse lejos (donde apuntas) en vez de en la boca?
