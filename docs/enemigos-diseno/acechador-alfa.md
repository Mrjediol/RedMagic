# Acechador alfa

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Acechador alfa)

## Diseño
<!-- gen:diseno -->
**Nivel:** élite · **Rol:** Élite del Acechador: jefe de manada · **Cuerpo:** de suelo, hitbox 76×50 px (≈ 1.9×1.3 u) · código `e-*.js` id `stalkerAlpha`

**Pregunta al jugador:** Prioridad y lectura: si no lo matas, aúlla y su manada va más rápido (y si está solo, la llama). De cerca no huye: zarpazo. Sus saltos vienen de dos en dos.

**Movimientos**

- **Aullido** — Se para y aúlla (se puede interrumpir con daño): los acechadores cercanos (él incluido) van más rápido unos segundos (brillo rojo). Si quedan pocos, LLAMA a dos más, que entran por el borde de la arena.
- **Doble salto** — El abalanzarse del común, pero al caer se vuelve a agachar un instante y salta otra vez a donde estás.
- **Zarpazo** — Si te pegas a él no salta atrás como los demás: golpe rápido delante (preparación corta).

**Combina con:** Con 2 Acechadores (su manada). Con una Tejedora: el enredo + la manada acelerada.

**Arte (idea, sin decidir):** El Acechador más grande, con melena de cristal y una cicatriz; que se lea como el jefe de los mismos lobos.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_stalkerAlpha`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 300 | 5 – 1500 (paso 5) | Vida |
| `poise` | 80 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.6 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 4 | 0 – 40 (paso 1) | Daño por contacto |
| `ringMin` | 190 | 40 – 500 (paso 10) | Rodea a: distancia mínima |
| `ringMax` | 320 | 60 – 700 (paso 10) | Rodea a: distancia máxima |
| `run` | 230 | 50 – 500 (paso 5) | Velocidad corriendo |
| `crouch` | 0.45 | 0.1 – 1.5 (paso 0.05) | Abalanzarse: preparación (s) |
| `leapH` | 110 | 20 – 300 (paso 5) | Abalanzarse: altura del arco |
| `lead` | 0.25 | 0 – 1 (paso 0.05) | Abalanzarse: adelanta tu movimiento (s) |
| `leapDmg` | 16 | 0 – 60 (paso 1) | Abalanzarse: daño |
| `landRecover` | 0.7 | 0 – 2 (paso 0.05) | Al caer: recuperación (s, castigo) |
| `feint` | 0.15 | 0 – 1 (paso 0.05) | Probabilidad de finta |
| `hopV` | 330 | 100 – 700 (paso 10) | Salto atrás: impulso |
| `flank` | 1 | 0 – 1 (paso 1) | Manada: repartirse a los dos lados (1 = sí) |
| `cooldown` | 1.3 | 0 – 5 (paso 0.1) | Pausa entre ataques (s) |
| `chain` | 1 | 0 – 3 (paso 1) | Saltos encadenados |
| `crouch2` | 0.25 | 0.05 – 1 (paso 0.05) | Salto encadenado: preparación (s) |
| `howlEvery` | 11 | 2 – 40 (paso 0.5) | Aullido: cada (s) |
| `howlWindup` | 0.9 | 0.2 – 3 (paso 0.05) | Aullido: preparación (s) |
| `howlR` | 700 | 100 – 1500 (paso 10) | Aullido: alcance |
| `howlTime` | 5 | 0 – 15 (paso 0.5) | Aullido: duración del acelerón (s) |
| `callN` | 2 | 0 – 5 (paso 1) | Llamada: si quedan menos de N acechadores, llama hasta N |
| `callCd` | 16 | 2 – 60 (paso 1) | Llamada: pausa (s) |
| `swipeRange` | 110 | 30 – 250 (paso 5) | Zarpazo: si estás a menos de |
| `swipeWindup` | 0.3 | 0.1 – 1 (paso 0.05) | Zarpazo: preparación (s) |
| `swipeDmg` | 14 | 0 – 60 (paso 1) | Zarpazo: daño |
| `swipeRec` | 0.45 | 0 – 2 (paso 0.05) | Zarpazo: recuperación (s, castigo) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
