# Erizo de escarcha

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Erizo de escarcha)

## Diseño
<!-- gen:diseno -->
**Nivel:** común · **Mundo:** hielo · **Rol:** Artillero de suelo: lluvia desde arriba · **Cuerpo:** de suelo, hitbox 60×44 px (≈ 1.5×1.1 u) · código `e-*.js` id `urchin`

**Pregunta al jugador:** Salir de las marcas de la lluvia (caen también en tu plataforma: subir no salva) y castigarle cuando se queda sin púas; no pegarte a él cuando se eriza.

**Movimientos**

- **Lluvia de púas** — Se encoge y dispara sus púas hacia arriba; un momento después caen en varios sitios alrededor de ti (marcas en el suelo o en tu plataforma desde que las dispara).
- **Pelado** — Tras la lluvia se queda sin púas un rato: recibe más daño (anillo rosa) hasta que le vuelven a crecer.
- **Erizarse** — Si te pegas a él, saca todas las púas (preparación corta): tocarlo mientras está erizado duele y empuja.
- **Huir y Rodar** — Entre ataques se recoloca a ráfagas a media distancia. Si te acercas, huye a saltitos. Si lo acorralas contra la pared, se hace bola de púas y rueda a través de ti hasta el otro lado (salta por encima).

**Combina con:** Con Colgantes: arriba (carámbanos) y abajo a la vez. Con Morsa: los pinchos del suelo te encierran y llueve.

**Arte (idea, sin decidir):** Erizo o puercoespín de tundra, redondo y bajo, pelo blanco crema sucio; en el lomo un abanico de púas que son carámbanos (azul pálido, translúcidos en la punta). Hocico oscuro, ojos pequeños. Estados: normal, encogido (púas apuntando arriba), pelado (lomo rosado con muñones), erizado (púas largas en todas direcciones). Partes: cuerpo, cabeza, 4 patas, capa de púas (3 estados). ~1,5×1,1 u.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_urchin`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 110 | 5 – 1500 (paso 5) | Vida |
| `poise` | 35 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0.8 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 4 | 0 – 40 (paso 1) | Daño por contacto |
| `keepMin` | 230 | 0 – 600 (paso 10) | Le gusta estar a (mín.) |
| `keepMax` | 360 | 50 – 700 (paso 10) | Le gusta estar a (máx.) |
| `walk` | 85 | 0 – 250 (paso 5) | Velocidad andando |
| `flee` | 180 | 0 – 400 (paso 10) | Huye si te acercas a menos de |
| `rollWindup` | 0.35 | 0.1 – 1.5 (paso 0.05) | Rodar: preparación (s) |
| `rollSpeed` | 430 | 100 – 900 (paso 10) | Rodar: velocidad |
| `rollDist` | 420 | 100 – 1000 (paso 10) | Rodar: recorrido |
| `rollDmg` | 12 | 0 – 50 (paso 1) | Rodar: daño |
| `rainWindup` | 0.6 | 0.1 – 2 (paso 0.05) | Lluvia: preparación (s) |
| `quills` | 5 | 1 – 12 (paso 1) | Lluvia: púas |
| `spread` | 70 | 0 – 200 (paso 5) | Lluvia: separación |
| `rainDelay` | 1 | 0.3 – 3 (paso 0.05) | Lluvia: de disparar a caer (s) |
| `quillR` | 18 | 6 – 60 (paso 1) | Lluvia: radio de cada púa |
| `quillDmg` | 9 | 0 – 40 (paso 1) | Lluvia: daño |
| `bald` | 1.8 | 0 – 5 (paso 0.1) | Pelado (s) |
| `baldVuln` | 1.5 | 1 – 3 (paso 0.05) | Pelado: × daño recibido |
| `bristleRange` | 90 | 20 – 250 (paso 5) | Erizarse: si estás a menos de |
| `bristleWindup` | 0.25 | 0.05 – 1 (paso 0.05) | Erizarse: preparación (s) |
| `bristleTime` | 1.2 | 0.2 – 4 (paso 0.1) | Erizado (s) |
| `bristleDmg` | 10 | 0 – 40 (paso 1) | Erizado: daño al tocarlo |
| `cooldown` | 2 | 0.2 – 6 (paso 0.1) | Pausa entre lluvias (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
- 2026-10-20 · El usuario: le disparaba desde fuera de pantalla y entre ataques no hacía nada; «si me acerco debería intentar huir». → regla general de pantalla; ahora se recoloca a ráfagas, huye a saltitos y, acorralado, rueda a través de ti.
- 2026-10-20 · Al usuario le ha gustado, sobre todo rodar al estar acorralado (y que huya).
