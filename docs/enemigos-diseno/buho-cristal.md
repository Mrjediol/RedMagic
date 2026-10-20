# Búho de cristal

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Búho de cristal)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** bosque · **Rol:** Volador que se posa y cae en picado · **Cuerpo:** volador, hitbox 40×44 px (≈ 1.0×1.1 u) · código `e-*.js` id `owl`

**Pregunta al jugador:** Leer la curva del picado (sale marcada: pasa por donde estás y sube al otro lado) y apartarte de ella; pegarle cuando está posado. Si te acercas a su rama, se va volando a otra.

**Movimientos**

- **Posarse** — Vuela a una rama alta o a una plataforma a media distancia de ti y se queda ahí mirándote (gira la cabeza). Si te acercas, se va a otra.
- **Picado** — Se agacha en la rama; se marca una curva desde él, pasando por ti, hasta el otro lado. Baja por ella muy rápido y sube a posarse en otro sitio.
- **Plumas** — Desde la rama, sacude las alas y lanza un abanico de plumas de cristal hacia ti.

**Combina con:** Con el Oso (arriba el búho, abajo el oso). Con Zarzas: la curva del picado y el hueco del muro a la vez.

**Arte (idea, sin decidir):** Búho o lechuza grande de plumaje gris pardo con las puntas de las plumas de cristal turquesa; disco facial claro y dos ojos enormes turquesa que brillan; penachos en las orejas. Posado: alas plegadas, gira la cabeza. Picado: alas pegadas, cuerpo en flecha. Volando: alas abiertas. Partes: cuerpo, cabeza (gira), 2 alas (plegada / abierta / pegada), patas. ~1×1,1 u.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_owl`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 85 | 5 – 1500 (paso 5) | Vida |
| `poise` | 25 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 1.2 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 0 | 0 – 40 (paso 1) | Daño por contacto |
| `perchMin` | 180 | 0 – 500 (paso 10) | Se posa a (mín.) de ti |
| `perchMax` | 420 | 100 – 900 (paso 10) | Se posa a (máx.) de ti |
| `speed` | 280 | 60 – 600 (paso 10) | Velocidad volando |
| `perchTime` | 1.4 | 0.2 – 5 (paso 0.1) | Posado antes de atacar (s) |
| `flee` | 140 | 0 – 400 (paso 10) | Se va si te acercas a menos de |
| `swoopChance` | 0.6 | 0 – 1 (paso 0.05) | Probabilidad de picado (si no, plumas) |
| `swoopWindup` | 0.6 | 0.1 – 2 (paso 0.05) | Picado: preparación (s) |
| `swoopTime` | 0.85 | 0.3 – 2 (paso 0.05) | Picado: duración (s) |
| `swoopDmg` | 14 | 0 – 50 (paso 1) | Picado: daño |
| `featherWindup` | 0.45 | 0.1 – 1.5 (paso 0.05) | Plumas: preparación (s) |
| `featherN` | 3 | 1 – 7 (paso 1) | Plumas: cuántas |
| `featherSpeed` | 430 | 100 – 900 (paso 10) | Plumas: velocidad |
| `featherDmg` | 8 | 0 – 40 (paso 1) | Plumas: daño |
| `cooldown` | 1 | 0 – 5 (paso 0.1) | Pausa entre ataques (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
