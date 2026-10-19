# Rayo (`ray`)

**Estado**: prototipo web (clips + jugable), pendiente de prueba. Código: `a3-rayo.js`, `g-rayo.js`.

## Rol

Mantener para canalizar un haz continuo que quema por ticks al primer enemigo que toca. Daño sostenido; obliga a quedarse y
apuntar. Otra forma de disparar («mantener» en vez de «cargar y soltar»).

## Comportamiento (jugable)

- Mantener clic = haz desde el cristal. Se extiende de golpe (0,07 s) hasta el primer enemigo, el suelo o `range`.
- La dirección sigue al ratón con giro limitado (`turnRate` rad/s): barrer tiene inercia.
- Cada `tick` s: `tickDamage` al primero + empuje `push`. El calor sube mientras canalizas; a `maxChannel` s se corta solo.
- Al soltar (o al cambiar de arma, o al llegar al máximo): se apaga en 0,14 s dejando restos que flotan; recarga `cooldown`.

## Mejoras

<!-- gen:tree -->
Mejoras provisionales (el árbol con evoluciones está pendiente de diseñar):

| Mejora (id) | Qué hace |
|---|---|
| Prisma (`prism`) | Atraviesa al primero y se divide en haces dorados hacia los enemigos más cercanos. |
| Robo de vida (`life`) | Una parte del daño cura al player. |
<!-- /gen:tree -->

- **Prisma**: sale por detrás del primero y se divide en `prismCount` haces dorados hacia los enemigos más cercanos a ese punto
  (≤ `prismRange`), cada uno con ×`prismMul` del daño. Gema-prisma girando en el punto de salida.
- **Robo de vida**: `lifesteal` del daño cura al player (gotas carmesí que vuelven por el haz, cruces de curación).
  Para probarlo hay que activar «Los enemigos disparan» (Dificultad).

## Elemento y Duplicador (items generales)

- **Hielo**: paleta hielo; cada tick suma `freezeTick`. **Al soltar**, todo lo que tocó y esté congelado recibe un golpe
  fuerte → añicos (canalizar hasta congelar y soltar = ritmo propio del arma).
- **Fuego**: cada tick quema `burnTick` s.
- **Duplicador**: dos haces separados ±`dupAngle` rad (sólo uno suena).

## Sinergias

- **Singularidad**: los atraídos quedan en línea → el Prisma los encadena.
- **Reacciones**: Hielo + soltar = añicos; un rayo de fuego (futuro) haría deshielo.
- **Golpe estelar con Marcas**: cada tick marca (muy rápido).

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `maxChannel` | 2 | 0.3 – 6 (paso 0.1) | Canalización máxima (s) |
| `cooldown` | 0.8 | 0 – 4 (paso 0.05) | Recarga tras soltar (s) |
| `range` | 520 | 100 – 900 (paso 10) | Alcance (px) |
| `turnRate` | 9 | 1 – 40 (paso 0.5) | Giro hacia el ratón (rad/s) |
| `tick` | 0.1 | 0.03 – 0.5 (paso 0.01) | Cada cuánto quema (s) |
| `tickDamage` | 6 | 1 – 40 (paso 1) | Daño por tick |
| `push` | 18 | 0 – 120 (paso 1) | Empuje por tick |
| `prismCount` | 2 | 1 – 5 (paso 1) | Prisma: haces |
| `prismRange` | 300 | 50 – 600 (paso 10) | Prisma: alcance |
| `prismMul` | 0.6 | 0.1 – 1.5 (paso 0.05) | Prisma: daño de cada haz (×) |
| `freezeTick` | 0.07 | 0 – 0.5 (paso 0.01) | Hielo: congelación por tick |
| `lifesteal` | 0.3 | 0 – 1 (paso 0.01) | Robo de vida: fracción del daño |
| `burnTick` | 0.6 | 0 – 4 (paso 0.1) | Fuego: quemadura por tick (s) |
| `dupAngle` | 0.07 | 0 – 0.4 (paso 0.01) | Duplicador: separación de los dos haces (rad) |
<!-- /gen:tuning -->

## Visual

`energyBeam` (fx.js): 5 cintas (morado ancho → núcleo blanco) que respiran, dos hilos en hélice (por detrás y por delante),
anillos de flujo y motas que avanzan. `beamContact`: luz, destello que parpadea y chispas continuas hacia atrás.
`prismGem`, `lifeMotes`, `healCrosses`.

## Sonido

`ray_charge` (clips), `ray_ignite`, zumbido en bucle mientras canaliza (dos sierras de 110 Hz desafinadas + armónico + ruido;
en la jugable se genera en `g-rayo.js`), tick grave, `ray_off`, `ray_split`, `ray_prism`, `ray_shatter`, `ray_life`.

## Port a Unity (plan)

Modo de disparo nuevo «canalizar» en `WeaponUser` (hoy: tocar o cargar). Raycast al primer enemigo/suelo cada frame; ticks de
daño por temporizador; el haz con `FxCanvas` (cintas por puntos, como la estela del dash).

## Decisiones

- 2026-10-19 · Calor en vez de munición: `maxChannel` 2 s, recarga 0,8 s.

## Preguntas abiertas

- ¿Sobrecarga (más grueso con el tiempo y estallido al soltar) como mod o como comportamiento base?
