# Zarza

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Zarza)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** bosque · **Rol:** Tiradora de patrones (muro con hueco) · **Cuerpo:** de suelo, hitbox 52×68 px (≈ 1.3×1.7 u) · código `e-*.js` id `bramble`

**Pregunta al jugador:** Mirar dónde está el hueco del muro de espinas (abajo: quédate de pie; en medio o arriba: salta en el momento justo) y perseguirla, que huye; si la acorralas, cuidado con el latigazo.

**Movimientos**

- **Muro de espinas** — Se tensa y marca varias líneas horizontales hacia ti (de abajo arriba); dispara una espina por cada línea, todas a la vez. Las líneas que faltan son el hueco por donde pasar.
- **Huir** — Entre ataques se recoloca a ráfagas lejos de ti; si te acercas, huye a saltitos.
- **Latigazo** — Si te pegas a ella (o la acorralas contra la pared), gira sus ramas en círculo: daño y te aparta lejos. Luego se aleja.

**Combina con:** Con Saltasetas: saltar el muro sin caer en un gran salto. Con Atrapamoscas: la liana te deja quieto delante del muro.

**Arte (idea, sin decidir):** Arbusto de zarzas oscuro con forma de bola que camina sobre tres raíces; ramas con espinas a modo de brazos, dos bayas rojas como ojos y hojas verde bosque. Al disparar, las ramas se tensan hacia atrás. Partes: cuerpo (hojas), 2-3 ramas, raíces, ojos. ~1,3×1,7 u.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_bramble`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 100 | 5 – 1500 (paso 5) | Vida |
| `poise` | 30 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.7 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 3 | 0 – 40 (paso 1) | Daño por contacto |
| `keepMin` | 260 | 50 – 700 (paso 10) | Le gusta estar a (mín.) |
| `keepMax` | 380 | 100 – 800 (paso 10) | Le gusta estar a (máx.) |
| `walk` | 80 | 0 – 250 (paso 5) | Velocidad |
| `flee` | 170 | 0 – 400 (paso 10) | Huye si te acercas a menos de |
| `wallWindup` | 0.75 | 0.2 – 2 (paso 0.05) | Muro: preparación (s) |
| `lanes` | 5 | 3 – 8 (paso 1) | Muro: líneas |
| `laneGap` | 36 | 20 – 60 (paso 1) | Muro: separación entre líneas |
| `gapLanes` | 3 | 1 – 5 (paso 1) | Muro: líneas del hueco |
| `thornSpeed` | 420 | 150 – 900 (paso 10) | Muro: velocidad |
| `thornDmg` | 9 | 0 – 40 (paso 1) | Muro: daño |
| `cooldown` | 2 | 0.3 – 6 (paso 0.1) | Pausa entre muros (s) |
| `whipRange` | 90 | 30 – 200 (paso 5) | Latigazo: si estás a menos de |
| `whipWindup` | 0.3 | 0.1 – 1 (paso 0.05) | Latigazo: preparación (s) |
| `whipR` | 95 | 30 – 200 (paso 5) | Latigazo: radio |
| `whipDmg` | 8 | 0 – 40 (paso 1) | Latigazo: daño |
| `whipPush` | 620 | 0 – 1200 (paso 10) | Latigazo: empujón |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
