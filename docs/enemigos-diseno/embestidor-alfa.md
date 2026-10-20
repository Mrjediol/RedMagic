# Embestidor alfa

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Embestidor alfa)

## Diseño
<!-- gen:diseno -->
**Nivel:** élite · **Rol:** Élite del Embestidor: cargas encadenadas y furia · **Cuerpo:** de suelo, hitbox 108×74 px (≈ 2.7×1.9 u) · código `e-*.js` id `chargerAlpha`

**Pregunta al jugador:** Lo mismo que el Embestidor, pero sin respiro: tras una carga que esquivas viene otra enseguida. Ponerte delante de la pared corta la cadena (el choque sigue siendo tu premio).

**Movimientos**

- **Embestida encadenada** — Como la del común, pero al derrapar se gira y vuelve a cargar con una preparación corta (flechas otra vez). Si choca con la pared, la cadena se acaba y queda aturdido.
- **Pisotón con ondas** — El pisotón del común (también sacude tu plataforma) y además suelta dos ondas por el suelo a los lados: saltarlas.
- **Furia** — A media vida ruge: todo un 25 % más rápido (preparaciones, cargas y pausas) hasta que muere.

**Combina con:** Solo ya es un encuentro. Con un Artillero detrás: carga + morteros. Con Fatuos: atropella fatuos y le revientan encima.

**Arte (idea, sin decidir):** El Embestidor más grande, con una cresta de cristal en el lomo y cicatrices; que se reconozca como el mismo bicho.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_chargerAlpha`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 600 | 5 – 1500 (paso 5) | Vida |
| `poise` | 140 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.15 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 5 | 0 – 40 (paso 1) | Daño por contacto |
| `keepMin` | 220 | 0 – 600 (paso 10) | Distancia mínima (se aparta) |
| `keepMax` | 420 | 100 – 900 (paso 10) | Distancia máxima (se acerca) |
| `walk` | 80 | 0 – 300 (paso 5) | Velocidad andando |
| `windup` | 0.8 | 0.2 – 2 (paso 0.05) | Embestida: preparación (s) |
| `lockAt` | 0.7 | 0 – 1 (paso 0.05) | Embestida: fija la dirección a esta fracción |
| `chargeSpeed` | 700 | 200 – 1200 (paso 10) | Embestida: velocidad |
| `chargeMax` | 720 | 200 – 1400 (paso 10) | Embestida: recorrido máximo |
| `passBy` | 150 | 0 – 500 (paso 10) | Embestida: sigue tanto tras pasarte |
| `chargeDmg` | 20 | 0 – 60 (paso 1) | Embestida: daño |
| `allyDmg` | 12 | 0 – 60 (paso 1) | Embestida: daño a otros enemigos |
| `crashStun` | 1.8 | 0 – 5 (paso 0.1) | Choque con pared: aturdido (s) |
| `crashVuln` | 1.5 | 1 – 3 (paso 0.05) | Choque con pared: × daño recibido |
| `skidTime` | 0.55 | 0.1 – 2 (paso 0.05) | Derrape (s, ventana de castigo) |
| `stompRange` | 120 | 0 – 300 (paso 5) | Pisotón: si estás a menos de |
| `stompWindup` | 0.5 | 0.1 – 1.5 (paso 0.05) | Pisotón: preparación (s) |
| `stompR` | 130 | 30 – 300 (paso 5) | Pisotón: radio |
| `stompDmg` | 12 | 0 – 60 (paso 1) | Pisotón: daño |
| `upDelay` | 0.6 | 0 – 3 (paso 0.05) | Si estás encima de una plataforma más de (s): pisotón |
| `shakeR` | 320 | 0 – 600 (paso 10) | Pisotón: sacude las plataformas a menos de |
| `shakeDmg` | 6 | 0 – 40 (paso 1) | Pisotón: daño al tirarte de la plataforma |
| `cooldown` | 1.1 | 0 – 5 (paso 0.1) | Pausa entre ataques (s) |
| `chain` | 1 | 0 – 3 (paso 1) | Embestida: cargas extra encadenadas |
| `windup2` | 0.4 | 0.1 – 1.5 (paso 0.05) | Carga encadenada: preparación (s) |
| `waveSpeed` | 360 | 80 – 800 (paso 10) | Ondas del pisotón: velocidad |
| `waveDist` | 480 | 50 – 1200 (paso 10) | Ondas del pisotón: recorrido |
| `waveH` | 30 | 10 – 90 (paso 1) | Ondas del pisotón: altura |
| `waveDmg` | 10 | 0 – 60 (paso 1) | Ondas del pisotón: daño |
| `rageAt` | 0.5 | 0 – 1 (paso 0.05) | Furia: a esta fracción de vida |
| `rageK` | 1.25 | 1 – 2 (paso 0.05) | Furia: × rapidez |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
