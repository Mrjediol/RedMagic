# Embestidor

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Embestidor)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Rol:** Tanque de suelo que carga · **Cuerpo:** de suelo, hitbox 88×60 px (≈ 2.2×1.5 u) · código `e-*.js` id `charger`

**Pregunta al jugador:** Saltar o esquivar la carga en el último momento; colocarte delante de una pared para que se estampe y quede vendido.

**Movimientos**

- **Embestida** — Rasca el suelo (polvo + chevrones del alto de su cuerpo) y carga en línea recta. Fija la dirección un poco antes de salir: si te mueves al final, falla. Si choca con la pared se queda aturdido y recibe ×1,5. Si le saltas, derrapa y queda de espaldas. Atropella a los demás enemigos que encuentra.
- **Pisotón** — Si estás pegado a él: se encabrita y pisa. Zona circular en el suelo; saltar la esquiva. Si te subes a una plataforma cerca de él, el mismo pisotón la sacude (se marca en rojo) y te tira al suelo: no hay sitio seguro encima.

**Combina con:** Con Fatuos: atrae su carga hacia un fatuo o hacia otros enemigos (los atropella). Con Artilleros: la carga te obliga a moverte mientras caen morteros.

**Arte (idea, sin decidir):** Silueta ancha y baja, cuatro patas cortas, cabeza gacha con cuernos/colmillos de cristal. Jabalí de raíces, carnero de piedra, escarabajo rinoceronte.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_charger`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 260 | 5 – 1500 (paso 5) | Vida |
| `poise` | 70 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.35 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 5 | 0 – 40 (paso 1) | Daño por contacto |
| `keepMin` | 220 | 0 – 600 (paso 10) | Distancia mínima (se aparta) |
| `keepMax` | 420 | 100 – 900 (paso 10) | Distancia máxima (se acerca) |
| `walk` | 80 | 0 – 300 (paso 5) | Velocidad andando |
| `windup` | 0.8 | 0.2 – 2 (paso 0.05) | Embestida: preparación (s) |
| `lockAt` | 0.7 | 0 – 1 (paso 0.05) | Embestida: fija la dirección a esta fracción |
| `chargeSpeed` | 640 | 200 – 1200 (paso 10) | Embestida: velocidad |
| `chargeMax` | 720 | 200 – 1400 (paso 10) | Embestida: recorrido máximo |
| `passBy` | 150 | 0 – 500 (paso 10) | Embestida: sigue tanto tras pasarte |
| `chargeDmg` | 18 | 0 – 60 (paso 1) | Embestida: daño |
| `allyDmg` | 12 | 0 – 60 (paso 1) | Embestida: daño a otros enemigos |
| `crashStun` | 1.8 | 0 – 5 (paso 0.1) | Choque con pared: aturdido (s) |
| `crashVuln` | 1.5 | 1 – 3 (paso 0.05) | Choque con pared: × daño recibido |
| `skidTime` | 0.55 | 0.1 – 2 (paso 0.05) | Derrape (s, ventana de castigo) |
| `stompRange` | 120 | 0 – 300 (paso 5) | Pisotón: si estás a menos de |
| `stompWindup` | 0.5 | 0.1 – 1.5 (paso 0.05) | Pisotón: preparación (s) |
| `stompR` | 130 | 30 – 300 (paso 5) | Pisotón: radio |
| `stompDmg` | 12 | 0 – 60 (paso 1) | Pisotón: daño |
| `upDelay` | 0.6 | 0 – 3 (paso 0.05) | Si estás encima de una plataforma más de (s): pisotón |
| `shakeR` | 260 | 0 – 600 (paso 10) | Pisotón: sacude las plataformas a menos de |
| `shakeDmg` | 6 | 0 – 40 (paso 1) | Pisotón: daño al tirarte de la plataforma |
| `cooldown` | 1.3 | 0 – 5 (paso 0.1) | Pausa entre ataques (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
- 2026-10-20 · Respuesta hacia arriba (el usuario no veía cómo encajan los de suelo con tantas plataformas): ver «Movimientos»; ajustes nuevos en la tabla.
