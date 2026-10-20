# Artillero

Estado: **prototipo web, sin probar por el usuario** · jugable: `docs/bocetos/enemigos/jugable.html` (Duelo ▸ Artillero)

## Diseño
<!-- gen:diseno -->
**Rol:** Torreta enraizada: niega zonas a distancia · **Cuerpo:** de suelo, hitbox 56×80 px (≈ 1.4×2.0 u) · código `e-*.js` id `mortar`

**Pregunta al jugador:** Acercarte esquivando los morteros (no quedarte quieto ni en los charcos) y castigarle cuando está abierto; de cerca se cierra y suelta púas.

**Movimientos**

- **Mortero** — Se hincha y escupe 3 bolas en parábola a donde vas a estar (una en tu sitio y dos a los lados). Cada una marca en el suelo su explosión desde que sale. Al caer deja un charco que hace daño unos segundos.
- **Cerrarse** — Si te pegas a él se cierra en su corteza: recibe sólo ×0,2 (los golpes fuertes ×0,5).
- **Brote** — Tras cerrarse, se abre de golpe soltando un abanico de púas hacia arriba y a los lados. Después queda abierto un rato sin poder cerrarse: tu ventana.

**Combina con:** Detrás de un Caparazón (el escudo tapa, el artillero bombardea). Con Embestidor: la carga te empuja a los charcos.

**Arte (idea, sin decidir):** Planta bulbosa con raíces a la vista, boca que se abre hacia ti y corteza que se cierra como una piña. Flor carnívora, seta de cristal.
<!-- /gen:diseno -->

## Ajustes (`G.tuning.foe_mortar`, px y s; px × 0,025 = unidades)
<!-- gen:ajustes -->
| Campo | Valor | Rango | Qué es |
|---|---:|---|---|
| `hp` | 140 | 5 – 1500 (paso 5) | Vida |
| `poise` | 40 | 0 – 300 (paso 5) | Aguante: daño que le interrumpe mientras prepara (0 = nunca) |
| `kb` | 0 | 0 – 3 (paso 0.05) | Empuje recibido × |
| `contact` | 0 | 0 – 40 (paso 1) | Daño por contacto |
| `range` | 820 | 100 – 1400 (paso 10) | Alcance |
| `shots` | 3 | 1 – 7 (paso 1) | Mortero: bolas por ataque |
| `spread` | 85 | 0 – 250 (paso 5) | Mortero: separación entre bolas |
| `flight` | 1.1 | 0.4 – 2.5 (paso 0.05) | Mortero: tiempo de vuelo (s) |
| `lead` | 0.5 | 0 – 1.5 (paso 0.05) | Mortero: adelanta tu movimiento (×vuelo) |
| `shotDmg` | 12 | 0 – 60 (paso 1) | Mortero: daño |
| `blastR` | 46 | 15 – 150 (paso 1) | Mortero: radio de la explosión |
| `puddleTime` | 2.5 | 0 – 8 (paso 0.1) | Charco: duración (s) |
| `puddleDmg` | 4 | 0 – 30 (paso 1) | Charco: daño por toque |
| `windup` | 0.7 | 0.1 – 2 (paso 0.05) | Mortero: preparación (s) |
| `cooldown` | 2.2 | 0.2 – 6 (paso 0.1) | Pausa entre morteros (s) |
| `hideRange` | 150 | 0 – 400 (paso 5) | Se cierra si estás a menos de |
| `hideTime` | 1.3 | 0.2 – 4 (paso 0.1) | Cerrado (s) |
| `hideMul` | 0.2 | 0 – 1 (paso 0.05) | Cerrado: × daño recibido |
| `burstWindup` | 0.35 | 0.1 – 1.5 (paso 0.05) | Brote: preparación (s) |
| `ringN` | 8 | 3 – 16 (paso 1) | Brote: púas |
| `ringSpeed` | 190 | 60 – 500 (paso 5) | Brote: velocidad |
| `ringDmg` | 8 | 0 – 40 (paso 1) | Brote: daño |
| `hideGrace` | 2 | 0 – 6 (paso 0.1) | Tras el brote: tiempo sin poder cerrarse (s) |
<!-- /gen:ajustes -->

## Decisiones y feedback

- 2026-10-20 · Diseñado sin arte (primero el comportamiento, luego se busca el arte). Pendiente de que el usuario lo pruebe.
