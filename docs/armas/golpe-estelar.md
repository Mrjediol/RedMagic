# Golpe estelar (`star`)

**Estado**: prototipo web (clips + jugable), pendiente de prueba. Código: `a6-golpe-estelar.js`, `g-golpe-estelar.js`.

## Rol

El cristal lanza una señal al cielo, un círculo rúnico marca el suelo y, tras el aviso, cae una lanza de luz que golpea todo lo
que hay en esa columna. Mucho daño, mucho retraso: hay que leer hacia dónde va el enemigo. Consumidor natural de **marcas**.

## Comportamiento (jugable)

- Clic = marca el suelo en la x del ratón. El círculo se cierra durante `delay` s mientras crece un destello arriba.
- Cae la lanza (0,07 s) y golpea a **todo enemigo cuya x esté dentro de la columna** (`width`), voladores incluidos:
  `damage`, golpe fuerte, hit-stop, impact frames; los voladores alcanzados se estampan contra el suelo. Alrededor (`shockRadius`,
  sólo cerca del suelo) una onda de `shockDamage` sin reacciones.

<!-- gen:mods -->
| Mod (`id`) | Qué hace |
|---|---|
| Marcas (`marks`) | Tus otras armas marcan al golpear; la lanza cae en cadena sobre los marcados (sin apuntar). |
| Fuego (`fire`) | Deja un cráter ardiendo que quema a quien lo pisa. |
| Lluvia (`rain`) | Lluvia de agujas en diagonal sobre una zona más ancha. |
<!-- /gen:mods -->

- **Marcas** (sistema): mientras esté equipado con este mod, **los golpes de tus otras armas marcan** (rombo encima, 8 s). Al
  pulsar, si hay marcados, la lanza cae en cadena sobre cada uno (hasta `maxTargets`, del más cercano al más lejano, cada
  `chainGap` s) siguiéndolos durante el aviso; consume las marcas. Sin marcados, cae donde apuntas.
- **Fuego**: paleta fuego; deja un cráter con llamas durante `craterTime` s que quema (`craterBurn` s) a quien lo pisa; contra
  congelados, deshielo.
- **Lluvia**: paleta oro; en vez de una lanza, `rainNeedles` agujas en diagonal repartidas en `rainWidth` px durante `rainTime`
  s; cada una golpea lo primero que corta (`needleDamage`).

## Sinergias

- **Cualquier arma rápida** (Rayo, Abanico) + Marcas: marcas rápido, rematas sin apuntar.
- **Singularidad**: los atraídos quedan juntos y quietos → una lanza los alcanza a todos.
- **Semilla con Hielo**: congela el grupo, la lanza lo rompe.

<!-- gen:tuning -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `cooldown` | 3 | 0.3 – 10 (paso 0.1) | Recarga (s) |
| `delay` | 0.6 | 0.1 – 2 (paso 0.05) | Aviso hasta que cae (s) |
| `damage` | 70 | 1 – 300 (paso 1) | Daño de la lanza |
| `width` | 40 | 10 – 120 (paso 2) | Ancho de la columna |
| `shockDamage` | 15 | 0 – 100 (paso 1) | Onda: daño |
| `shockRadius` | 120 | 0 – 300 (paso 5) | Onda: radio |
| `maxTargets` | 5 | 1 – 10 (paso 1) | Marcas: objetivos máx. |
| `chainGap` | 0.15 | 0.03 – 0.6 (paso 0.01) | Marcas: tiempo entre lanzas (s) |
| `craterTime` | 1.3 | 0.2 – 5 (paso 0.1) | Fuego: duración del cráter (s) |
| `craterBurn` | 2 | 0 – 6 (paso 0.1) | Fuego: quemadura (s) |
| `rainNeedles` | 15 | 3 – 40 (paso 1) | Lluvia: agujas |
| `rainWidth` | 170 | 40 – 400 (paso 5) | Lluvia: ancho |
| `rainTime` | 0.55 | 0.1 – 2 (paso 0.05) | Lluvia: duración (s) |
| `needleDamage` | 12 | 1 – 60 (paso 1) | Lluvia: daño por aguja |
<!-- /gen:tuning -->

## Visual

Señal vertical desde el cristal, `runeCircle` (arcos que giran en sentidos opuestos, marcas radiales, columna guía tenue),
`starLance` (columna por capas + punta), agujas de velocidad durante la caída, `groundImpact` grande + `cracks`. Cráter: elipse
oscura + aro + llamas. Lluvia: `rainNeedle`.

## Sonido

`star_up`, `star_mark`, tensión (clips), `star_fall` (silbido que baja), `star_impact`, `star_needle`, `star_markon`, quemadura.

## Port a Unity (plan)

Habilidad diferida con recarga larga. El sistema de marcas es genérico (estado en el enemigo + regla «golpe de otra arma
marca» + consumidores), no lógica de esta arma.

## Decisiones

- 2026-10-19 · La lanza golpea toda la columna (también voladores), no sólo un radio en el suelo.

## Preguntas abiertas

- ¿Constelación (transformación: cada 5º disparo invoca un golpe pequeño gratis)?
